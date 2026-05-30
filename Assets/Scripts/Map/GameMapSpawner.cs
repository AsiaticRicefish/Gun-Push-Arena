using UnityEngine;

public class GameMapSpawner : MonoBehaviour
{
    private static PhysicsMaterial2D noFrictionMaterial;

    [Header("Tile Visuals")]
    [SerializeField] private float tileSize = 1f;
    [SerializeField] private Color floorColor = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] private Color wallColor = new Color(0.1f, 0.1f, 0.1f);
    [SerializeField] private Color player1SpawnColor = Color.cyan;
    [SerializeField] private Color player2SpawnColor = Color.magenta;
    [SerializeField] private float platformThickness = 0.35f;

    [Header("Physics")]
    [SerializeField] private bool createWallColliders = true;
    [SerializeField] private bool createFloorColliders = true;

    private Vector2 mapOffset;
    private Sprite squareSprite;

    public Vector3 Player1SpawnWorldPosition { get; private set; }
    public Vector3 Player2SpawnWorldPosition { get; private set; }
    public float DeathY { get; private set; }

    public void Spawn(MapLayoutData layout)
    {
        if (layout == null)
        {
            Debug.LogWarning("[GameMapSpawner] Layout is null.");
            return;
        }

        ClearChildren();
        mapOffset = new Vector2((layout.Width - 1) * 0.5f, (layout.Height - 1) * 0.5f);
        DeathY = GetMapBottomWorldY() - tileSize * 3f;

        SpawnTiles(layout);

        Player1SpawnWorldPosition = GetPlayerSpawnWorldPosition(layout.Player1Spawn);
        Player2SpawnWorldPosition = GetPlayerSpawnWorldPosition(layout.Player2Spawn);

        SpawnMarker(layout.Player1Spawn, player1SpawnColor, "Player1Spawn");
        SpawnMarker(layout.Player2Spawn, player2SpawnColor, "Player2Spawn");

        Debug.Log($"[GameMapSpawner] Map spawned. Size: {layout.Width}x{layout.Height}, P1: {Player1SpawnWorldPosition}, P2: {Player2SpawnWorldPosition}");
    }

    public Vector3 GetSpawnWorldPosition(int slotIndex)
    {
        return slotIndex == 0
            ? Player1SpawnWorldPosition
            : Player2SpawnWorldPosition;
    }

    private void SpawnTiles(MapLayoutData layout)
    {
        SpawnFloorPlatforms(layout);
        SpawnWalls(layout);
    }

    private void SpawnFloorPlatforms(MapLayoutData layout)
    {
        for (int y = 0; y < layout.Height; y++)
        {
            int x = 0;
            while (x < layout.Width)
            {
                if (layout.GetTile(x, y) != MapTileType.Floor)
                {
                    x++;
                    continue;
                }

                int startX = x;
                while (x < layout.Width && layout.GetTile(x, y) == MapTileType.Floor)
                {
                    x++;
                }

                SpawnFloorPlatform(startX, x - 1, y);
            }
        }
    }

    private void SpawnWalls(MapLayoutData layout)
    {
        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                if (layout.GetTile(x, y) != MapTileType.Wall)
                {
                    continue;
                }

                GameObject wall = SpawnRectangle(
                    CellToWorld(new Vector2Int(x, y), -0.1f),
                    Vector2.one * tileSize,
                    wallColor,
                    $"Wall_{x}_{y}");

                if (createWallColliders)
                {
                    AddBoxCollider(wall, Vector2.one);
                }
            }
        }
    }

    private void SpawnFloorPlatform(int startX, int endX, int y)
    {
        int tileCount = endX - startX + 1;
        float width = tileCount * tileSize;
        float height = tileSize * platformThickness;
        float centerX = ((startX + endX) * 0.5f - mapOffset.x) * tileSize;
        float centerY = (y - mapOffset.y) * tileSize;

        GameObject platform = SpawnRectangle(
            new Vector3(centerX, centerY, 0f),
            new Vector2(width, height),
            floorColor,
            $"Platform_{startX}_{endX}_{y}");

        if (createFloorColliders)
        {
            AddTopEdgeCollider(platform);
        }
    }

    private GameObject SpawnRectangle(Vector3 position, Vector2 size, Color color, string objectName)
    {
        GameObject rectangle = new GameObject(objectName);
        rectangle.transform.SetParent(transform);
        rectangle.transform.position = position;
        rectangle.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer spriteRenderer = rectangle.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetSquareSprite();
        spriteRenderer.color = color;

        return rectangle;
    }

    private void AddBoxCollider(GameObject target, Vector2 size)
    {
        BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.sharedMaterial = GetNoFrictionMaterial();
    }

    private void AddTopEdgeCollider(GameObject target)
    {
        EdgeCollider2D collider = target.AddComponent<EdgeCollider2D>();
        collider.points = new[]
        {
            new Vector2(-0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        };
        collider.edgeRadius = 0f;
        collider.sharedMaterial = GetNoFrictionMaterial();
    }

    private Vector3 GetPlayerSpawnWorldPosition(Vector2Int spawnCell)
    {
        Vector3 cellPosition = CellToWorld(spawnCell, -0.2f);
        cellPosition.y += tileSize * (0.5f + platformThickness);
        return cellPosition;
    }

    private void SpawnMarker(Vector2Int cell, Color color, string objectName)
    {
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
        float x = (cell.x - mapOffset.x) * tileSize;
        float y = (cell.y - mapOffset.y) * tileSize;

        return new Vector3(x, y, zPosition);
    }

    private float GetMapBottomWorldY()
    {
        return -mapOffset.y * tileSize;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    private static PhysicsMaterial2D GetNoFrictionMaterial()
    {
        if (noFrictionMaterial != null)
        {
            return noFrictionMaterial;
        }

        noFrictionMaterial = new PhysicsMaterial2D("NoFriction")
        {
            friction = 0f,
            bounciness = 0f
        };

        return noFrictionMaterial;
    }
}
