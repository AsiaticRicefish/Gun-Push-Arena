using UnityEngine;

public sealed class IslandThemeMapBuilder : IAiThemeMapBuilder
{
    public string Theme => "island";

    public AiMapLayoutDto Build(AiMapIntentDto intent, AiMapGenerateRequest request)
    {
        System.Random random = AiThemeMapBuilderUtility.CreateRandom(intent, 203);
        AiMapLayoutDto map = AiThemeMapBuilderUtility.CreateEmptyMap(request);
        AiThemeMapBuilderUtility.Fill(map, 0);

        int centerX = map.width / 2;
        int centerY = map.height / 2;
        int centerOffsetY = random.Next(-1, 2);

        AiThemeMapBuilderUtility.FillRect(map, 2, centerY - 1, 5, centerY + 1, 1);
        AiThemeMapBuilderUtility.FillRect(map, map.width - 6, centerY - 1, map.width - 3, centerY + 1, 1);
        AiThemeMapBuilderUtility.FillRect(map, centerX - 2, centerY - 2 + centerOffsetY, centerX + 2, centerY + centerOffsetY, 1);
        AiThemeMapBuilderUtility.FillRect(map, centerX - 1, centerY + 2 - centerOffsetY, centerX + 1, centerY + 2 - centerOffsetY, 1);

        AiThemeMapBuilderUtility.FillRect(map, 5, centerY + random.Next(-1, 1), centerX - 2, centerY, 1);
        AiThemeMapBuilderUtility.FillRect(map, centerX + 2, centerY, map.width - 6, centerY + random.Next(0, 2), 1);

        if (AiThemeMapBuilderUtility.Normalize(intent?.dangerLevel, "medium") == "high")
        {
            AiThemeMapBuilderUtility.SetTile(map, centerX, centerY, 0);
            AiThemeMapBuilderUtility.SetTile(map, centerX - 1, centerY - 1, 0);
            AiThemeMapBuilderUtility.SetTile(map, centerX + 1, centerY - 1, 0);
        }

        map.player1Spawn = new AiVector2IntDto { x = 3, y = centerY };
        map.player2Spawn = new AiVector2IntDto { x = map.width - 4, y = centerY };

        AiThemeMapBuilderUtility.ApplyDanger(map, intent);
        AiThemeMapBuilderUtility.ApplyWalls(map, intent);
        AiThemeMapBuilderUtility.EnsureSpawnRules(map);

        return map;
    }
}
