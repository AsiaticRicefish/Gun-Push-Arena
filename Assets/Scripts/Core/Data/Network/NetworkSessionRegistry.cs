using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Host 런타임에서 Netcode clientId와 Firebase UserId의 매핑을 보관합니다.
/// ConnectionApproval에서 등록하고, NetworkObject 스폰 시 clientId -> userId -> SlotIndex 순서로 스폰 위치를 찾습니다.
/// </summary>
public sealed class NetworkSessionRegistry
{
    // Netcode가 부여한 연결 ID로 Firebase Auth UID를 찾기 위한 정방향 매핑입니다.
    private readonly Dictionary<ulong, string> clientIdToUserId = new Dictionary<ulong, string>();

    // Firebase Auth UID로 Netcode 연결 ID를 찾기 위한 역방향 매핑입니다.
    private readonly Dictionary<string, ulong> userIdToClientId = new Dictionary<string, ulong>();

    public int Count => clientIdToUserId.Count;
    public IReadOnlyDictionary<ulong, string> ClientIdToUserId => clientIdToUserId;

    /// <summary>
    /// 승인된 연결의 clientId와 payload.UserId를 등록합니다.
    /// 같은 clientId 또는 userId가 다시 들어오면 기존 매핑을 정리하고 최신 값으로 교체합니다.
    /// </summary>
    public bool Register(ulong clientId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            Debug.LogWarning($"[NetworkSessionRegistry] Cannot register empty userId. ClientId: {clientId}");
            return false;
        }

        string normalizedUserId = userId.Trim();

        if (clientIdToUserId.TryGetValue(clientId, out string existingUserId))
        {
            userIdToClientId.Remove(existingUserId);
        }

        if (userIdToClientId.TryGetValue(normalizedUserId, out ulong existingClientId) &&
            existingClientId != clientId)
        {
            clientIdToUserId.Remove(existingClientId);
            Debug.LogWarning($"[NetworkSessionRegistry] UserId was already registered by another client. OldClientId: {existingClientId}, NewClientId: {clientId}, UserId: {normalizedUserId}");
        }

        clientIdToUserId[clientId] = normalizedUserId;
        userIdToClientId[normalizedUserId] = clientId;

        Debug.Log($"[NetworkSessionRegistry] Registered. ClientId: {clientId}, UserId: {normalizedUserId}");
        return true;
    }

    public bool TryGetUserId(ulong clientId, out string userId)
    {
        return clientIdToUserId.TryGetValue(clientId, out userId);
    }

    public bool TryGetClientId(string userId, out ulong clientId)
    {
        clientId = default;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return userIdToClientId.TryGetValue(userId.Trim(), out clientId);
    }

    public bool ContainsClient(ulong clientId)
    {
        return clientIdToUserId.ContainsKey(clientId);
    }

    public bool ContainsUser(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) &&
               userIdToClientId.ContainsKey(userId.Trim());
    }

    /// <summary>
    /// 클라이언트 연결이 끊겼을 때 clientId 기준으로 매핑을 제거합니다.
    /// </summary>
    public bool UnregisterClient(ulong clientId)
    {
        if (!clientIdToUserId.TryGetValue(clientId, out string userId))
        {
            return false;
        }

        clientIdToUserId.Remove(clientId);
        userIdToClientId.Remove(userId);

        Debug.Log($"[NetworkSessionRegistry] Unregistered. ClientId: {clientId}, UserId: {userId}");
        return true;
    }

    public bool UnregisterUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        string normalizedUserId = userId.Trim();

        if (!userIdToClientId.TryGetValue(normalizedUserId, out ulong clientId))
        {
            return false;
        }

        userIdToClientId.Remove(normalizedUserId);
        clientIdToUserId.Remove(clientId);

        Debug.Log($"[NetworkSessionRegistry] Unregistered. ClientId: {clientId}, UserId: {normalizedUserId}");
        return true;
    }

    /// <summary>
    /// 게임 종료, 로비 복귀, Host 종료 시 현재 네트워크 세션 매핑을 모두 비웁니다.
    /// </summary>
    public void Clear()
    {
        clientIdToUserId.Clear();
        userIdToClientId.Clear();

        Debug.Log("[NetworkSessionRegistry] Cleared.");
    }
}
