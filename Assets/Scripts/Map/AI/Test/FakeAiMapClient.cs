using UnityEngine;

public class FakeAiMapClient : IAiMapClient
{
    public AiMapGenerateResponse GenerateMap(AiMapGenerateRequest request)
    {
        return new AiMapGenerateResponse
        {
            success = true,
            errorMessage = string.Empty,
            map = CreateSampleMap(request?.prompt)
        };
    }

    private AiMapLayoutDto CreateSampleMap(string prompt)
    {
        string normalizedPrompt = string.IsNullOrEmpty(prompt)
            ? string.Empty
            : prompt.ToLower();

        if (normalizedPrompt.Contains("bridge"))
        {
            return CreateBridgeMap();
        }

        if (normalizedPrompt.Contains("island"))
        {
            return CreateIslandMap();
        }
        
        return CreateDefaultSampleMap();
    }

    private AiMapLayoutDto CreateBridgeMap()
    {
        int width = 17;
        int height = 11;

        int[] tiles = CreateEmptyTiles(width, height);

        SetRectFloor(tiles, width, 2, 6, 3, 7);
        SetRectFloor(tiles, width, 10, 14, 3, 7);
        SetRectFloor(tiles, width, 6, 10, 5, 5);

        SetEmpty(tiles, width, 3, 6);
        SetEmpty(tiles, width, 4, 6);
        SetEmpty(tiles, width, 7, 4);
        SetEmpty(tiles, width, 9, 6);
        SetEmpty(tiles, width, 12, 6);
        SetEmpty(tiles, width, 13, 6);

        SetFloor(tiles, width, 3, 5);
        SetFloor(tiles, width, 13, 5);

        return CreateLayoutDto(width, height, tiles);
    }

    private AiMapLayoutDto CreateIslandMap()
    {
        int width = 17;
        int height = 11;

        int[] tiles = CreateEmptyTiles(width, height);

        SetRectFloor(tiles, width, 2, 6, 3, 7);
        SetRectFloor(tiles, width, 10, 14, 3, 7);
        SetRectFloor(tiles, width, 7, 9, 4, 6);

        SetFloor(tiles, width, 6, 5);
        SetFloor(tiles, width, 10, 5);

        SetEmpty(tiles, width, 3, 6);
        SetEmpty(tiles, width, 4, 6);
        SetEmpty(tiles, width, 5, 4);
        SetEmpty(tiles, width, 8, 5);
        SetEmpty(tiles, width, 11, 4);
        SetEmpty(tiles, width, 12, 6);
        SetEmpty(tiles, width, 13, 6);

        SetFloor(tiles, width, 3, 5);
        SetFloor(tiles, width, 13, 5);

        return CreateLayoutDto(width, height, tiles);
    }

    private int[] CreateEmptyTiles(int width, int height)
    {
        int[] tiles = new int[width * height];

        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] = 0;
        }

        return tiles;
    }

    private AiMapLayoutDto CreateLayoutDto(int width, int height, int[] tiles)
    {
        return new AiMapLayoutDto
        {
            width = width,
            height = height,
            tiles = tiles,
            player1Spawn = new AiVector2IntDto { x = 3, y = 5 },
            player2Spawn = new AiVector2IntDto { x = 13, y = 5 }
        };
    }


    private AiMapLayoutDto CreateDefaultSampleMap()
    {
        int width = 17;
        int height = 11;

        int[] tiles = new int[width * height];

        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] = 0;
        }

        SetRectFloor(tiles, width, 3, 13, 3, 7);

        SetEmpty(tiles, width, 8, 4);
        SetEmpty(tiles, width, 10, 4);
        SetEmpty(tiles, width, 6, 6);
        SetEmpty(tiles, width, 8, 6);

        SetEmpty(tiles, width, 3, 6);
        SetEmpty(tiles, width, 4, 6);
        SetEmpty(tiles, width, 12, 6);
        SetEmpty(tiles, width, 13, 6);

        SetFloor(tiles, width, 3, 5);
        SetFloor(tiles, width, 13, 5);

        return new AiMapLayoutDto
        {
            width = width,
            height = height,
            tiles = tiles,
            player1Spawn = new AiVector2IntDto { x = 3, y = 5 },
            player2Spawn = new AiVector2IntDto { x = 13, y = 5 }
        };
    }

    private void SetRectFloor(int[] tiles, int width, int startX, int endX, int startY, int endY)
    {
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                SetFloor(tiles, width, x, y);
            }
        }
    }

    private void SetFloor(int[] tiles, int width, int x, int y)
    {
        tiles[y * width + x] = 1;
    }

    private void SetEmpty(int[] tiles, int width, int x, int y)
    {
        tiles[y * width + x] = 0;
    }
}
