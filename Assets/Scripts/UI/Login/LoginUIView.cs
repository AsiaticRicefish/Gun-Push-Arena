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

    [Header("Inputs")]
    [SerializeField] private TMP_InputField nicknameInput;

    [Header("Texts")]
    [SerializeField] private TMP_Text statusText;

    public string Nickname => nicknameInput != null ? nicknameInput.text.Trim() : "";

    public void Initialize()
    {
        SetNicknamePanelActive(false);
        SetStatus("");
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
}