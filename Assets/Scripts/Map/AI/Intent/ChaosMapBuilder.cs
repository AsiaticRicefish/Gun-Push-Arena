using UnityEngine;

public sealed class ChaosMapBuilder : IAiThemeMapBuilder
{
    public string Theme => "chaos";

    public AiMapLayoutDto Build(AiMapIntentDto intent, AiMapGenerateRequest request)
    {
        System.Random random = AiThemeMapBuilderUtility.CreateRandom(intent, 409);
        AiMapLayoutDto map = AiThemeMapBuilderUtility.CreateEmptyMap(request);
        AiThemeMapBuilderUtility.Fill(map, 0);

        int centerX = map.width / 2;
        int centerY = map.height / 2;
        int platformCount = AiThemeMapBuilderUtility.IsLarge(intent) ? 10 : 8;
        int upperY = Mathf.Clamp(centerY + 3, 1, map.height - 2);
        int lowerY = Mathf.Clamp(centerY - 3, 1, map.height - 2);

        AddPlatform(map, 2, 5, centerY);
        AddPlatform(map, map.width - 6, map.width - 3, centerY);
        AddPlatform(map, centerX - 2, centerX + 2, centerY + random.Next(-2, 3));
        AddPlatform(map, 3, 7, lowerY);
        AddPlatform(map, map.width - 8, map.width - 4, upperY);
        AddPlatform(map, centerX - 4, centerX + 4, centerY);

        for (int i = 0; i < platformCount; i++)
        {
            int width = random.Next(3, 6);
            int x = random.Next(2, Mathf.Max(3, map.width - width - 1));
            int y = random.Next(2, Mathf.Max(3, map.height - 2));

            if (IsNearSpawnArea(map, x, y))
            {
                continue;
            }

            AddPlatform(map, x, x + width, y);
        }

        map.player1Spawn = new AiVector2IntDto { x = 3, y = centerY };
        map.player2Spawn = new AiVector2IntDto { x = map.width - 4, y = centerY };

        AiThemeMapBuilderUtility.ApplyDanger(map, intent);
        AiThemeMapBuilderUtility.ApplyWalls(map, intent);
        AiThemeMapBuilderUtility.EnsureSpawnRules(map);
        EnsureMinimumFloorCoverage(map, centerX, centerY);

        return map;
    }

    private void AddPlatform(AiMapLayoutDto map, int startX, int endX, int y)
    {
        AiThemeMapBuilderUtility.FillRect(
            map,
            Mathf.Clamp(startX, 1, map.width - 2),
            Mathf.Clamp(y, 1, map.height - 2),
            Mathf.Clamp(endX, 1, map.width - 2),
            Mathf.Clamp(y, 1, map.height - 2),
            1);
    }

    private bool IsNearSpawnArea(AiMapLayoutDto map, int x, int y)
    {
        int centerY = map.height / 2;
        return (x <= 5 && Mathf.Abs(y - centerY) <= 1) ||
               (x >= map.width - 6 && Mathf.Abs(y - centerY) <= 1);
    }

    private void EnsureMinimumFloorCoverage(AiMapLayoutDto map, int centerX, int centerY)
    {
        const int targetFloorCount = 34;

        if (CountFloorTiles(map) >= targetFloorCount)
        {
            return;
        }

        AddPlatform(map, centerX - 5, centerX + 5, centerY);
        AddPlatform(map, 2, 6, Mathf.Clamp(centerY + 2, 1, map.height - 2));
        AddPlatform(map, map.width - 7, map.width - 3, Mathf.Clamp(centerY - 2, 1, map.height - 2));

        for (int y = 2; y <= map.height - 3 && CountFloorTiles(map) < targetFloorCount; y += 2)
        {
            AddPlatform(map, centerX - 1, centerX + 1, y);
        }

        AiThemeMapBuilderUtility.EnsureSpawnRules(map);
    }

    private int CountFloorTiles(AiMapLayoutDto map)
    {
        int count = 0;

        for (int i = 0; i < map.tiles.Length; i++)
        {
            if (map.tiles[i] == 1)
            {
                count++;
            }
        }

        return count;
    }
}
