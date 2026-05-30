using UnityEngine;

public sealed class SplitMapBuilder : IAiThemeMapBuilder
{
    public string Theme => "split";

    public AiMapLayoutDto Build(AiMapIntentDto intent, AiMapGenerateRequest request)
    {
        System.Random random = AiThemeMapBuilderUtility.CreateRandom(intent, 203);
        AiMapLayoutDto map = AiThemeMapBuilderUtility.CreateEmptyMap(request);
        AiThemeMapBuilderUtility.Fill(map, 0);

        int centerX = map.width / 2;
        int centerY = map.height / 2;
        int highY = Mathf.Clamp(centerY + 3 + random.Next(-1, 2), 1, map.height - 2);
        int lowY = Mathf.Clamp(centerY - 3, 1, map.height - 2);

        AddPlatform(map, 2, 5, centerY);
        AddPlatform(map, map.width - 6, map.width - 3, centerY);
        AddPlatform(map, centerX - 2, centerX + 2, highY);
        AddPlatform(map, centerX - 2, centerX + 2, lowY);
        AddPlatform(map, 2, 6, lowY);
        AddPlatform(map, map.width - 7, map.width - 3, lowY);
        AddPlatform(map, centerX - 1, centerX + 1, Mathf.Clamp(centerY + 1, 1, map.height - 2));

        if (AiThemeMapBuilderUtility.IsLarge(intent))
        {
            AddPlatform(map, centerX - 3, centerX + 3, Mathf.Clamp(centerY - 1, 1, map.height - 2));
        }

        map.player1Spawn = new AiVector2IntDto { x = 3, y = centerY };
        map.player2Spawn = new AiVector2IntDto { x = map.width - 4, y = centerY };

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

        AddPlatform(map, centerX - 3, centerX + 3, centerY);
        AddPlatform(map, 2, 7, Mathf.Clamp(centerY - 2, 1, map.height - 2));
        AddPlatform(map, map.width - 8, map.width - 3, Mathf.Clamp(centerY + 2, 1, map.height - 2));

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
