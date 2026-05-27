using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Relay/Netcode 연결 전, Firestore players 문서의 SlotIndex와 맵 스폰 위치 매핑을 검증하기 위한 임시 스포너입니다.
/// 실제 플레이어 동기화는 Host가 NetworkPlayerSpawner로 NetworkObject를 스폰하는 구조로 대체됩니다.
/// </summary>
public class GamePlayerPreviewSpawner : MonoBehaviour
{
    [SerializeField] private float markerSize = 0.8f;
    [SerializeField] private Color fallbackColor = Color.white;

    private Sprite squareSprite;

    public void SpawnPlayers(IReadOnlyList<RoomPlayerState> players, GameMapSpawner mapSpawner, string localUserId)
    {
        ClearChildren();

        if (players == null || mapSpawner == null)
        {
            Debug.LogWarning("[GamePlayerPreviewSpawner] Players or mapSpawner is missing.");
            return;
        }

        foreach (RoomPlayerState player in players)
        {
            Vector3 spawnPosition = mapSpawner.GetSpawnWorldPosition(player.SlotIndex);
            SpawnPlayerMarker(player, spawnPosition, localUserId);
        }

        Debug.Log($"[GamePlayerPreviewSpawner] Spawned player previews. Count: {players.Count}");
    }

    private void SpawnPlayerMarker(RoomPlayerState player, Vector3 spawnPosition, string localUserId)
    {
        GameObject marker = new GameObject($"PlayerPreview_Slot{player.SlotIndex}_{player.Nickname}");
        marker.transform.SetParent(transform);
        marker.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, -0.6f);

        // SpriteRenderer와 TextMesh는 둘 다 Renderer 계열이라 같은 GameObject에 붙이지 않고 자식으로 분리합니다.
        GameObject body = new GameObject("Body");
        body.transform.SetParent(marker.transform, false);
        body.transform.localScale = Vector3.one * markerSize;

        SpriteRenderer spriteRenderer = body.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetSquareSprite();
        spriteRenderer.color = TryParseColor(player.ColorHex, out Color color) ? color : fallbackColor;
        spriteRenderer.sortingOrder = player.UserId == localUserId ? 20 : 15;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(marker.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = player.SlotIndex == 0 ? "P1" : "P2";
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = 0.35f;
        label.fontSize = 32;
        label.color = Color.black;

        MeshRenderer labelRenderer = labelObject.GetComponent<MeshRenderer>();
        if (labelRenderer != null)
        {
            labelRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        }
    }

    private bool TryParseColor(string colorHex, out Color color)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            color = default;
            return false;
        }

        return ColorUtility.TryParseHtmlString(colorHex, out color);
    }

    private Sprite GetSquareSprite()
    {
        if (squareSprite != null)
        {
            return squareSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        squareSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);

        return squareSprite;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }
}
