using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Map1 게임 씬의 진입점입니다.
/// LobbyScene에서 저장한 GameSessionContext.RoomId를 기준으로 Firestore room 문서를 다시 읽고,
/// 저장된 FinalMap을 실제 게임 맵으로 스폰합니다.
/// </summary>
public class GameSceneBootstrap : MonoBehaviour
{
    // 현재 1차 구현에서는 Map1을 게임 씬으로 사용합니다.
    // 나중에 씬 이름을 GameScene으로 바꾸면 이 값도 함께 바꾸면 됩니다.
    private const string DefaultGameSceneName = "Map1";

    // 씬에 직접 배치해도 되고, 없으면 런타임에 자동 생성합니다.
    [SerializeField] private GameMapSpawner mapSpawner;
    [SerializeField] private GamePlayerPreviewSpawner playerPreviewSpawner;
    [SerializeField] private NetworkPlayerSpawner networkPlayerSpawner;
    [SerializeField] private GameRoundManager gameRoundManager;

    // Start가 여러 경로로 호출되더라도 Firestore 로드를 한 번만 진행하기 위한 방어 플래그입니다.
    private bool isLoading;

    // 모든 씬에 GameSceneBootstrap을 미리 배치하지 않아도 되도록 씬 로드 이벤트를 등록합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Map1이 로드되는 순간 Bootstrap 오브젝트를 자동으로 만들고 게임 초기화를 시작합니다.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != DefaultGameSceneName)
        {
            return;
        }

        if (FindFirstObjectByType<GameSceneBootstrap>() != null)
        {
            return;
        }

        new GameObject(nameof(GameSceneBootstrap)).AddComponent<GameSceneBootstrap>();
    }

    private void Start()
    {
        // Unity 생명주기 메서드에서는 await할 수 없으므로 UniTask를 fire-and-forget으로 실행합니다.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBgm(SoundId.GameBgm);
        }

        LoadRoomMapAsync().Forget();
    }

    private async UniTask LoadRoomMapAsync()
    {
        if (isLoading)
        {
            return;
        }

        isLoading = true;

        // Map1을 에디터에서 직접 실행했거나 로비를 거치지 않은 경우에는 RoomId가 없으므로 중단합니다.
        if (GameSessionContext.Instance == null || !GameSessionContext.Instance.HasSession)
        {
            Debug.LogWarning("[GameSceneBootstrap] Game session is missing. Enter the game scene from LobbyScene.");
            return;
        }

        string roomId = GameSessionContext.Instance.RoomId;

        // 로비의 RoomService 인스턴스를 넘겨받지 않고, 게임 씬에서 Firestore를 단건 조회합니다.
        // 이렇게 해야 씬 의존성이 작고 모든 클라이언트가 같은 room.FinalMap을 기준으로 로드합니다.
        IRoomRepository roomRepository = new FirestoreRoomRepository();
        RoomState room = await roomRepository.GetRoomAsync(roomId);

        if (room == null)
        {
            Debug.LogWarning($"[GameSceneBootstrap] Room not found. RoomId: {roomId}");
            return;
        }

        if (room.FinalMap == null)
        {
            Debug.LogWarning($"[GameSceneBootstrap] FinalMap is missing. RoomId: {roomId}");
            return;
        }

        // Firestore에 저장된 공유 DTO를 실제 게임 로직용 MapLayoutData로 변환합니다.
        if (!AiMapLayoutParser.ToMapLayoutData(room.FinalMap, out MapLayoutData layout, out string errorMessage))
        {
            Debug.LogWarning($"[GameSceneBootstrap] Failed to parse FinalMap. Reason: {errorMessage}");
            return;
        }

        // 변환에 성공한 finalMap만 실제 게임 맵으로 생성합니다.
        GameMapSpawner spawner = GetOrCreateMapSpawner();
        spawner.Spawn(layout);

        IReadOnlyList<RoomPlayerState> players = await roomRepository.GetPlayersAsync(roomId);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            // 실제 Netcode 실행 중에는 Host만 Player NetworkObject를 생성합니다.
            await GetOrCreateNetworkPlayerSpawner().SpawnPlayersAsync(
                players,
                spawner,
                RelayGameStartService.ActiveSessionRegistry);

            GetOrCreateGameRoundManager().StartWatching(players.Count);
        }
        else if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            // Netcode 없이 Map1만 확인할 때는 임시 preview 마커만 표시합니다.
            GetOrCreatePlayerPreviewSpawner().SpawnPlayers(
                players,
                spawner,
                GameSessionContext.Instance.LocalUserId);
        }

        Debug.Log($"[GameSceneBootstrap] FinalMap loaded. RoomId: {room.RoomId}, MapVersion: {room.MapVersion}");
    }

    private GameMapSpawner GetOrCreateMapSpawner()
    {
        // Map1에 남아 있는 테스트용 preview spawner가 같은 맵을 만들지 못하게 먼저 꺼둡니다.
        DisablePreviewSpawner();

        if (mapSpawner != null)
        {
            return mapSpawner;
        }

        mapSpawner = FindFirstObjectByType<GameMapSpawner>();
        if (mapSpawner != null)
        {
            return mapSpawner;
        }

        GameObject mapRoot = new GameObject("GameMapSpawner");
        mapSpawner = mapRoot.AddComponent<GameMapSpawner>();
        return mapSpawner;
    }

    private GamePlayerPreviewSpawner GetOrCreatePlayerPreviewSpawner()
    {
        if (playerPreviewSpawner != null)
        {
            return playerPreviewSpawner;
        }

        playerPreviewSpawner = FindFirstObjectByType<GamePlayerPreviewSpawner>();
        if (playerPreviewSpawner != null)
        {
            return playerPreviewSpawner;
        }

        GameObject playerRoot = new GameObject("GamePlayerPreviewSpawner");
        playerPreviewSpawner = playerRoot.AddComponent<GamePlayerPreviewSpawner>();
        return playerPreviewSpawner;
    }

    private NetworkPlayerSpawner GetOrCreateNetworkPlayerSpawner()
    {
        if (networkPlayerSpawner != null)
        {
            return networkPlayerSpawner;
        }

        networkPlayerSpawner = FindFirstObjectByType<NetworkPlayerSpawner>();
        if (networkPlayerSpawner != null)
        {
            return networkPlayerSpawner;
        }

        GameObject playerRoot = new GameObject("NetworkPlayerSpawner");
        networkPlayerSpawner = playerRoot.AddComponent<NetworkPlayerSpawner>();
        return networkPlayerSpawner;
    }

    private GameRoundManager GetOrCreateGameRoundManager()
    {
        if (gameRoundManager != null)
        {
            return gameRoundManager;
        }

        gameRoundManager = FindFirstObjectByType<GameRoundManager>();
        if (gameRoundManager != null)
        {
            return gameRoundManager;
        }

        GameObject roundRoot = new GameObject("GameRoundManager");
        gameRoundManager = roundRoot.AddComponent<GameRoundManager>();
        return gameRoundManager;
    }

    private void DisablePreviewSpawner()
    {
        // MapPreviewSpawner는 로비/테스트 미리보기 용도이므로 실제 게임 씬 로딩에서는 사용하지 않습니다.
        MapPreviewSpawner previewSpawner = FindFirstObjectByType<MapPreviewSpawner>();
        if (previewSpawner != null)
        {
            previewSpawner.gameObject.SetActive(false);
        }
    }
}
