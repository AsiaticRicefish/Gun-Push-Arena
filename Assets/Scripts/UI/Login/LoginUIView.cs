using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginUIView : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button guestLoginButton;
    [SerializeField] private Button nicknameConfirmButton;

    [Header("Panels")]
    [SerializeField] private GameObject nicknamePanel;
    [SerializeField] private GameObject noticePanel;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField nicknameInput;

    [Header("Texts")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text noticeMessageText;

    [Header("Notice")]
    [SerializeField] private Button noticeOkButton;

    public string Nickname => nicknameInput != null ? nicknameInput.text.Trim() : "";

    public void Initialize()
    {
        SetNicknamePanelActive(false);
        SetStatus("");
        SetNoticeVisible(false);

        if (noticeOkButton != null)
        {
            noticeOkButton.onClick.RemoveListener(HideNotice);
            noticeOkButton.onClick.AddListener(HideNotice);
        }
        else
        {
            Debug.LogWarning("[LoginUIView] Notice Ok Button is not assigned.");
        }
    }

    public void SetGuestLoginInteractable(bool interactable)
    {
        if (guestLoginButton != null)
        {
            guestLoginButton.interactable = interactable;
        }
    }

    public void SetNicknameConfirmInteractable(bool interactable)
    {
        if (nicknameConfirmButton != null)
        {
            nicknameConfirmButton.interactable = interactable;
        }
    }

    public void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    public void ShowNotice(string message)
    {
        SetStatus(message);

        if (noticeMessageText != null)
        {
            noticeMessageText.text = message;
        }
        else
        {
            Debug.LogWarning("[LoginUIView] Notice Message Text is not assigned.");
        }

        SetNoticeVisible(true);
    }

    public void HideNotice()
    {
        SetNoticeVisible(false);
    }

    public void OpenNicknamePanel(string nickname)
    {
        SetNicknamePanelActive(true);

        if (nicknameInput != null)
        {
            nicknameInput.text = nickname;
            nicknameInput.Select();
        }
    }

    private void SetNicknamePanelActive(bool active)
    {
        if (nicknamePanel != null)
        {
            nicknamePanel.SetActive(active);
        }
    }

    private void SetNoticeVisible(bool visible)
    {
        if (noticePanel != null)
        {
            noticePanel.SetActive(visible);
        }
        else if (visible)
        {
            Debug.LogWarning("[LoginUIView] Notice Panel is not assigned.");
        }
    }
}
