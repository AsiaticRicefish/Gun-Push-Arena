using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

[RequireComponent(typeof(LoginUIView))]
public class LoginUIController : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private LoginUIView view;

    [Header("Scenes")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private IAuthService authService;

    private LoginPresenter presenter;

    private void Awake()
    {
        if (view == null)
        {
            view = GetComponent<LoginUIView>();
        }
    }

    public void Construct(IAuthService authService)
    {
        this.authService = authService;
    }

    private void Start()
    {
        presenter = new LoginPresenter(view, authService, LoadLobbyScene);
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

    private void LoadLobbyScene()
    {
        SceneManager.LoadScene(lobbySceneName);
    }
}
