using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;

/// <summary>
/// 방에 필요한 Firestore 기능
/// 나중에 저장 방식을 바꿔도 코드 수정을 좀 줄이기 위해 인터페이스로 진행
/// </summary>
public interface IRoomRepository
{
    // 방장이 방을 만들 때 호출
    Task<RoomState> CreateRoomAsync(
        string hostUserId,
        string nickname,
        string colorHex,
        int maxPlayers);


    // 참가자가 방 코드 입력 후 호출
    Task<bool> JoinRoomAsync(
        string roomId,
        string userId,
        string nickname,
        string colorHex);

    // 게임 씬에서 roomId만 가지고 rooms/{roomId} 문서를 한 번 읽기 위해 사용합니다.
    // finalMap 로드는 실시간 구독이 아니라 단건 조회로 충분하므로 별도 메서드로 분리합니다.
    Task<RoomState> GetRoomAsync(string roomId);

    // 게임 씬에서 SlotIndex 기준 플레이어 배치를 확인하기 위해 players 컬렉션을 한 번 읽습니다.
    // 실제 플레이 중 위치 동기화는 Firestore가 아니라 Netcode가 담당합니다.
    Task<IReadOnlyList<RoomPlayerState>> GetPlayersAsync(string roomId);

    // 플레이어가 방을 나갈 때 호출
    Task LeaveRoomAsync(
        string roomId,
        string userId);

    // 선택된 맵 테마 변경
    Task UpdateThemeAsync(
        string roomId,
        AiMapTheme theme);

    // 참가자의 ready 상태 변경
    Task UpdateReadyAsync(
        string roomId,
        string userId,
        bool isReady);

    // Waiting, GeneratingMap, MapReady, Starting 등 상태 변경
    Task SetRoomStatusAsync(
        string roomId,
        RoomStatus status);

    // 방장이 생성한 최종 맵을 room 문서에 저장
    Task SaveFinalMapAsync(
        string roomId,
        AiMapLayoutDto finalMap);

    Task SetRelayJoinCodeAsync(
        string roomId,
        string relayJoinCode);

    ListenerRegistration ListenRoom(
        string roomId,
        Action<RoomState> onChanged,
        Action<Exception> onError);

    ListenerRegistration ListenPlayers(
        string roomId,
        Action<IReadOnlyList<RoomPlayerState>> onChanged,
        Action<Exception> onError);
}
