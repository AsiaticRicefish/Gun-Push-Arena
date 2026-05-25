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

    // 플레이어가 방을 나갈 때 호출
    Task LeaveRoomAsync(
        string roomId,
        string userId);

    // 선택된 맵 테마 변경
    Task UpdateThemeAsync(
        string roomId,
        AiMapTheme theme);

    // AI 옵션 힌트 변경
    Task UpdateAiStyleHintAsync(
        string roomId,
        string aiStyleHint);

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