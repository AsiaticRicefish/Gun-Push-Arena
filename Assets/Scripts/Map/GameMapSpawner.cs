using UnityEngine;

/// <summary>
/// 게임 플레이 씬에서 사용하는 실제 맵 스포너입니다.
/// 로비 미리보기용 MapPreviewSpawner와 분리해서, 게임용 시각 요소와 충돌체 생성,
/// 플레이어 스폰 위치 계산을 담당합니다.
/// </summary>
public class GameMapSpawner : MonoBehaviour
{
    [Header("Tile Visuals")]
    // MapLayoutData의 한 칸을 Unity 월드 좌표에서 몇 unit으로 표현할지 결정합니다.
    [SerializeField] private float tileSize = 1f;
    [SerializeField] private Color floorColor = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] private Color wallColor = new Color(0.1f, 0.1f, 0.1f);
    [SerializeField] private Color player1SpawnColor = Color.cyan;
    [SerializeField] private Color player2SpawnColor = Color.magenta;

    [Header("Physics")]
    // Wall 타일에 BoxCollider2D를 붙여 실제 플레이 중 통과하지 못하게 합니다.
    [SerializeField] private bool createWallColliders = true;

    // 좌상단 기준 타일 좌표를 맵 중앙 기준 월드 좌표로 옮기기 위한 보정값입니다.
    private Vector2 mapOffset;

    // 런타임에 흰색 1픽셀 스프라이트를 한 번만 만들어 모든 타일 렌더링에 재사용합니다.
    private Sprite squareSprite;

    // 이후 NetworkObject 플레이어 스폰에서 SlotIndex 0/1에 맞는 위치로 매핑합니다.
    public Vector3 Player1SpawnWorldPosition { get; private set; }
    public Vector3 Player2SpawnWorldPosition { get; private set; }

    /// <summary>
    /// 검증과 파싱이 끝난 MapLayoutData를 실제 GameObject 타일들로 생성합니다.
    /// </summary>
    public void Spawn(MapLayoutData layout)
    {
        if (layout == null)
        {
            Debug.LogWarning("[GameMapSpawner] Layout is null.");
            return;
        }

        ClearChildren();

        // 맵 중심이 월드 원점 근처에 오도록 좌표를 보정합니다.
        mapOffset = new Vector2((layout.Width - 1) * 0.5f, (layout.Height - 1) * 0.5f);

        SpawnTiles(layout);

        // 스폰 타일 좌표를 플레이어 생성에 바로 사용할 수 있는 월드 좌표로 저장합니다.
        Player1SpawnWorldPosition = CellToWorld(layout.Player1Spawn, -0.2f);
        Player2SpawnWorldPosition = CellToWorld(layout.Player2Spawn, -0.2f);

        SpawnMarker(layout.Player1Spawn, player1SpawnColor, "Player1Spawn");
        SpawnMarker(layout.Player2Spawn, player2SpawnColor, "Player2Spawn");

        Debug.Log($"[GameMapSpawner] Map spawned. Size: {layout.Width}x{layout.Height}, P1: {Player1SpawnWorldPosition}, P2: {Player2SpawnWorldPosition}");
    }

    /// <summary>
    /// rooms/{roomId}/players의 SlotIndex를 기준으로 플레이어 스폰 위치를 반환합니다.
    /// </summary>
    public Vector3 GetSpawnWorldPosition(int slotIndex)
    {
        return slotIndex == 0
            ? Player1SpawnWorldPosition
            : Player2SpawnWorldPosition;
    }

    private void SpawnTiles(MapLayoutData layout)
    {
        // MapLayoutData는 1차원 타일 배열이지만, 스폰은 x/y 격자 순회로 처리합니다.
        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                MapTileType tileType = layout.GetTile(x, y);

                if (tileType == MapTileType.Empty)
                {
                    // Empty는 빈 공간이므로 렌더러와 콜라이더를 만들지 않습니다.
                    continue;
                }

                Color color = tileType == MapTileType.Wall ? wallColor : floorColor;
                float zPosition = tileType == MapTileType.Wall ? -0.1f : 0f;
                GameObject tile = SpawnSquare(
                    new Vector2Int(x, y),
                    color,
                    $"{tileType}_{x}_{y}",
                    1f,
                    zPosition);

                if (tileType == MapTileType.Wall && createWallColliders)
                {
                    // Wall만 물리 충돌 대상입니다. Floor는 이동 가능한 바닥으로 둡니다.
                    BoxCollider2D collider = tile.AddComponent<BoxCollider2D>();
                    collider.size = Vector2.one;
                }
            }
        }
    }

    private void SpawnMarker(Vector2Int cell, Color color, string objectName)
    {
        // 현재는 디버깅을 위해 스폰 위치를 작은 색상 마커로 표시합니다.
        // 나중에 실제 플레이어 스폰이 안정되면 인스펙터 옵션으로 숨길 수 있습니다.
        SpawnSquare(cell, color, objectName, 0.55f, -0.2f);
    }

    private GameObject SpawnSquare(
        Vector2Int cell,
        Color color,
        string objectName,
        float scaleMultiplier,
        float zPosition)
    {
        GameObject square = new GameObject(objectName);
        square.transform.SetParent(transform);
        square.transform.position = CellToWorld(cell, zPosition);
        square.transform.localScale = Vector3.one * tileSize * scaleMultiplier;

        SpriteRenderer spriteRenderer = square.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetSquareSprite();
        spriteRenderer.color = color;

        return square;
    }

    private Sprite GetSquareSprite()
    {
        if (squareSprite != null)
        {
            return squareSprite;
        }

        // 별도 아트 리소스 없이도 프로토타입 맵을 볼 수 있도록 Unity 기본 흰 텍스처를 스프라이트로 변환합니다.
        Texture2D texture = Texture2D.whiteTexture;
        squareSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);

        return squareSprite;
    }

    private Vector3 CellToWorld(Vector2Int cell, float zPosition)
    {
        // 타일 좌표계는 (0,0)부터 시작하지만, 월드에서는 맵 중심이 원점에 오도록 offset을 뺍니다.
        float x = (cell.x - mapOffset.x) * tileSize;
        float y = (cell.y - mapOffset.y) * tileSize;

        return new Vector3(x, y, zPosition);
    }

    private void ClearChildren()
    {
        // 같은 spawner를 재사용해 맵을 다시 로드할 수 있도록 이전 타일들을 제거합니다.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }
}
