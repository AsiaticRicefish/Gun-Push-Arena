using UnityEngine;

[RequireComponent(typeof(LoginUIView))]
public class LoginUIController : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private LoginUIView view;

    [Header("Scenes")]
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private SceneLoader sceneLoader;

    private IAuthService authService;

    private LoginPresenter presenter;

    private void Awake()
    {
        if (view == null)
        {
            view = GetComponent<LoginUIView>();
        }

        if (sceneLoader == null)
        {
            sceneLoader = GetComponent<SceneLoader>();
        }
    }

    public void Construct(IAuthService authService)
    {
        this.authService = authService;
    }

    private void Start()
    {
        if (sceneLoader == null)
        {
            Debug.LogError("[LoginUIController] SceneLoader is missing.");
            enabled = false;
            return;
        }

        if (authService == null)
        {
            authService = AuthManager.Instance;
        }

        if (authService == null)
        {
            Debug.LogError("[LoginUIController] AuthService is missing.");
            enabled = false;
            return;
        }

        presenter = new LoginPresenter(view, authService, sceneLoader, lobbySceneName);
        presenter.Initialize();
    }

    public void OnClickGuestLogin()
    {
        _ = presenter.HandleGuestLoginAsync();
    }

    public void OnClickConfirmNickname()
    {
        _ = presenter.HandleConfirmNicknameAsync();
    }

    public void OnClickQuitGame()
    {
        ApplicationQuitter.Quit();
    }
}
