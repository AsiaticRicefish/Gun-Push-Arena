using UnityEngine;
using Firebase.Firestore;

/// <summary>
/// rooms/{roomId}/players/{userId} 문서 하나를 표현합니다. 즉 “방 안의 플레이어 한 명 상태”입니다.
/// </summary>

[FirestoreData]
public class RoomPlayerState
{
    [FirestoreProperty] public string UserId { get; set; } // Firebase Auth UID
    [FirestoreProperty] public string Nickname { get; set; } // 로비 표시용으로 AuthManager.Instance.CurrentUserData.Nickname에서 가져옴

    [FirestoreProperty] public string ColorHex { get; set; } // 플레이어 색상(임시)

    [FirestoreProperty] public bool IsHost { get; set; }
    [FirestoreProperty] public bool IsReady { get; set; }

    [FirestoreProperty] public int SlotIndex { get; set; } // Player1Spawn, Player2Spawn 매칭 확인용 방장은 0, 참가자는 1

    [FirestoreProperty] public Timestamp JoinedAt { get; set; } // 입장 시간
    [FirestoreProperty] public Timestamp LastSeenAt { get; set; } // 추후 연결 끊김/오래된 유저 정리용
}