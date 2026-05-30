using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// Lobby UI의 Unity 이벤트 어댑터입니다.
/// Inspector에 연결된 버튼/드롭다운 이벤트를 Presenter 호출로 넘기는 역할만 담당합니다.
/// </summary>
[RequireComponent(typeof(LobbyUIView))]
public class LobbyUIController : MonoBehaviour
{
    // 실제 텍스트, 버튼, 패널 표시를 담당하는 View입니다.
    [SerializeField] private LobbyUIView view;

    [Header("Map")]
    // 방장이 AI 맵을 생성할 때 사용할 검증 설정입니다.
    // Inspector에서 MapValidationSettings.asset을 연결해야 합니다.
    [SerializeField] private MapValidationSettings validationSettings;

    [Header("Scene")]
    // 지금은 Map1을 게임 씬으로 사용합니다. 나중에 씬 이름을 바꾸면 Inspector나 여기 기본값을 수정하면 됩니다.
    [SerializeField] private string gameSceneName = "Map1";

    // Presenter가 씬 이동을 요청할 수 있도록 주입하는 씬 로더입니다.
    // 인스펙터에 연결하지 않아도 Awake에서 자동으로 찾거나 추가합니다.
    [SerializeField] private SceneLoader sceneLoader;

    // 로비 화면의 흐름과 상태 갱신을 담당합니다.
    private LobbyPresenter presenter;
    private RelayGameStartService relayGameStartService;

    private void Awake()
    {
        // 같은 GameObject에 LobbyUIView가 붙어 있으면 자동으로 찾아 씁니다.
        if (view == null)
        {
            view = GetComponent<LobbyUIView>();
        }

        if (sceneLoader == null)
        {
            sceneLoader = GetComponent<SceneLoader>();
        }

        if (sceneLoader == null)
        {
            // Lobby 오브젝트에 SceneLoader가 없으면 런타임에 붙여 Presenter 의존성을 만족시킵니다.
            sceneLoader = gameObject.AddComponent<SceneLoader>();
        }

        if (GameSessionContext.Instance == null)
        {
            // LobbyScene에서 Map1로 넘어갈 때 RoomId를 보존해야 하므로 DontDestroyOnLoad 싱글톤으로 생성합니다.
            new GameObject(nameof(GameSessionContext)).AddComponent<GameSessionContext>();
        }
    }

    private void Start()
    {
        // Repository는 Firestore에 직접 읽고 쓰는 계층입니다.
        IRoomRepository roomRepository = new FirestoreRoomRepository();

        // RoomService는 현재 유저 기준의 방 규칙과 listener 관리를 담당합니다.
        RoomService roomService = new RoomService(roomRepository, AuthManager.Instance);

        // RoomMapService는 현재 방 설정을 기준으로 AI 맵을 생성하고 최종 맵을 room에 저장합니다.
        RoomMapService roomMapService = new RoomMapService(
            roomService,
            validationSettings,
            theme => new OllamaAiMapClient(theme));

        UnityGameServicesInitializer unityGameServicesInitializer = new UnityGameServicesInitializer();
        NetworkSessionRegistry networkSessionRegistry = new NetworkSessionRegistry();
        NetworkConnectionApprovalHandler approvalHandler = new NetworkConnectionApprovalHandler(networkSessionRegistry);
        relayGameStartService = new RelayGameStartService(
            unityGameServicesInitializer,
            networkSessionRegistry,
            approvalHandler);

        // Presenter는 View, Service, MapService, Auth를 조합해서 로비 화면 흐름을 제어합니다.
        // 씬 이동에 필요한 SceneLoader와 게임 씬 이름도 Presenter에 주입합니다.
        presenter = new LobbyPresenter(
            view,
            roomService,
            roomMapService,
            AuthManager.Instance,
            sceneLoader,
            gameSceneName,
            relayGameStartService);
        presenter.Initialize();
    }

    private void OnDestroy()
    {
        // 씬 이동/오브젝트 파괴 시 Firestore listener와 이벤트 구독을 정리합니다.
        presenter?.Dispose();
    }

    public void OnClickCreateRoom()
    {
        // Unity 버튼 이벤트에서는 await를 직접 쓰기보다 UniTask를 실행하고 Forget으로 흘려보냅니다.
        // 내부 예외 처리는 Presenter의 RunBusyAsync에서 담당합니다.
        presenter.HandleCreateRoomAsync().Forget();
    }

    public void OnClickJoinRoom()
    {
        // 방 코드 입력값 검증은 Presenter에서 처리합니다.
        presenter.HandleJoinRoomAsync().Forget();
    }

    public void OnClickLeaveRoom()
    {
        // 방 나가기와 listener 정리는 RoomService/Presenter 쪽에서 처리합니다.
        presenter.HandleLeaveRoomAsync().Forget();
    }

    public void OnClickCopyRoomCode()
    {
        // 방 코드 복사는 Firestore 상태 변경이 아니라 순수 UI 기능이므로 View에 바로 위임합니다.
        view.CopyRoomCodeToClipboard();
    }

    public void OnClickReady()
    {
        // 참가자의 ready 토글 요청입니다. 방장은 Presenter에서 무시합니다.
        presenter.HandleToggleReadyAsync().Forget();
    }

    public void OnClickGenerateMap()
    {
        // 방장 맵 생성 요청입니다. 실제 생성/저장은 RoomMapService가 담당합니다.
        presenter.HandleGenerateMapAsync().Forget();
    }

    public void OnClickStartGame()
    {
        // 시작 가능 여부는 Presenter/RoomService에서 검사합니다.
        presenter.HandleStartGameAsync().Forget();
    }

    public void OnThemeChanged(int index)
    {
        // TMP_Dropdown의 index를 AiMapTheme으로 바꾸는 작업은 Presenter가 담당합니다.
        presenter.HandleThemeChangedAsync(index).Forget();
    }

}
