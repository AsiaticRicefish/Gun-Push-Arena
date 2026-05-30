using UnityEngine;

public sealed class VerticalMapBuilder : IAiThemeMapBuilder
{
    public string Theme => "vertical";

    public AiMapLayoutDto Build(AiMapIntentDto intent, AiMapGenerateRequest request)
    {
        System.Random random = AiThemeMapBuilderUtility.CreateRandom(intent, 307);
        AiMapLayoutDto map = AiThemeMapBuilderUtility.CreateEmptyMap(request);
        AiThemeMapBuilderUtility.Fill(map, 0);

        int centerX = map.width / 2;
        int centerY = map.height / 2;
        int halfWidth = AiThemeMapBuilderUtility.IsLarge(intent) ? 7 : 6;
        int upperY = Mathf.Clamp(centerY + 3, 1, map.height - 2);
        int lowerY = Mathf.Clamp(centerY - 3, 1, map.height - 2);

        AddPlatform(map, centerX - halfWidth, centerX + halfWidth, centerY);
        AddPlatform(map, centerX - halfWidth + 1, centerX - halfWidth + 5, upperY);
        AddPlatform(map, centerX + halfWidth - 5, centerX + halfWidth - 1, upperY);
        AddPlatform(map, centerX - 3, centerX + 3, lowerY);
        AddPlatform(map, centerX - 2, centerX + 2, Mathf.Clamp(centerY - 1, 1, map.height - 2));

        map.player1Spawn = new AiVector2IntDto { x = Mathf.Max(2, centerX - halfWidth + 1), y = centerY };
        map.player2Spawn = new AiVector2IntDto { x = Mathf.Min(map.width - 3, centerX + halfWidth - 1), y = centerY };

        PlaceCenterCover(map, centerX, centerY, random);
        AiThemeMapBuilderUtility.EnsureSpawnRules(map);

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

    private void PlaceCenterCover(AiMapLayoutDto map, int centerX, int centerY, System.Random random)
    {
        AiThemeMapBuilderUtility.SetTile(map, centerX - 4 + random.Next(-1, 2), centerY, 2);
        AiThemeMapBuilderUtility.SetTile(map, centerX, centerY, 2);
        AiThemeMapBuilderUtility.SetTile(map, centerX + 4 + random.Next(-1, 2), centerY, 2);
    }
}
