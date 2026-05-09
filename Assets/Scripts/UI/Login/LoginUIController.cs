using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(LoginUIView))]
public class LoginUIController : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private LoginUIView view;

    [Header("Scenes")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private LoginPresenter presenter;

    private void Awake()
    {
        if (view == null)
        {
            view = GetComponent<LoginUIView>();
        }
    }

    private void Start()
    {
        presenter = new LoginPresenter(view, AuthManager.Instance, LoadLobbyScene);
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
