using UnityEngine;
using System.Threading.Tasks;

public class MapPreviewSpawner : MonoBehaviour
{
    private enum PreviewMapSource
    {
        Random,
        FakeAi,
        Ollama,
        Default
    }

    [SerializeField] private PreviewMapSource previewMapSource = PreviewMapSource.Random;

    [Header("AI Preview")]
    [SerializeField] private AiMapTheme aiMapTheme = AiMapTheme.Bridge;
    [SerializeField] private string aiStyleHint = "";

    [Header("Validation")]
    [SerializeField] private MapValidationSettings validationSettings;

    [Header("Tile Visuals")]
    [SerializeField] private float tileSize = 1f;
    [SerializeField] private Color floorColor = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] private Color wallColor = new Color(0.1f, 0.1f, 0.1f);
    [SerializeField] private Color player1SpawnColor = Color.cyan;
    [SerializeField] private Color player2SpawnColor = Color.magenta;

    private Vector2 mapOffset;
    private Sprite squareSprite;

    private async void Start()
    {
        await SpawnPreviewMapAsync();
    }

    private async Task SpawnPreviewMapAsync()
    {
        DefaultMapGenerator defaultGenerator = new DefaultMapGenerator();
        MapLayoutValidator validator = new MapLayoutValidator(validationSettings);

        IMapGenerator generator = CreatePreviewGenerator(validator, defaultGenerator);

        MapLayoutData layout;

        if (generator is AiMapGenerator aiGenerator)
        {
            layout = await aiGenerator.GenerateAsync();
        }
        else
        {
            layout = generator.Generate();
        }

        if (!validator.Validate(layout, out string errorMessage))
        {
            Debug.LogWarning($"Preview map validation failed. Use default map. Reason: {errorMessage}");
            layout = defaultGenerator.Generate();
        }

        ClearChildren();
        mapOffset = new Vector2((layout.Width - 1) * 0.5f, (layout.Height - 1) * 0.5f);
        SpawnTiles(layout);
        SpawnMarker(layout.Player1Spawn, player1SpawnColor, "Player1Spawn");
        SpawnMarker(layout.Player2Spawn, player2SpawnColor, "Player2Spawn");
    }

    private IMapGenerator CreatePreviewGenerator(
    MapLayoutValidator validator,
    DefaultMapGenerator defaultGenerator)
    {
        switch (previewMapSource)
        {
            case PreviewMapSource.FakeAi:
                AiMapGenerateRequest request = new AiMapGenerateRequest
                {
                    roomId = "preview_room",
                    prompt = aiStyleHint,
                    width = 17,
                    height = 11,
                    playerCount = 2
                };

                return new AiMapGenerator(
                    new FakeAiMapClient(),
                    validator,
                    defaultGenerator,
                    request);

            case PreviewMapSource.Ollama:
                AiMapGenerateRequest ollamaRequest = new AiMapGenerateRequest
                {
                    roomId = "preview_room",
                    prompt = aiStyleHint,
                    width = 17,
                    height = 11,
                    playerCount = 2
                };

                return new AiMapGenerator(
                    new OllamaAiMapClient(aiMapTheme),
                    validator,
                    defaultGenerator,
                    ollamaRequest);

            case PreviewMapSource.Default:
                return defaultGenerator;

            default:
                return new RandomMapGenerator();
        }
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
                float zPosition = tileType == MapTileType.Wall ? -0.1f : 0f;

                SpawnSquare(new Vector2Int(x, y), color, objectName, 1f, zPosition);
            }
        }
    }

    private void SpawnMarker(Vector2Int cell, Color color, string objectName)
    {
        SpawnSquare(cell, color, objectName, 0.55f, -0.2f);
    }

    private void SpawnSquare(Vector2Int cell, Color color, string objectName, float scaleMultiplier, float zPosition)
    {
        GameObject square = new GameObject(objectName);
        square.transform.SetParent(transform);

        square.transform.position = CellToWorld(cell, zPosition);
        square.transform.localScale = Vector3.one * tileSize * scaleMultiplier;

        SpriteRenderer spriteRenderer = square.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetSquareSprite();
        spriteRenderer.color = color;
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

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }
}
