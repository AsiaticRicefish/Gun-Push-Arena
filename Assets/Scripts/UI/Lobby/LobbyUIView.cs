using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobby 화면의 View입니다.
/// 실제 UI 컴포넌트 표시와 입력값 제공만 담당하고, 방 생성/참가 같은 로직은 Presenter/Service에 맡깁니다.
/// </summary>
public class LobbyUIView : MonoBehaviour
{
    [Header("Root Panels")]
    [SerializeField] private GameObject userInfoPanel;
    [SerializeField] private GameObject createJoinPanel;
    [SerializeField] private GameObject roomPanel;

    [Header("User")]
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text userIdText;
    [SerializeField] private Image colorPreview;

    [Header("Create / Join")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;

    [Header("Room Header")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private Button copyRoomCodeButton;
    [SerializeField] private TMP_Text roomStatusText;
    [SerializeField] private Button leaveRoomButton;

    [Header("Player Slots")]
    [SerializeField] private LobbyPlayerSlotView player1Slot;
    [SerializeField] private LobbyPlayerSlotView player2Slot;

    [Header("Map Setup")]
    [SerializeField] private GameObject mapSetupPanel;
    [SerializeField] private TMP_Dropdown themeDropdown;
    [SerializeField] private Button generateMapButton;
    [SerializeField] private TMP_Text mapStatusText;
    [SerializeField] private GameObject mapPreviewArea;
    [SerializeField] private LobbyMapPreviewView mapPreviewView;

    [Header("Room Actions")]
    [SerializeField] private GameObject roomActionPanel;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button startGameButton;

    public string RoomCodeInput => roomCodeInput != null ? roomCodeInput.text.Trim() : "";

    public void Initialize()
    {
        SetStatus("");
        SetMapStatus("");
        SetLoading(false);
        ClearRoomInfo();
        ShowLobbyMode();
    }

    public void SetUserInfo(string nickname, string colorHex, string userId)
    {
        if (userInfoPanel != null)
        {
            userInfoPanel.SetActive(true);
        }

        if (nicknameText != null)
        {
            nicknameText.text = nickname;
        }

        if (userIdText != null)
        {
            userIdText.text = ShortenUserId(userId);
        }

        if (colorPreview != null && ColorUtility.TryParseHtmlString(colorHex, out Color color))
        {
            colorPreview.color = color;
        }
    }

    public void SetRoomInfo(RoomState room)
    {
        if (room == null)
        {
            ClearRoomInfo();
            ShowLobbyMode();
            return;
        }

        ShowRoomMode();

        if (roomCodeText != null)
        {
            roomCodeText.text = room.RoomId;
        }

        if (roomStatusText != null)
        {
            roomStatusText.text = room.Status;
        }

        SetMapStatus(CreateMapStatusText(room));
        SetMapPreviewVisible(room.FinalMap != null);

        if (mapPreviewView != null)
        {
            if (room.FinalMap != null)
            {
                mapPreviewView.SetMap(room.FinalMap);
            }
            else
            {
                mapPreviewView.Clear();
            }
        }
    }

    public void ClearRoomInfo()
    {
        if (roomCodeText != null)
        {
            roomCodeText.text = "";
        }

        if (roomStatusText != null)
        {
            roomStatusText.text = "";
        }

        SetMapStatus("");
        SetMapPreviewVisible(false);
        if (mapPreviewView != null)
        {
            mapPreviewView.Clear();
        }

        SetPlayerSlotsEmpty();
        SetHostControlsVisible(false);
        SetReadyState(false);
    }

    public void SetPlayers(IReadOnlyList<RoomPlayerState> players, string hostUserId)
    {
        SetPlayerSlotsEmpty();

        if (players == null)
        {
            return;
        }

        foreach (RoomPlayerState player in players)
        {
            bool isHost = player.UserId == hostUserId;

            if (player.SlotIndex == 0 && player1Slot != null)
            {
                player1Slot.SetPlayer(player, isHost);
            }
            else if (player.SlotIndex == 1 && player2Slot != null)
            {
                player2Slot.SetPlayer(player, isHost);
            }
        }
    }

    public void SetHostControlsVisible(bool visible)
    {
        // MapSetupPanel은 참가자도 볼 수 있게 유지하고, 조작 가능 여부만 방장 기준으로 바꿉니다.
        if (mapSetupPanel != null)
        {
            mapSetupPanel.SetActive(true);
        }

        SetMapSetupInteractable(visible);
    }

    public void SetRoomControlsVisible(bool inRoom)
    {
        if (roomPanel != null)
        {
            roomPanel.SetActive(inRoom);
        }

        if (roomActionPanel != null)
        {
            roomActionPanel.SetActive(inRoom);
        }

        if (createJoinPanel != null)
        {
            createJoinPanel.SetActive(!inRoom);
        }
    }

    public void SetCreateJoinInteractable(bool interactable)
    {
        if (createRoomButton != null)
        {
            createRoomButton.interactable = interactable;
        }

        if (joinRoomButton != null)
        {
            joinRoomButton.interactable = interactable;
        }

        if (roomCodeInput != null)
        {
            roomCodeInput.interactable = interactable;
        }
    }

    public void SetRoomActionInteractable(bool interactable)
    {
        if (leaveRoomButton != null)
        {
            leaveRoomButton.interactable = interactable;
        }

        if (copyRoomCodeButton != null)
        {
            copyRoomCodeButton.interactable = interactable;
        }
    }

    public void SetReadyInteractable(bool interactable)
    {
        if (readyButton != null)
        {
            readyButton.interactable = interactable;
        }
    }

    public void SetStartGameInteractable(bool interactable)
    {
        if (startGameButton != null)
        {
            startGameButton.interactable = interactable;
        }
    }

    public void SetReadyVisible(bool visible)
    {
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(visible);
        }
    }

    public void SetStartGameVisible(bool visible)
    {
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(visible);
        }
    }

    public void SetGenerateMapInteractable(bool interactable)
    {
        if (generateMapButton != null)
        {
            generateMapButton.interactable = interactable;
        }
    }

    public void SetTheme(AiMapTheme theme)
    {
        if (themeDropdown == null)
        {
            return;
        }

        themeDropdown.SetValueWithoutNotify(ThemeToIndex(theme));
    }

    public void SetReadyState(bool isReady)
    {
        if (readyButtonText != null)
        {
            readyButtonText.text = isReady ? "Cancel Ready" : "Ready";
        }
    }

    public void SetStatus(string message)
    {
        // 전체 StatusText UI는 제거했습니다.
        // Presenter 흐름은 유지하되, 필요하면 Console에서 상태를 확인할 수 있게 로그만 남깁니다.
        if (!string.IsNullOrWhiteSpace(message))
        {
            Debug.Log($"[LobbyUIView] {message}");
        }
    }

    public void SetLoading(bool isLoading)
    {
        // 별도 LoadingIndicator UI는 사용하지 않습니다.
        // 버튼 interactable 제어는 Presenter.RefreshViewState에서 계속 처리합니다.
    }

    public void CopyRoomCodeToClipboard()
    {
        if (roomCodeText == null || string.IsNullOrWhiteSpace(roomCodeText.text))
        {
            return;
        }

        GUIUtility.systemCopyBuffer = roomCodeText.text;
        SetStatus("Room code copied.");
    }

    private void ShowLobbyMode()
    {
        if (userInfoPanel != null)
        {
            userInfoPanel.SetActive(true);
        }

        if (createJoinPanel != null)
        {
            createJoinPanel.SetActive(true);
        }

        if (roomPanel != null)
        {
            roomPanel.SetActive(false);
        }
    }

    private void ShowRoomMode()
    {
        if (userInfoPanel != null)
        {
            userInfoPanel.SetActive(true);
        }

        if (createJoinPanel != null)
        {
            createJoinPanel.SetActive(false);
        }

        if (roomPanel != null)
        {
            roomPanel.SetActive(true);
        }

        if (mapSetupPanel != null)
        {
            mapSetupPanel.SetActive(true);
        }

        if (roomActionPanel != null)
        {
            roomActionPanel.SetActive(true);
        }
    }

    private void SetMapSetupInteractable(bool interactable)
    {
        if (themeDropdown != null)
        {
            themeDropdown.interactable = interactable;
        }

        if (generateMapButton != null)
        {
            generateMapButton.gameObject.SetActive(interactable);
            generateMapButton.interactable = interactable;
        }
    }

    private void SetPlayerSlotsEmpty()
    {
        if (player1Slot != null)
        {
            player1Slot.SetEmpty();
        }

        if (player2Slot != null)
        {
            player2Slot.SetEmpty();
        }
    }

    private void SetMapStatus(string message)
    {
        if (mapStatusText != null)
        {
            mapStatusText.text = message;
        }
    }

    private void SetMapPreviewVisible(bool visible)
    {
        if (mapPreviewArea != null)
        {
            mapPreviewArea.SetActive(visible);
        }
    }

    private string CreateMapStatusText(RoomState room)
    {
        if (room == null)
        {
            return "";
        }

        if (room.Status == RoomStatus.GeneratingMap.ToString())
        {
            return "Generating map...";
        }

        if (room.FinalMap != null)
        {
            return $"Map ready v{room.MapVersion}";
        }

        return "No map generated.";
    }

    private int ThemeToIndex(AiMapTheme theme)
    {
        switch (theme)
        {
            case AiMapTheme.Split:
                return 1;
            case AiMapTheme.Vertical:
                return 2;
            case AiMapTheme.Chaos:
                return 3;
            default:
                return 0;
        }
    }

    private string ShortenUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId) || userId.Length <= 8)
        {
            return userId;
        }

        return userId.Substring(0, 8);
    }
}
