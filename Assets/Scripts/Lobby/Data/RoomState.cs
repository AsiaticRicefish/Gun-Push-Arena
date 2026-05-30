using UnityEngine;
using Firebase.Firestore;

/// <summary>
/// rooms/{roomId} 문서 하나를 표현합니다.즉 “방 전체 상태”입니다
/// </summary>

[FirestoreData]
public class RoomState
{
    [FirestoreProperty] public string RoomId { get; set; } // 방 코드(UI에서 초대코드처럼 보여줄 값)
    [FirestoreProperty] public string HostUserId { get; set; } // 방장 UID(방장 권한 판단 기준)
    [FirestoreProperty] public string Status { get; set; } // 현재 방 상태(문자열로 저장해서 Firestore 콘솔에서 보기 쉽게 함)

    [FirestoreProperty] public int MaxPlayers { get; set; } // 최대 인원
    [FirestoreProperty] public int PlayerCount { get; set; } // 현재 인원
    
    [FirestoreProperty] public string SelectedTheme { get; set; } // 맵 테마
    [FirestoreProperty] public int MapWidth { get; set; }
    [FirestoreProperty] public int MapHeight { get; set; }
    [FirestoreProperty] public int MapVersion { get; set; } // 맵을 재생성할 때 증가함. 클라이언트가 새 맵인지 판단하기 좋음

    [FirestoreProperty] public AiMapLayoutDto FinalMap { get; set; } // 검증 완료된 최종 맵으로 클라이언트가 이 값을 사용

    [FirestoreProperty] public string RelayJoinCode { get; set; } // 나중에 StartGame 시 Relay 코드 저장

    [FirestoreProperty] public Timestamp CreatedAt { get; set; }
    [FirestoreProperty] public Timestamp UpdatedAt { get; set; }
}
