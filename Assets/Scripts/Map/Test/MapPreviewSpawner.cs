using UnityEngine;

public class MapPreviewSpawner : MonoBehaviour
{
    [Header("Validation")]
    [SerializeField] private MapValidationSettings validationSettings;

    [Header("Tile Visuals")]
    [SerializeField] private float tileSize = 1f;
    [SerializeField] private Color floorColor = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] private Color wallColor = new Color(0.1f, 0.1f, 0.1f);
    [SerializeField] private Color player1SpawnColor = Color.cyan;
    [SerializeField] private Color player2SpawnColor = Color.magenta;

    private void Start()
    {
        SpawnPreviewMap();
    }

    private void SpawnPreviewMap()
    {
        RandomMapGenerator randomGenerator = new RandomMapGenerator();
        DefaultMapGenerator defaultGenerator = new DefaultMapGenerator();
        MapLayoutValidator validator = new MapLayoutValidator(validationSettings);

        MapLayoutData layout = randomGenerator.Generate();

        if (!validator.Validate(layout, out string errorMessage))
        {
            Debug.LogWarning($"Random map validation failed. Use default map. Reason: {errorMessage}");
            layout = defaultGenerator.Generate();
        }

        ClearChildren();
        SpawnTiles(layout);
        SpawnMarker(layout.Player1Spawn, player1SpawnColor, "Player1Spawn");
        SpawnMarker(layout.Player2Spawn, player2SpawnColor, "Player2Spawn");
    }

    private void SpawnTiles(MapLayoutData layout)
    {
        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                MapTileType tileType = layout.GetTile(x, y);

                if (tileType == MapTileType.Empty)
                {
                    continue;
                }

                Color color = tileType == MapTileType.Wall ? wallColor : floorColor;
                string objectName = $"{tileType}_{x}_{y}";

                SpawnSquare(new Vector2Int(x, y), color, objectName, 1f);
            }
        }
    }

    private void SpawnMarker(Vector2Int cell, Color color, string objectName)
    {
        SpawnSquare(cell, color, objectName, 0.55f);
    }

    private void SpawnSquare(Vector2Int cell, Color color, string objectName, float scaleMultiplier)
    {
        GameObject square = GameObject.CreatePrimitive(PrimitiveType.Quad);
        square.name = objectName;
        square.transform.SetParent(transform);

        square.transform.position = CellToWorld(cell);
        square.transform.localScale = Vector3.one * tileSize * scaleMultiplier;

        Renderer renderer = square.GetComponent<Renderer>();
        renderer.material.color = color;
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        float x = cell.x * tileSize;
        float y = cell.y * tileSize;

        return new Vector3(x, y, 0f);
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }
}