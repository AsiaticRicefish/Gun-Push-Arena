using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Lobby MVP의 Presenter입니다.
/// View에서 받은 입력을 검증하고, RoomService를 호출한 뒤, 변경된 상태를 다시 View에 반영합니다.
/// </summary>
public class LobbyPresenter
{
    // 화면 표시 전용 객체입니다. Presenter는 UI 컴포넌트를 직접 만지지 않고 View 메서드만 호출합니다.
    private readonly LobbyUIView view;

    // 방 생성/참가/권한 체크/listener 관리를 담당하는 도메인 서비스입니다.
    private readonly RoomService roomService;

    // 방장의 AI 맵 생성과 최종 맵 저장을 담당하는 서비스입니다.
    private readonly RoomMapService roomMapService;

    // 현재 로그인 유저 정보 표시와 내 플레이어 판별에 사용합니다.
    private readonly IAuthService authService;

    // RoomStatus가 Starting으로 바뀌면 Presenter가 직접 씬 이동을 요청합니다.
    // SceneLoader를 의존성으로 받아 MVP 구조에서 Presenter가 MonoBehaviour에 직접 묶이지 않게 합니다.
    private readonly ISceneLoader sceneLoader;

    // 현재 1차 구현에서는 Map1을 게임 씬으로 사용합니다.
    private readonly string gameSceneName;

    private readonly RelayGameStartService relayGameStartService;

    // 중복 클릭으로 같은 비동기 작업이 여러 번 실행되는 것을 막기 위한 플래그입니다.
    private bool isBusy;

    // Firestore snapshot이 여러 번 들어와도 LoadSceneAsync가 중복 실행되지 않게 막습니다.
    private bool isMovingToGameScene;

    private bool isStartingRelayClient;

    // 현재 클라이언트가 알고 있는 내 ready 상태입니다.
    // Firestore player listener가 들어오면 서버 상태 기준으로 다시 동기화됩니다.
    private bool localReady;

    public LobbyPresenter(
        LobbyUIView view,
        RoomService roomService,
        RoomMapService roomMapService,
        IAuthService authService,
        ISceneLoader sceneLoader,
        string gameSceneName,
        RelayGameStartService relayGameStartService)
    {
        this.view = view;
        this.roomService = roomService;
        this.roomMapService = roomMapService;
        this.authService = authService;
        this.sceneLoader = sceneLoader;
        this.gameSceneName = gameSceneName;
        this.relayGameStartService = relayGameStartService;
    }

    public void Initialize()
    {
        // View 초기 상태를 정리합니다.
        view.Initialize();

        // RoomService의 실시간 상태 변경 이벤트를 Presenter가 받아 View를 갱신합니다.
        roomService.RoomChanged += OnRoomChanged;
        roomService.PlayersChanged += OnPlayersChanged;
        roomService.ErrorOccurred += OnErrorOccurred;

        // 로비 상단의 내 닉네임/색상/UID 표시를 초기화합니다.
        ApplyUserInfo();

        // 현재 방 상태 기준으로 버튼/패널 활성 상태를 한번 계산합니다.
        RefreshViewState();

        view.SetStatus("로비 준비 완료.");
    }

    public void Dispose()
    {
        // Presenter가 사라질 때 이벤트 구독을 반드시 해제합니다.
        // 해제하지 않으면 씬 이동 후에도 이전 Presenter로 콜백이 들어올 수 있습니다.
        roomService.RoomChanged -= OnRoomChanged;
        roomService.PlayersChanged -= OnPlayersChanged;
        roomService.ErrorOccurred -= OnErrorOccurred;

        // Firestore 실시간 listener도 함께 정리합니다.
        roomService.StopListening();
    }

    public async UniTask HandleCreateRoomAsync()
    {
        // 이미 다른 작업 중이면 중복 클릭을 무시합니다.
        if (isBusy)
        {
            return;
        }

        // RunBusyAsync는 버튼 잠금, 예외 처리, 로딩 상태 해제를 공통으로 처리합니다.
        await RunBusyAsync(async () =>
        {
            view.SetStatus("방 생성 중...");

            // 현재 유저를 방장으로 하는 2인 방을 생성합니다.
            RoomState room = await roomService.CreateRoomAsync(2);

            if (room != null)
            {
                // 방장은 ready 대상에서 제외하거나 항상 ready로 볼 수 있으므로 true로 둡니다.
                localReady = true;
                view.SetStatus($"방이 생성되었습니다: {room.RoomId}");
            }
        });
    }

    public async UniTask HandleJoinRoomAsync()
    {
        if (isBusy)
        {
            return;
        }

        // View는 입력값만 제공하고, Presenter가 빈 값 검사를 담당합니다.
        string roomCode = view.RoomCodeInput;

        if (string.IsNullOrWhiteSpace(roomCode))
        {
            view.SetStatus("방 코드를 입력해 주세요.");
            return;
        }

        await RunBusyAsync(async () =>
        {
            view.SetStatus("방 참가 중...");

            // RoomService 내부에서 roomId 정규화, 유저 정보 조합, Repository 호출을 처리합니다.
            bool success = await roomService.JoinRoomAsync(roomCode);

            if (success)
            {
                // 참가자는 처음 입장 시 준비 전 상태입니다.
                localReady = false;
                view.SetReadyState(false);
                view.SetStatus("방에 참가했습니다.");
            }
            else
            {
                view.SetStatus("방 참가에 실패했습니다.");
            }
        });
    }

    public async UniTask HandleLeaveRoomAsync()
    {
        if (isBusy)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            view.SetStatus("방 나가는 중...");

            // RoomService가 Firestore player 문서 삭제와 listener 정리를 처리합니다.
            await roomService.LeaveRoomAsync();

            // 로컬 UI 상태도 방 밖 상태로 되돌립니다.
            localReady = false;
            view.SetReadyState(false);
            view.ClearRoomInfo();
            view.SetStatus("방에서 나갔습니다.");
        });
    }

    public async UniTask HandleToggleReadyAsync()
    {
        // 작업 중이거나 방에 들어가 있지 않으면 ready를 바꾸지 않습니다.
        if (isBusy || roomService.CurrentRoom == null)
        {
            return;
        }

        // 방장은 ready 버튼을 쓰지 않는 정책입니다.
        if (roomService.IsCurrentUserHost())
        {
            return;
        }

        // 먼저 화면을 즉시 바꾼 뒤 Firestore에 저장합니다.
        // 이후 listener가 다시 들어오면 Firestore 상태로 localReady가 재동기화됩니다.
        localReady = !localReady;
        view.SetReadyState(localReady);

        await roomService.UpdateReadyAsync(localReady);
    }

    public async UniTask HandleThemeChangedAsync(int index)
    {
        // 방장이 아니거나 작업 중이면 선택 변경을 서버에 반영하지 않습니다.
        if (isBusy || !roomService.CanHostControlRoom())
        {
            // View가 잘못 바뀌었을 수 있으므로 현재 room 상태 기준으로 다시 UI를 계산합니다.
            RefreshViewState();
            return;
        }

        // Dropdown index를 도메인 enum으로 변환합니다.
        AiMapTheme theme = ConvertIndexToTheme(index);
        await roomService.UpdateThemeAsync(theme);
    }

    public async UniTask HandleGenerateMapAsync()
    {
        // 방장만 최종 맵을 생성하고 저장할 수 있습니다.
        if (isBusy || !roomService.CanHostControlRoom())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            view.SetStatus("맵 생성 중...");

            // RoomMapService가 GeneratingMap 상태 전환, AI 맵 생성, 검증, DTO 변환, Firestore 저장까지 처리합니다.
            bool success = await roomMapService.GenerateAndSaveMapAsync();

            PlayLocalSfx(success ? SoundId.MapGenerateSuccess : SoundId.MapGenerateFail);
            view.SetStatus(success ? "맵 준비 완료." : "맵 생성에 실패했습니다.");
        });
    }

    public async UniTask HandleStartGameAsync()
    {
        if (isBusy)
        {
            return;
        }

        // 방장, finalMap, ready 상태 등 시작 조건은 RoomService에 모아둡니다.
        if (!roomService.CanStartGame())
        {
            view.SetStatus("아직 게임을 시작할 수 없습니다.");
            RefreshViewState();
            return;
        }

        await RunBusyAsync(async () =>
        {
            view.SetStatus("게임 시작 중...");
            PlayLocalSfx(SoundId.GameStart);

            // 현재는 room status를 Starting으로 바꾸는 단계입니다.
            // 다음 단계에서 Relay 생성과 Netcode 시작이 이어집니다.
            if (relayGameStartService == null)
            {
                view.SetStatus("Relay 서비스가 연결되어 있지 않습니다.");
                return;
            }

            RoomState room = roomService.CurrentRoom;
            GameSessionContext.Instance.SetSession(
                room.RoomId,
                authService.UserId,
                room.HostUserId);

            int maxClientConnections = Mathf.Max(1, room.MaxPlayers - 1);
            bool hostStarted = await relayGameStartService.StartHostWithRelayAsync(
                room.RoomId,
                authService.UserId,
                maxClientConnections);

            if (!hostStarted)
            {
                view.SetStatus("Relay 호스트 시작에 실패했습니다.");
                return;
            }

            await roomService.SetRelayJoinCodeAsync(relayGameStartService.LastRelayJoinCode);

            await roomService.StartGameAsync();
        });
    }

    private void OnRoomChanged(RoomState room)
    {
        // 방이 삭제되었거나 Closed 처리 후 null로 전달된 경우 UI를 방 밖 상태로 정리합니다.
        if (room == null)
        {
            view.ClearRoomInfo();
            RefreshViewState();
            return;
        }

        // room 문서의 최신 상태를 화면에 반영합니다.
        view.SetRoomInfo(room);

        // 방장이 StartGame을 누르면 room.Status가 Starting으로 저장됩니다.
        // 방장과 참가자 모두 같은 Firestore 변경을 감지해서 동일한 타이밍에 Map1으로 이동합니다.
        if (room.Status == RoomStatus.Starting.ToString())
        {
            if (roomService.IsCurrentUserHost())
            {
                MoveToGameSceneAsync(room).Forget();
            }
            else
            {
                StartRelayClientAndMoveAsync(room).Forget();
            }

            return;
        }

        // Firestore에는 theme이 문자열로 저장되므로 enum으로 파싱해서 dropdown에 반영합니다.
        if (Enum.TryParse(room.SelectedTheme, out AiMapTheme theme))
        {
            view.SetTheme(theme);
        }

        RefreshViewState();
    }

    private async UniTask MoveToGameSceneAsync(RoomState room)
    {
        // Starting snapshot이 반복 수신되어도 씬 이동은 한 번만 진행합니다.
        if (isMovingToGameScene)
        {
            return;
        }

        if (room == null)
        {
            view.SetStatus("게임을 시작할 수 없습니다. 방 정보가 없습니다.");
            return;
        }

        if (room.FinalMap == null)
        {
            view.SetStatus("게임을 시작할 수 없습니다. 최종 맵이 없습니다.");
            return;
        }

        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            view.SetStatus("게임을 시작할 수 없습니다. 게임 씬 이름이 비어 있습니다.");
            return;
        }

        if (sceneLoader == null)
        {
            view.SetStatus("게임을 시작할 수 없습니다. SceneLoader가 없습니다.");
            return;
        }

        if (GameSessionContext.Instance == null)
        {
            view.SetStatus("게임을 시작할 수 없습니다. GameSessionContext가 없습니다.");
            return;
        }

        isMovingToGameScene = true;

        try
        {
            // 게임 씬에는 RoomService 인스턴스를 넘기지 않고, roomId와 사용자 식별자만 넘깁니다.
            // Map1에서는 이 RoomId로 Firestore room을 다시 읽어 finalMap을 로드합니다.
            GameSessionContext.Instance.SetSession(
                room.RoomId,
                authService.UserId,
                room.HostUserId);

            view.SetStatus("게임 씬 불러오는 중...");
            // 씬 이동 전에 로비 listener를 끊어 이전 Presenter가 추가 콜백을 받지 않게 합니다.
            roomService.StopListening();

            await sceneLoader.LoadSceneAsync(gameSceneName);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyPresenter] Failed to load game scene: {e}");
            view.SetStatus("게임 씬을 불러오지 못했습니다.");
            isMovingToGameScene = false;
        }
    }

    private async UniTask StartRelayClientAndMoveAsync(RoomState room)
    {
        if (isStartingRelayClient || isMovingToGameScene)
        {
            return;
        }

        if (room == null || string.IsNullOrWhiteSpace(room.RelayJoinCode))
        {
            view.SetStatus("Relay 참가 코드를 기다리는 중...");
            return;
        }

        if (relayGameStartService == null)
        {
            view.SetStatus("Relay 서비스가 연결되어 있지 않습니다.");
            return;
        }

        if (GameSessionContext.Instance == null)
        {
            view.SetStatus("클라이언트를 시작할 수 없습니다. GameSessionContext가 없습니다.");
            return;
        }

        isStartingRelayClient = true;

        try
        {
            GameSessionContext.Instance.SetSession(
                room.RoomId,
                authService.UserId,
                room.HostUserId);

            view.SetStatus("Relay 참가 중...");

            bool clientStarted = await relayGameStartService.StartClientWithRelayAsync(
                room.RoomId,
                authService.UserId,
                room.RelayJoinCode);

            if (!clientStarted)
            {
                view.SetStatus("Relay 참가에 실패했습니다.");
                isStartingRelayClient = false;
                return;
            }

            await MoveToGameSceneAsync(room);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyPresenter] Failed to start relay client: {e}");
            view.SetStatus("Relay 참가에 실패했습니다.");
            isStartingRelayClient = false;
        }
    }

    private void OnPlayersChanged(IReadOnlyList<RoomPlayerState> players)
    {
        // 플레이어 목록 표시에는 hostUserId가 필요합니다.
        string hostUserId = roomService.CurrentRoom != null
            ? roomService.CurrentRoom.HostUserId
            : "";

        view.SetPlayers(players, hostUserId);

        // 내 player 문서를 찾아 ready 버튼 텍스트를 Firestore 상태 기준으로 맞춥니다.
        RoomPlayerState myPlayer = FindCurrentPlayer(players);
        if (myPlayer != null)
        {
            localReady = myPlayer.IsReady;
            view.SetReadyState(localReady);
        }

        RefreshViewState();
    }

    private void OnErrorOccurred(string message)
    {
        // Service/Repository 계층에서 올라온 에러를 상태 메시지로 보여줍니다.
        view.SetStatus(message);
        RefreshViewState();
    }

    private void ApplyUserInfo()
    {
        // 로그인 직후 로비에 진입했다면 CurrentUserData에 닉네임/색상이 들어 있어야 합니다.
        UserData userData = authService.CurrentUserData;

        if (userData == null)
        {
            view.SetUserInfo("알 수 없음", "#FFFFFF", authService.UserId);
            return;
        }

        view.SetUserInfo(
            userData.Nickname,
            userData.ColorHex,
            authService.UserId);
    }

    private void RefreshViewState()
    {
        // 화면 버튼 상태는 매번 현재 room/service 상태에서 다시 계산합니다.
        // 이렇게 해야 listener로 상태가 바뀌어도 UI가 일관되게 따라옵니다.
        bool inRoom = roomService.CurrentRoom != null;
        bool isHost = roomService.IsCurrentUserHost();
        bool canHostControl = roomService.CanHostControlRoom();

        // 작업 중이면 로딩 표시를 켜고 대부분의 버튼을 잠급니다.
        view.SetLoading(isBusy);

        // 방 밖에서는 생성/참가 UI를 사용할 수 있고, 방 안에서는 잠급니다.
        view.SetCreateJoinInteractable(!isBusy && !inRoom);

        // 방에 들어와 있을 때만 room panel과 leave 같은 room 액션을 보여줍니다.
        view.SetRoomControlsVisible(inRoom);
        view.SetHostControlsVisible(inRoom && canHostControl);

        // 방장이 아닌 참가자만 ready 버튼을 사용할 수 있습니다.
        view.SetReadyVisible(inRoom && !isHost);
        view.SetReadyInteractable(!isBusy && inRoom && !isHost);

        // 방장 전용 기능은 RoomService의 권한 체크 결과를 따릅니다.
        view.SetStartGameVisible(inRoom && isHost);
        view.SetGenerateMapInteractable(!isBusy && canHostControl);
        view.SetStartGameInteractable(!isBusy && roomService.CanStartGame());
        view.SetRoomActionInteractable(!isBusy && inRoom);
    }

    private async UniTask RunBusyAsync(Func<UniTask> action)
    {
        // 공통 비동기 실행 래퍼입니다.
        // 시작 시 UI를 잠그고, 예외가 나도 finally에서 반드시 복구합니다.
        isBusy = true;
        RefreshViewState();

        try
        {
            await action();
        }
        catch (Exception e)
        {
            // Presenter 단계에서 잡은 예외는 로그와 상태 메시지로 남깁니다.
            Debug.LogError($"[LobbyPresenter] {e}");
            view.SetStatus(e.Message);
        }
        finally
        {
            isBusy = false;
            RefreshViewState();
        }
    }

    private RoomPlayerState FindCurrentPlayer(IReadOnlyList<RoomPlayerState> players)
    {
        // players snapshot에서 현재 로그인 유저의 player 문서를 찾습니다.
        if (players == null)
        {
            return null;
        }

        foreach (RoomPlayerState player in players)
        {
            if (player.UserId == authService.UserId)
            {
                return player;
            }
        }

        return null;
    }

    private void PlayLocalSfx(SoundId soundId)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(soundId);
        }
    }

    private AiMapTheme ConvertIndexToTheme(int index)
    {
        // TMP_Dropdown의 옵션 순서와 AiMapTheme enum을 연결합니다.
        // Dropdown 옵션 순서를 바꾸면 이 매핑도 함께 바꿔야 합니다.
        switch (index)
        {
            case 1:
                return AiMapTheme.Split;
            case 2:
                return AiMapTheme.Vertical;
            case 3:
                return AiMapTheme.Chaos;
            default:
                return AiMapTheme.Balanced;
        }
    }
}
