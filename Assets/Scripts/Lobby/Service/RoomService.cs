using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// UI와 Repository 사이에서 방 관련 게임 규칙을 처리하는 클래스입니다.
/// 누가 방장인지, 지금 이 버튼을 눌러도 되는지, 현재 유저 정보로 어떤 Repository 메서드를 호출할지 판단합니다.
/// </summary>
public class RoomService
{
    // Firestore 읽기/쓰기 자체는 Repository가 담당합니다.
    // Service는 Repository를 호출하기 전에 현재 유저 기준의 규칙을 검사합니다.
    private readonly IRoomRepository roomRepository;

    // 현재 로그인한 유저의 UID와 로그인 여부를 확인하기 위해 사용합니다.
    private readonly IAuthService authService;

    // Firestore 실시간 구독을 해제하기 위해 보관하는 핸들입니다.
    // 방을 나가거나 씬이 바뀔 때 Stop()을 호출해야 중복 콜백과 메모리 누수를 막을 수 있습니다.
    private ListenerRegistration roomListener;
    private ListenerRegistration playersListener;

    // 현재 내가 들어가 있는 방의 최신 상태입니다.
    // ListenRoom 콜백이 들어올 때마다 갱신됩니다.
    public RoomState CurrentRoom { get; private set; }

    // 현재 방에 들어와 있는 플레이어 목록입니다.
    // ListenPlayers 콜백이 들어올 때마다 갱신됩니다.
    public IReadOnlyList<RoomPlayerState> CurrentPlayers { get; private set; }

    // UI는 이 이벤트를 구독해서 방 상태 텍스트, 테마, 버튼 활성화 등을 갱신합니다.
    public event Action<RoomState> RoomChanged;

    // UI는 이 이벤트를 구독해서 플레이어 목록과 ready 상태를 갱신합니다.
    public event Action<IReadOnlyList<RoomPlayerState>> PlayersChanged;

    // UI에서 경고 텍스트를 띄우고 싶을 때 사용할 에러 이벤트입니다.
    public event Action<string> ErrorOccurred;

    public RoomService(IRoomRepository roomRepository, IAuthService authService)
    {
        this.roomRepository = roomRepository;
        this.authService = authService;
        CurrentPlayers = new List<RoomPlayerState>();
    }

    public async UniTask<RoomState> CreateRoomAsync(int maxPlayers = 2)
    {
        // 방 생성은 로그인한 유저만 할 수 있습니다.
        if (!authService.IsLoggedIn)
        {
            NotifyError("Login is required.");
            return null;
        }

        // 현재 프로젝트에서는 닉네임/색상 데이터가 AuthManager에 들어 있으므로 여기서 가져옵니다.
        // 나중에 IAuthService에 CurrentUserData를 추가하면 AuthManager 직접 참조를 줄일 수 있습니다.
        UserData userData = AuthManager.Instance.CurrentUserData;

        // Repository는 실제 Firestore room 문서와 host player 문서를 생성합니다.
        // Firestore SDK는 Task 기반이므로 AsUniTask()로 UniTask 흐름에 맞춥니다.
        RoomState room = await roomRepository.CreateRoomAsync(
            authService.UserId,
            userData.Nickname,
            userData.ColorHex,
            maxPlayers).AsUniTask();

        // 방 생성에 성공하면 해당 방의 room 문서와 players 컬렉션을 실시간 구독합니다.
        StartListening(room.RoomId);
        return room;
    }

    public async UniTask<bool> JoinRoomAsync(string roomId)
    {
        // 방 참가도 로그인 상태가 필요합니다.
        if (!authService.IsLoggedIn)
        {
            NotifyError("Login is required.");
            return false;
        }

        // 빈 방 코드를 Firestore에 요청하지 않도록 먼저 막습니다.
        if (string.IsNullOrWhiteSpace(roomId))
        {
            NotifyError("Room code is empty.");
            return false;
        }

        // 방 코드는 대소문자 입력 차이를 줄이기 위해 대문자로 정규화합니다.
        string normalizedRoomId = roomId.Trim().ToUpperInvariant();
        UserData userData = AuthManager.Instance.CurrentUserData;

        // Repository에서 방 존재 여부, 인원 초과, 입장 가능 상태를 검사하고 player 문서를 추가합니다.
        bool success = await roomRepository.JoinRoomAsync(
            normalizedRoomId,
            authService.UserId,
            userData.Nickname,
            userData.ColorHex).AsUniTask();

        // 참가에 성공한 경우에만 listener를 시작합니다.
        if (success)
        {
            StartListening(normalizedRoomId);
        }

        return success;
    }

    public async UniTask LeaveRoomAsync()
    {
        // 현재 방이 없으면 나갈 것도 없습니다.
        if (CurrentRoom == null)
        {
            return;
        }

        string roomId = CurrentRoom.RoomId;

        // Firestore에 나가기 요청을 보내기 전에 먼저 listener를 끊습니다.
        // 나가는 중 들어오는 마지막 snapshot으로 UI가 흔들리는 것을 줄이기 위함입니다.
        StopListening();

        await roomRepository.LeaveRoomAsync(
            roomId,
            authService.UserId).AsUniTask();

        // 로컬 상태를 비우고 UI에도 빈 상태를 알립니다.
        CurrentRoom = null;
        CurrentPlayers = new List<RoomPlayerState>();

        RoomChanged?.Invoke(null);
        PlayersChanged?.Invoke(CurrentPlayers);
    }

    public async UniTask UpdateThemeAsync(AiMapTheme theme)
    {
        // 맵 테마 변경은 방장만 가능합니다.
        if (!CanHostControlRoom())
        {
            NotifyError("Only host can change theme.");
            return;
        }

        await roomRepository.UpdateThemeAsync(
            CurrentRoom.RoomId,
            theme).AsUniTask();
    }

    public async UniTask UpdateReadyAsync(bool isReady)
    {
        // ready는 현재 방에 들어와 있을 때만 변경할 수 있습니다.
        if (CurrentRoom == null)
        {
            NotifyError("You are not in a room.");
            return;
        }

        await roomRepository.UpdateReadyAsync(
            CurrentRoom.RoomId,
            authService.UserId,
            isReady).AsUniTask();
    }

    public async UniTask SetGeneratingMapAsync()
    {
        // AI 맵 생성 시작 상태로 바꾸는 것도 방장 전용입니다.
        if (!CanHostControlRoom())
        {
            NotifyError("Only host can generate map.");
            return;
        }

        await roomRepository.SetRoomStatusAsync(
            CurrentRoom.RoomId,
            RoomStatus.GeneratingMap).AsUniTask();
    }

    public async UniTask SaveFinalMapAsync(AiMapLayoutDto finalMap)
    {
        // 최종 맵 저장은 모든 클라이언트가 같은 맵을 쓰게 만드는 핵심 작업이므로 방장만 허용합니다.
        if (!CanHostControlRoom())
        {
            NotifyError("Only host can save final map.");
            return;
        }

        // null 맵이 저장되면 다른 클라이언트가 맵을 만들 수 없으므로 미리 방어합니다.
        if (finalMap == null)
        {
            NotifyError("Final map is null.");
            return;
        }

        await roomRepository.SaveFinalMapAsync(
            CurrentRoom.RoomId,
            finalMap).AsUniTask();
    }

    public async UniTask SetRelayJoinCodeAsync(string relayJoinCode)
    {
        // Relay Join Code는 게임 시작 단계에서 방장이 생성해서 공유하는 값입니다.
        if (!CanHostControlRoom())
        {
            NotifyError("Only host can set relay join code.");
            return;
        }

        await roomRepository.SetRelayJoinCodeAsync(
            CurrentRoom.RoomId,
            relayJoinCode).AsUniTask();
    }

    public async UniTask StartGameAsync()
    {
        // 시작 조건은 CanStartGame에 모아둡니다.
        // UI 버튼 활성화 조건과 실제 실행 조건이 같은 기준을 쓰게 하기 위함입니다.
        if (!CanStartGame())
        {
            NotifyError("Cannot start game yet.");
            return;
        }

        await roomRepository.SetRoomStatusAsync(
            CurrentRoom.RoomId,
            RoomStatus.Starting).AsUniTask();
    }

    public bool IsCurrentUserHost()
    {
        // 실제 권한 판단은 RoomPlayerState.IsHost보다 room의 HostUserId 기준이 더 안전합니다.
        return CurrentRoom != null &&
               CurrentRoom.HostUserId == authService.UserId;
    }

    public bool CanHostControlRoom()
    {
        // 방장이면서, 이미 닫혔거나 게임 중인 방이 아닐 때만 설정을 바꿀 수 있습니다.
        return CurrentRoom != null &&
               IsCurrentUserHost() &&
               CurrentRoom.Status != RoomStatus.Closed.ToString() &&
               CurrentRoom.Status != RoomStatus.InGame.ToString();
    }

    public bool CanStartGame()
    {
        // 게임 시작은 방장만 할 수 있습니다.
        if (!IsCurrentUserHost())
        {
            return false;
        }

        // 최종 맵이 있어야 하고, 방 상태가 MapReady여야 시작할 수 있습니다.
        if (CurrentRoom == null ||
            CurrentRoom.FinalMap == null ||
            CurrentRoom.Status != RoomStatus.MapReady.ToString())
        {
            return false;
        }

        // 현재는 2인 기준 프로토타입이므로 최소 2명 이상일 때만 시작합니다.
        if (CurrentPlayers == null || CurrentPlayers.Count < 2)
        {
            return false;
        }

        // 방장을 제외한 참가자들이 모두 ready 상태인지 확인합니다.
        foreach (RoomPlayerState player in CurrentPlayers)
        {
            if (player.UserId == CurrentRoom.HostUserId)
            {
                continue;
            }

            if (!player.IsReady)
            {
                return false;
            }
        }

        return true;
    }

    public void StartListening(string roomId)
    {
        // 같은 Service에서 다른 방을 구독 중일 수 있으므로, 기존 listener를 먼저 정리합니다.
        StopListening();

        // room 문서 하나를 구독합니다.
        // status, selectedTheme, finalMap 같은 방 전체 상태 변경을 받습니다.
        roomListener = roomRepository.ListenRoom(
            roomId,
            OnRoomChanged,
            OnListenerError);

        // players 하위 컬렉션을 구독합니다.
        // 입장/퇴장/ready 변경을 받습니다.
        playersListener = roomRepository.ListenPlayers(
            roomId,
            OnPlayersChanged,
            OnListenerError);
    }

    public void StopListening()
    {
        // ListenerRegistration.Stop()을 호출해야 Firestore 실시간 구독이 종료됩니다.
        roomListener?.Stop();
        roomListener = null;

        playersListener?.Stop();
        playersListener = null;
    }

    private void OnRoomChanged(RoomState room)
    {
        // Repository listener에서 받은 최신 room 상태를 Service 내부 상태로 보관합니다.
        CurrentRoom = room;

        // UI에게 방 상태가 바뀌었음을 알립니다.
        RoomChanged?.Invoke(CurrentRoom);
    }

    private void OnPlayersChanged(IReadOnlyList<RoomPlayerState> players)
    {
        // Repository listener에서 받은 최신 player 목록을 Service 내부 상태로 보관합니다.
        CurrentPlayers = players;

        // UI에게 플레이어 목록이 바뀌었음을 알립니다.
        PlayersChanged?.Invoke(CurrentPlayers);
    }

    private void OnListenerError(Exception exception)
    {
        // listener 안에서 발생한 예외는 Debug.Log와 UI 이벤트 양쪽으로 전달합니다.
        Debug.LogError($"[RoomService] Listener error: {exception}");
        NotifyError(exception.Message);
    }

    private void NotifyError(string message)
    {
        // Service 내부 경고를 로그로 남기고, UI가 표시할 수 있도록 이벤트로 전달합니다.
        Debug.LogWarning($"[RoomService] {message}");
        ErrorOccurred?.Invoke(message);
    }
}
