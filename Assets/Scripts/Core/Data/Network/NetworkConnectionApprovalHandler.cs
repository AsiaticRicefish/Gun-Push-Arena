using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Netcode 연결 승인 단계에서 클라이언트가 보낸 ConnectionData를 읽고,
/// Firebase UserId와 Netcode clientId를 연결합니다.
/// Relay를 사용해도 승인 흐름은 동일하므로 StartHost/StartClient 전에 등록해서 사용합니다.
/// </summary>
public sealed class NetworkConnectionApprovalHandler
{
    private readonly NetworkSessionRegistry sessionRegistry;

    public NetworkConnectionApprovalHandler(NetworkSessionRegistry sessionRegistry)
    {
        this.sessionRegistry = sessionRegistry;
    }

    /// <summary>
    /// NetworkManager의 ConnectionApprovalCallback에 연결합니다.
    /// 중복 등록을 피하기 위해 기존 콜백을 한 번 제거한 뒤 다시 등록합니다.
    /// </summary>
    public void Register(NetworkManager networkManager)
    {
        if (networkManager == null)
        {
            Debug.LogWarning("[NetworkConnectionApprovalHandler] NetworkManager is null.");
            return;
        }

        // 이 값이 true여야 클라이언트가 보낸 ConnectionData를 승인 콜백에서 읽을 수 있습니다.
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback -= OnConnectionApproval;
        networkManager.ConnectionApprovalCallback += OnConnectionApproval;

        Debug.Log("[NetworkConnectionApprovalHandler] Registered.");
    }

    public void Unregister(NetworkManager networkManager)
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.ConnectionApprovalCallback -= OnConnectionApproval;

        Debug.Log("[NetworkConnectionApprovalHandler] Unregistered.");
    }

    private void OnConnectionApproval(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        if (sessionRegistry == null)
        {
            Reject(response, "Session registry is missing.");
            return;
        }

        // 클라이언트가 NetworkConfig.ConnectionData에 넣은 RoomId/UserId payload를 읽습니다.
        if (!NetworkConnectionPayload.TryFromBytes(
                request.Payload,
                out NetworkConnectionPayload payload,
                out string errorMessage))
        {
            Reject(response, errorMessage);
            return;
        }

        // Host가 들고 있는 현재 방과 클라이언트가 요청한 방이 다르면 잘못된 접속으로 보고 거절합니다.
        if (GameSessionContext.Instance != null &&
            GameSessionContext.Instance.HasSession &&
            payload.RoomId != GameSessionContext.Instance.RoomId)
        {
            Reject(response, "RoomId does not match host session.");
            return;
        }

        // 승인된 연결의 clientId와 Firebase UserId를 저장해 이후 SlotIndex 기반 스폰에 사용합니다.
        bool registered = sessionRegistry.Register(
            request.ClientNetworkId,
            payload.UserId);

        if (!registered)
        {
            Reject(response, "Failed to register client session.");
            return;
        }

        response.Approved = true;

        // 자동 PlayerPrefab 생성을 끕니다.
        // Host가 RoomPlayerState.SlotIndex를 기준으로 SpawnAsPlayerObject를 직접 호출합니다.
        response.CreatePlayerObject = false;
        response.Pending = false;

        Debug.Log($"[NetworkConnectionApprovalHandler] Approved. ClientId: {request.ClientNetworkId}, UserId: {payload.UserId}, RoomId: {payload.RoomId}");
    }

    private void Reject(NetworkManager.ConnectionApprovalResponse response, string reason)
    {
        response.Approved = false;
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = reason ?? "Connection rejected.";

        Debug.LogWarning($"[NetworkConnectionApprovalHandler] Rejected. Reason: {response.Reason}");
    }
}
