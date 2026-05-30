using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameResultUIView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject resultPanel;

    [Header("Result")]
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button returnLobbyButton;

    public event Action ReturnLobbyClicked;

    private void Awake()
    {
        if (returnLobbyButton != null)
        {
            returnLobbyButton.onClick.AddListener(OnReturnLobbyClicked);
        }

        Hide();
    }

    private void OnDestroy()
    {
        if (returnLobbyButton != null)
        {
            returnLobbyButton.onClick.RemoveListener(OnReturnLobbyClicked);
        }
    }

    public void ShowWin()
    {
        Show("You Win", new Color(0.34f, 1f, 0.62f));
    }

    public void ShowLose()
    {
        Show("You Lose", new Color(1f, 0.42f, 0.42f));
    }

    public void Hide()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    private void Show(string message, Color color)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (resultText != null)
        {
            resultText.text = message;
            resultText.color = color;
        }
    }

    private void OnReturnLobbyClicked()
    {
        ReturnLobbyClicked?.Invoke();
    }
}
