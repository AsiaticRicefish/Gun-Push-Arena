using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public class LoginUIController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button guestLoginButton;
    [SerializeField] private Button nicknameConfirmButton;

    [Header("Panels")]
    [SerializeField] private GameObject nicknamePanel;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField nicknameInput;

    [Header("Texts")]
    [SerializeField] private TMP_Text statusText;

    [Header("Scenes")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private void Start()
    {
        // 시작할 때는 닉네임 패널을 비활성화 함
        if (nicknamePanel != null)
        {
            nicknamePanel.SetActive(false);
        }

        // 현재 로그인 상태
        LoginStatus("");
    }

    /// <summary>
    /// 게스트 로그인 버튼 클릭 시 호출
    /// </summary>
    public void OnClickGuestLogin()
    {
        _ = HandleGuestLoginAsync();
    }

    /// <summary>
    /// 게스트 로그인 프로세스를 처리하는 비동기 메서드
    /// 로그인 버튼을 비활성화하고 상태 메시지를 업데이트한 후, 
    /// AuthManager의 GuestLoginAsync() 메서드를 호출하여 로그인 시도를 합니다. 
    /// 로그인 성공 시 닉네임 패널을 열고, 실패 시 상태 메시지를 업데이트하고 로그인 버튼을 다시 활성화합니다.
    /// </summary>
    /// <returns></returns>
    private async Task HandleGuestLoginAsync()
    {
        GuestButtonInteractable(false);
        LoginStatus("Signing in...");

        bool success = await AuthManager.Instance.GuestLoginAsync();

        if (success)
        {
            LoginStatus("Signed in.");
            if (AuthManager.Instance.CurrentUserData.IsNicknameSet)
            {
                LoadLobbyScene();
                return;
            }

            OpenNicknamePanel();
            return;
        }

        LoginStatus("Guest login failed.");
        GuestButtonInteractable(true);
    }

    // 닉네임 패널 호츌
    private void OpenNicknamePanel()
    {
        if (nicknamePanel != null)
        {
            nicknamePanel.SetActive(true);
        }

        if (nicknameInput != null && AuthManager.Instance.CurrentUserData != null)
        {
            nicknameInput.text = AuthManager.Instance.CurrentUserData.Nickname;
            nicknameInput.Select();
        }
    }

    public void OnClickConfirmNickname()
    {
        _ = HandleConfirmNicknameAsync();
    }

    private async Task HandleConfirmNicknameAsync()
    {
        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : "";

        if (!IsValidNickname(nickname))
        {
            LoginStatus("Nickname must be 2-12 characters.");
            return;
        }

        NicknameButtonInteractable(false);
        LoginStatus("Saving nickname...");

        bool success = await AuthManager.Instance.UpdateNicknameAsync(nickname);

        if (success)
        {
            LoginStatus("Nickname saved.");
            LoadLobbyScene();
            return;
        }

        LoginStatus("Nickname save failed.");
        NicknameButtonInteractable(true);
    }

    private bool IsValidNickname(string nickname)
    {
        return nickname.Length >= 2 && nickname.Length <= 12;
    }

    private void LoadLobbyScene()
    {
        SceneManager.LoadScene(lobbySceneName);
    }


    private void GuestButtonInteractable(bool interactable)
    {
        if (guestLoginButton != null)
        {
            guestLoginButton.interactable = interactable;
        }
    }

    private void NicknameButtonInteractable(bool interactable)
    {
        if (nicknameConfirmButton != null)
        {
            nicknameConfirmButton.interactable = interactable;
        }
    }

    private void LoginStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
