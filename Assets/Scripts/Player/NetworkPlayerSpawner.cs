using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Host 전용 플레이어 NetworkObject 스포너입니다.
/// Netcode clientId -> Firebase UserId -> RoomPlayerState.SlotIndex 순서로
/// 각 클라이언트의 PlayerObject를 맞는 스폰 위치에 생성합니다.
/// </summary>
public sealed class NetworkPlayerSpawner : MonoBehaviour
{
    private const int SpawnWaitTimeoutMs = 10000;

    private bool hasSpawned;

    public async UniTask SpawnPlayersAsync(
        IReadOnlyList<RoomPlayerState> players,
        GameMapSpawner mapSpawner,
        NetworkSessionRegistry sessionRegistry)
    {
        if (hasSpawned)
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsHost)
        {
            return;
        }

        if (players == null || mapSpawner == null || sessionRegistry == null)
        {
            Debug.LogWarning("[NetworkPlayerSpawner] Missing spawn data.");
            return;
        }

        // Host가 Map1을 먼저 로드할 수 있으므로, 참가자 approval과 연결 등록이 끝날 때까지 잠깐 기다립니다.
        bool ready = await WaitForPlayerConnectionsAsync(players, sessionRegistry, networkManager);
        if (!ready)
        {
            Debug.LogWarning("[NetworkPlayerSpawner] Timed out waiting for player connections.");
            return;
        }

        GameObject playerPrefab = networkManager.NetworkConfig.PlayerPrefab;
        if (playerPrefab == null)
        {
            Debug.LogWarning("[NetworkPlayerSpawner] NetworkManager PlayerPrefab is missing.");
            return;
        }

        foreach (RoomPlayerState player in players)
        {
            if (!sessionRegistry.TryGetClientId(player.UserId, out ulong clientId))
            {
                Debug.LogWarning($"[NetworkPlayerSpawner] ClientId not found. UserId: {player.UserId}");
                continue;
            }

            if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient networkClient))
            {
                Debug.LogWarning($"[NetworkPlayerSpawner] Connected client not found. ClientId: {clientId}");
                continue;
            }

            if (networkClient.PlayerObject != null)
            {
                Debug.Log($"[NetworkPlayerSpawner] PlayerObject already exists. ClientId: {clientId}");
                continue;
            }

            Vector3 spawnPosition = mapSpawner.GetSpawnWorldPosition(player.SlotIndex);
            GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogWarning("[NetworkPlayerSpawner] PlayerPrefab does not have NetworkObject.");
                Destroy(playerObject);
                continue;
            }

            // SpawnAsPlayerObject를 사용해야 해당 클라이언트의 IsOwner가 true가 되고 입력을 받을 수 있습니다.
            networkObject.SpawnAsPlayerObject(clientId);

            Debug.Log($"[NetworkPlayerSpawner] Spawned player. ClientId: {clientId}, UserId: {player.UserId}, SlotIndex: {player.SlotIndex}, Position: {spawnPosition}");
        }

        hasSpawned = true;
    }

    private async UniTask<bool> WaitForPlayerConnectionsAsync(
        IReadOnlyList<RoomPlayerState> players,
        NetworkSessionRegistry sessionRegistry,
        NetworkManager networkManager)
    {
        float startTime = Time.realtimeSinceStartup;
        float timeoutSeconds = SpawnWaitTimeoutMs / 1000f;

        while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
        {
            if (AreAllPlayersConnected(players, sessionRegistry, networkManager))
            {
                return true;
            }

            await UniTask.Yield();
        }

        return AreAllPlayersConnected(players, sessionRegistry, networkManager);
    }

    private bool AreAllPlayersConnected(
        IReadOnlyList<RoomPlayerState> players,
        NetworkSessionRegistry sessionRegistry,
        NetworkManager networkManager)
    {
        foreach (RoomPlayerState player in players)
        {
            if (!sessionRegistry.TryGetClientId(player.UserId, out ulong clientId))
            {
                return false;
            }

            if (!networkManager.ConnectedClients.ContainsKey(clientId))
            {
                return false;
            }
        }

        return true;
    }
}
