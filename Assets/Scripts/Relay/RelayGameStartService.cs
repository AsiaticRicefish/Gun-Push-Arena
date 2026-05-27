using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>
/// Relay 연결을 준비하고 Netcode Host/Client를 시작하는 서비스입니다.
/// Firestore room 상태 변경과 씬 이동은 호출하는 쪽에서 처리하고,
/// 이 클래스는 Relay 할당/참가, UnityTransport 설정, NetworkManager 시작만 담당합니다.
/// </summary>
public sealed class RelayGameStartService
{
    private const string RelayConnectionType = "dtls";

    private readonly UnityGameServicesInitializer servicesInitializer;
    private readonly NetworkSessionRegistry sessionRegistry;
    private readonly NetworkConnectionApprovalHandler approvalHandler;

    // Map1이 로드된 뒤 Host가 플레이어를 스폰할 때 현재 clientId <-> userId 매핑을 찾기 위한 런타임 참조입니다.
    public static NetworkSessionRegistry ActiveSessionRegistry { get; private set; }

    public string LastRelayJoinCode { get; private set; }

    public RelayGameStartService(
        UnityGameServicesInitializer servicesInitializer,
        NetworkSessionRegistry sessionRegistry,
        NetworkConnectionApprovalHandler approvalHandler)
    {
        this.servicesInitializer = servicesInitializer;
        this.sessionRegistry = sessionRegistry;
        this.approvalHandler = approvalHandler;

        ActiveSessionRegistry = sessionRegistry;
    }

    public async UniTask<bool> StartHostWithRelayAsync(
        string roomId,
        string localUserId,
        int maxClientConnections)
    {
        if (!ValidateSession(roomId, localUserId))
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        UnityTransport transport = GetUnityTransport(networkManager);

        if (networkManager == null || transport == null)
        {
            return false;
        }

        MakeNetworkManagerPersistent(networkManager);

        if (!await EnsureServicesReadyAsync())
        {
            return false;
        }

        try
        {
            // Host가 Relay 서버에 방을 만들고, 다른 클라이언트가 사용할 join code를 발급받습니다.
            Allocation allocation = await RelayService.Instance
                .CreateAllocationAsync(maxClientConnections)
                .AsUniTask();

            string joinCode = await RelayService.Instance
                .GetJoinCodeAsync(allocation.AllocationId)
                .AsUniTask();

            // Relay 할당 정보를 Unity Transport에 넣어 Netcode가 Relay 경유로 통신하게 합니다.
            RelayServerData relayServerData = allocation.ToRelayServerData(RelayConnectionType);
            transport.SetRelayServerData(relayServerData);

            approvalHandler.Register(networkManager);
            SetConnectionPayload(networkManager, roomId, localUserId);

            bool started = networkManager.StartHost();

            if (!started)
            {
                Debug.LogWarning("[RelayGameStartService] StartHost failed.");
                return false;
            }

            LastRelayJoinCode = joinCode;

            // Host 자신도 PlayerObject 스폰 대상이므로 clientId -> userId 매핑을 보장합니다.
            sessionRegistry.Register(networkManager.LocalClientId, localUserId);

            Debug.Log($"[RelayGameStartService] Host started. JoinCode: {joinCode}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RelayGameStartService] StartHostWithRelayAsync failed: {e}");
            return false;
        }
    }

    public async UniTask<bool> StartClientWithRelayAsync(
        string roomId,
        string localUserId,
        string relayJoinCode)
    {
        if (!ValidateSession(roomId, localUserId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(relayJoinCode))
        {
            Debug.LogWarning("[RelayGameStartService] Relay join code is empty.");
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        UnityTransport transport = GetUnityTransport(networkManager);

        if (networkManager == null || transport == null)
        {
            return false;
        }

        MakeNetworkManagerPersistent(networkManager);

        if (!await EnsureServicesReadyAsync())
        {
            return false;
        }

        try
        {
            // Firestore room에 저장된 join code로 Host의 Relay 할당에 참가합니다.
            JoinAllocation joinAllocation = await RelayService.Instance
                .JoinAllocationAsync(relayJoinCode.Trim())
                .AsUniTask();

            RelayServerData relayServerData = joinAllocation.ToRelayServerData(RelayConnectionType);
            transport.SetRelayServerData(relayServerData);

            approvalHandler.Register(networkManager);
            SetConnectionPayload(networkManager, roomId, localUserId);

            bool started = networkManager.StartClient();

            if (!started)
            {
                Debug.LogWarning("[RelayGameStartService] StartClient failed.");
                return false;
            }

            LastRelayJoinCode = relayJoinCode.Trim();

            Debug.Log($"[RelayGameStartService] Client started. JoinCode: {LastRelayJoinCode}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RelayGameStartService] StartClientWithRelayAsync failed: {e}");
            return false;
        }
    }

    public void Stop()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager != null)
        {
            approvalHandler.Unregister(networkManager);

            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        sessionRegistry.Clear();
        LastRelayJoinCode = "";
        ActiveSessionRegistry = null;

        Debug.Log("[RelayGameStartService] Stopped.");
    }

    private async UniTask<bool> EnsureServicesReadyAsync()
    {
        if (servicesInitializer == null)
        {
            Debug.LogWarning("[RelayGameStartService] UnityGameServicesInitializer is missing.");
            return false;
        }

        return await servicesInitializer.EnsureInitializedAsync();
    }

    private bool ValidateSession(string roomId, string localUserId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            Debug.LogWarning("[RelayGameStartService] RoomId is empty.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(localUserId))
        {
            Debug.LogWarning("[RelayGameStartService] LocalUserId is empty.");
            return false;
        }

        return true;
    }

    private UnityTransport GetUnityTransport(NetworkManager networkManager)
    {
        if (networkManager == null)
        {
            Debug.LogWarning("[RelayGameStartService] NetworkManager.Singleton is missing.");
            return null;
        }

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogWarning("[RelayGameStartService] UnityTransport is missing on NetworkManager.");
            return null;
        }

        return transport;
    }

    private void MakeNetworkManagerPersistent(NetworkManager networkManager)
    {
        if (networkManager == null)
        {
            return;
        }

        Object.DontDestroyOnLoad(networkManager.gameObject);
    }

    private void SetConnectionPayload(
        NetworkManager networkManager,
        string roomId,
        string localUserId)
    {
        NetworkConnectionPayload payload = new NetworkConnectionPayload(roomId, localUserId);
        networkManager.NetworkConfig.ConnectionData = payload.ToBytes();
    }
}
