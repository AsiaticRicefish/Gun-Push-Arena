using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

public class LoginUIController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button guestLoginButton;

    [Header("Panels")]
    [SerializeField] private GameObject nicknamePanel;

    [Header("Texts")]
    [SerializeField] private TMP_Text statusText;

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
    }


    private void GuestButtonInteractable(bool interactable)
    {
        if (guestLoginButton != null)
        {
            guestLoginButton.interactable = interactable;
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