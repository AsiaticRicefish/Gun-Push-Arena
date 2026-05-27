using UnityEngine;

/// <summary>
/// LobbyScene에서 GameScene(Map1)으로 넘어갈 때 필요한 런타임 세션 정보입니다.
/// Firestore room 전체 데이터를 들고 가지 않고, 다음 씬에서 다시 조회할 수 있는 최소 식별자만 보관합니다.
/// </summary>
public class GameSessionContext : GlobalSingleton<GameSessionContext>
{
    // Firestore rooms/{RoomId} 문서를 다시 읽기 위한 방 ID입니다.
    public string RoomId { get; private set; }

    // 현재 클라이언트의 Firebase Auth UID입니다.
    // Netcode ConnectionData payload와 rooms/{roomId}/players 조회 기준으로 사용합니다.
    public string LocalUserId { get; private set; }

    // 방장의 Firebase Auth UID입니다.
    // 현재 클라이언트가 Host인지 판단하고, Relay/Netcode 시작 흐름을 분기할 때 사용합니다.
    public string HostUserId { get; private set; }

    public bool HasSession => !string.IsNullOrEmpty(RoomId);

    public bool IsHost => !string.IsNullOrEmpty(LocalUserId) &&
                          !string.IsNullOrEmpty(HostUserId) &&
                          LocalUserId == HostUserId;

    /// <summary>
    /// 로비에서 게임 시작 또는 참가 흐름으로 진입하기 직전에 호출합니다.
    /// 저장된 값은 Map1에서 finalMap 로드, Relay/Netcode 연결, SlotIndex 기반 스폰에 재사용됩니다.
    /// </summary>
    public void SetSession(
        string roomId,
        string localUserId,
        string hostUserId)
    {
        RoomId = roomId;
        LocalUserId = localUserId;
        HostUserId = hostUserId;

        Debug.Log($"[GameSessionContext] Session set. RoomId: {RoomId}, LocalUserId: {LocalUserId}, HostUserId: {HostUserId}, IsHost: {IsHost}");
    }

    /// <summary>
    /// 게임 종료, 로비 복귀, 방 나가기처럼 현재 방 문맥이 끝날 때 세션 정보를 초기화합니다.
    /// </summary>
    public void ClearSession()
    {
        Debug.Log("[GameSessionContext] Session cleared.");

        RoomId = "";
        LocalUserId = "";
        HostUserId = "";
    }
}
