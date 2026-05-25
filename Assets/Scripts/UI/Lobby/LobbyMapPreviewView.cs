using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RoomPanel 안의 MapPreviewArea에 최종 AiMapLayoutDto를 간단한 UI 타일 프리뷰로 그립니다.
/// 실제 게임 맵 생성기가 아니라, 로비에서 "같은 finalMap이 공유되었는지" 확인하기 위한 미리보기입니다.
/// </summary>
public class LobbyMapPreviewView : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color floorColor = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] private Color wallColor = new Color(0.1f, 0.1f, 0.1f);
    [SerializeField] private Color player1SpawnColor = Color.cyan;
    [SerializeField] private Color player2SpawnColor = Color.magenta;

    [Header("Layout")]
    [SerializeField] private float tileGap = 1f;
    [SerializeField] private float spawnMarkerScale = 0.55f;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void SetMap(AiMapLayoutDto map)
    {
        Clear();

        if (map == null || map.tiles == null || map.width <= 0 || map.height <= 0)
        {
            return;
        }

        int expectedTileCount = map.width * map.height;
        if (map.tiles.Length != expectedTileCount)
        {
            Debug.LogWarning($"[LobbyMapPreviewView] Invalid tile count. Expected: {expectedTileCount}, Current: {map.tiles.Length}");
            return;
        }

        Rect rect = RectTransform.rect;
        float cellSize = Mathf.Min(rect.width / map.width, rect.height / map.height);
        float mapPixelWidth = cellSize * map.width;
        float mapPixelHeight = cellSize * map.height;

        for (int y = 0; y < map.height; y++)
        {
            for (int x = 0; x < map.width; x++)
            {
                int tile = map.tiles[y * map.width + x];

                if (tile == (int)MapTileType.Empty)
                {
                    continue;
                }

                Color color = tile == (int)MapTileType.Wall ? wallColor : floorColor;
                CreateCell($"Tile_{x}_{y}", x, y, cellSize, mapPixelWidth, mapPixelHeight, color, 1f);
            }
        }

        if (map.player1Spawn != null)
        {
            CreateCell("Player1Spawn", map.player1Spawn.x, map.player1Spawn.y, cellSize, mapPixelWidth, mapPixelHeight, player1SpawnColor, spawnMarkerScale);
        }

        if (map.player2Spawn != null)
        {
            CreateCell("Player2Spawn", map.player2Spawn.x, map.player2Spawn.y, cellSize, mapPixelWidth, mapPixelHeight, player2SpawnColor, spawnMarkerScale);
        }
    }

    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    private void CreateCell(
        string objectName,
        int x,
        int y,
        float cellSize,
        float mapPixelWidth,
        float mapPixelHeight,
        Color color,
        float scale)
    {
        GameObject cell = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        cell.transform.SetParent(transform, false);

        RectTransform cellRect = cell.GetComponent<RectTransform>();
        cellRect.anchorMin = new Vector2(0.5f, 0.5f);
        cellRect.anchorMax = new Vector2(0.5f, 0.5f);
        cellRect.pivot = new Vector2(0.5f, 0.5f);
        cellRect.sizeDelta = Vector2.one * Mathf.Max(1f, (cellSize - tileGap) * scale);

        float anchoredX = (x + 0.5f) * cellSize - mapPixelWidth * 0.5f;
        float anchoredY = (y + 0.5f) * cellSize - mapPixelHeight * 0.5f;
        cellRect.anchoredPosition = new Vector2(anchoredX, anchoredY);

        Image image = cell.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private RectTransform RectTransform
    {
        get
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            return rectTransform;
        }
    }
}
