using UnityEngine;

public sealed class BalancedMapBuilder : IAiThemeMapBuilder
{
    public string Theme => "balanced";

    public AiMapLayoutDto Build(AiMapIntentDto intent, AiMapGenerateRequest request)
    {
        System.Random random = AiThemeMapBuilderUtility.CreateRandom(intent, 101);
        AiMapLayoutDto map = AiThemeMapBuilderUtility.CreateEmptyMap(request);
        AiThemeMapBuilderUtility.Fill(map, 0);

        int centerY = map.height / 2;
        int upperY = Mathf.Clamp(centerY + 2, 1, map.height - 2);
        int lowerY = Mathf.Clamp(centerY - 2, 1, map.height - 2);
        int centerX = map.width / 2;

        AddPlatform(map, 1, 5, centerY);
        AddPlatform(map, map.width - 6, map.width - 2, centerY);
        AddPlatform(map, centerX - 1, centerX + 1, centerY + random.Next(-1, 2));

        AddPlatform(map, 3, 7, upperY);
        AddPlatform(map, map.width - 8, map.width - 4, upperY);
        AddPlatform(map, centerX - 3, centerX + 3, lowerY);

        if (AiThemeMapBuilderUtility.IsLarge(intent))
        {
            AddPlatform(map, centerX - 2, centerX + 2, Mathf.Clamp(centerY + 1, 1, map.height - 2));
        }

        map.player1Spawn = new AiVector2IntDto { x = 3, y = centerY };
        map.player2Spawn = new AiVector2IntDto { x = map.width - 4, y = centerY };

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

    private void EnsureMinimumFloorCoverage(AiMapLayoutDto map, int centerX, int centerY)
    {
        const int targetFloorCount = 34;

        if (CountFloorTiles(map) >= targetFloorCount)
        {
            return;
        }

        AddPlatform(map, centerX - 4, centerX + 4, centerY);
        AddPlatform(map, 2, 7, Mathf.Clamp(centerY + 2, 1, map.height - 2));
        AddPlatform(map, map.width - 8, map.width - 3, Mathf.Clamp(centerY - 2, 1, map.height - 2));

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
