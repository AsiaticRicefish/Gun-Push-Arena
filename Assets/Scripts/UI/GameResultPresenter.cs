using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameResultPresenter : MonoBehaviour
{
    private const string LobbySceneName = "LobbyScene";

    private static GameResultPresenter instance;

    [SerializeField] private GameResultUIView view;

    public static void ShowResult(ulong winnerClientId)
    {
        GameResultPresenter presenter = instance != null
            ? instance
            : FindFirstObjectByType<GameResultPresenter>();

        if (presenter == null)
        {
            Debug.LogWarning("[GameResultPresenter] Presenter is missing in the scene.");
            return;
        }

        presenter.Show(winnerClientId);
    }

    private void Awake()
    {
        instance = this;

        if (view == null)
        {
            view = FindFirstObjectByType<GameResultUIView>();
        }
    }

    private void OnEnable()
    {
        if (view != null)
        {
            view.ReturnLobbyClicked += ReturnToLobby;
        }
    }

    private void OnDisable()
    {
        if (view != null)
        {
            view.ReturnLobbyClicked -= ReturnToLobby;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Show(ulong winnerClientId)
    {
        if (view == null)
        {
            Debug.LogWarning("[GameResultPresenter] View is missing.");
            return;
        }

        bool isWinner = NetworkManager.Singleton != null &&
                        NetworkManager.Singleton.LocalClientId == winnerClientId;

        if (isWinner)
        {
            view.ShowWin();
            PlayLocalSfx(SoundId.Win);
        }
        else
        {
            view.ShowLose();
            PlayLocalSfx(SoundId.Lose);
        }
    }

    private void PlayLocalSfx(SoundId soundId)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(soundId);
        }
    }

    private void ReturnToLobby()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            Destroy(networkManager.gameObject);
        }

        if (GameSessionContext.Instance != null)
        {
            GameSessionContext.Instance.ClearSession();
        }

        SceneManager.LoadScene(LobbySceneName);
    }
}
