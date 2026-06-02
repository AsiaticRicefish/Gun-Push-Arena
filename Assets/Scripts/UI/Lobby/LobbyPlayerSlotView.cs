using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RoomPanel 안의 플레이어 슬롯 하나를 표시하는 View입니다.
/// 1P/2P 슬롯 각각에 붙이고, 비어 있으면 Waiting 상태를 보여줍니다.
/// </summary>
public class LobbyPlayerSlotView : MonoBehaviour
{
    [SerializeField] private Image colorImage;
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text readyText;

    public void SetEmpty()
    {
        if (nicknameText != null)
        {
            nicknameText.text = "대기 중...";
        }

        if (roleText != null)
        {
            roleText.text = "";
        }

        if (readyText != null)
        {
            readyText.text = "";
        }

        if (colorImage != null)
        {
            colorImage.color = Color.gray;
        }
    }

    public void SetPlayer(RoomPlayerState player, bool isHost)
    {
        if (player == null)
        {
            SetEmpty();
            return;
        }

        if (nicknameText != null)
        {
            nicknameText.text = player.Nickname;
        }

        if (roleText != null)
        {
            roleText.text = isHost ? "방장" : "참가자";
        }

        if (readyText != null)
        {
            readyText.text = isHost ? "준비 완료" : (player.IsReady ? "준비 완료" : "준비 전");
        }

        if (colorImage != null && ColorUtility.TryParseHtmlString(player.ColorHex, out Color color))
        {
            colorImage.color = color;
        }
    }
}
