using System;
using UnityEngine;

public static class AiThemeMapBuilderUtility
{
    public static AiMapLayoutDto CreateEmptyMap(AiMapGenerateRequest request)
    {
        int width = Mathf.Max(8, request.width);
        int height = Mathf.Max(6, request.height);

        return new AiMapLayoutDto
        {
            width = width,
            height = height,
            tiles = new int[width * height],
            player1Spawn = new AiVector2IntDto(),
            player2Spawn = new AiVector2IntDto()
        };
    }

    public static void Fill(AiMapLayoutDto map, int tile)
    {
        for (int i = 0; i < map.tiles.Length; i++)
        {
            map.tiles[i] = tile;
        }
    }

    public static void FillRect(AiMapLayoutDto map, int minX, int minY, int maxX, int maxY, int tile)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                SetTile(map, x, y, tile);
            }
        }
    }

    public static void SetTile(AiMapLayoutDto map, int x, int y, int tile)
    {
        if (!IsInBounds(map, x, y))
        {
            return;
        }

        map.tiles[y * map.width + x] = tile;
    }

    public static int GetTile(AiMapLayoutDto map, int x, int y)
    {
        if (!IsInBounds(map, x, y))
        {
            return 0;
        }

        return map.tiles[y * map.width + x];
    }

    public static bool IsInBounds(AiMapLayoutDto map, int x, int y)
    {
        return x >= 0 && x < map.width && y >= 0 && y < map.height;
    }

    public static void EnsureStandable(AiMapLayoutDto map, int x, int y)
    {
        SetTile(map, x, y, 1);
        SetTile(map, x, y + 1, 0);
    }

    public static void EnsurePath(AiMapLayoutDto map, int startX, int endX, int y)
    {
        int minX = Mathf.Min(startX, endX);
        int maxX = Mathf.Max(startX, endX);

        for (int x = minX; x <= maxX; x++)
        {
            SetTile(map, x, y, 1);
        }
    }

    public static void EnsureSpawnRules(AiMapLayoutDto map)
    {
        EnsureStandable(map, map.player1Spawn.x, map.player1Spawn.y);
        EnsureStandable(map, map.player2Spawn.x, map.player2Spawn.y);
        EnsureStandable(map, map.player1Spawn.x + 1, map.player1Spawn.y);
        EnsureStandable(map, map.player2Spawn.x - 1, map.player2Spawn.y);

        if (map.player1Spawn.y == map.player2Spawn.y)
        {
            EnsurePath(map, map.player1Spawn.x, map.player2Spawn.x, map.player1Spawn.y);
        }
    }

    public static void ApplyDanger(AiMapLayoutDto map, AiMapIntentDto intent)
    {
        System.Random random = CreateRandom(intent, 17);
        string dangerLevel = Normalize(intent?.dangerLevel, "medium");
        int centerY = map.height / 2;
        int step = dangerLevel == "high" ? 2 : 3;

        if (dangerLevel == "low")
        {
            step = 4;
        }

        int startOffset = random.Next(0, step);

        for (int x = 2 + startOffset; x <= map.width - 3; x += step)
        {
            if (!IsNearSpawn(map, x, centerY))
            {
                int y = random.Next(0, 2) == 0 ? centerY + 1 : centerY - 1;
                SetTile(map, x, y, 0);
            }
        }

        if (dangerLevel == "high")
        {
            for (int x = 3 + random.Next(0, 2); x <= map.width - 4; x += 4)
            {
                if (!IsNearSpawn(map, x, centerY))
                {
                    SetTile(map, x, centerY - 1, 0);
                }
            }
        }
    }

    public static void ApplyWalls(AiMapLayoutDto map, AiMapIntentDto intent)
    {
        System.Random random = CreateRandom(intent, 31);
        string wallDensity = Normalize(intent?.wallDensity, "none");

        if (wallDensity == "none")
        {
            return;
        }

        int centerY = map.height / 2;
        int maxWalls = wallDensity == "medium" ? 3 : 2;
        int placed = 0;

        for (int x = 4 + random.Next(0, 3); x < map.width - 4 && placed < maxWalls; x += 3 + random.Next(0, 2))
        {
            if (GetTile(map, x, centerY) != 1 || IsNearSpawn(map, x, centerY))
            {
                continue;
            }

            int y = centerY + random.Next(-1, 2);
            if (GetTile(map, x, y) == 1 && !IsNearSpawn(map, x, y))
            {
                SetTile(map, x, y, 2);
            }

            placed++;
        }
    }

    public static bool IsLarge(AiMapIntentDto intent)
    {
        return Normalize(intent?.platformScale, "medium") == "large";
    }

    public static string Normalize(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant();
    }

    public static System.Random CreateRandom(AiMapIntentDto intent, int salt = 0)
    {
        int seed = intent != null && intent.seed != 0
            ? intent.seed
            : Environment.TickCount;

        int saltedSeed = seed + salt;

        if (saltedSeed == int.MinValue)
        {
            saltedSeed = 0;
        }

        return new System.Random(Mathf.Abs(saltedSeed));
    }

    private static bool IsNearSpawn(AiMapLayoutDto map, int x, int y)
    {
        return DistanceSquared(x, y, map.player1Spawn.x, map.player1Spawn.y) <= 2
            || DistanceSquared(x, y, map.player2Spawn.x, map.player2Spawn.y) <= 2;
    }

    private static int DistanceSquared(int ax, int ay, int bx, int by)
    {
        int dx = ax - bx;
        int dy = ay - by;
        return dx * dx + dy * dy;
    }
}
