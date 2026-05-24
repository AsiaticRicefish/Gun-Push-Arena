using UnityEngine;

public sealed class WarehouseThemeMapBuilder : IAiThemeMapBuilder
{
    public string Theme => "warehouse";

    public AiMapLayoutDto Build(AiMapIntentDto intent, AiMapGenerateRequest request)
    {
        System.Random random = AiThemeMapBuilderUtility.CreateRandom(intent, 307);
        AiMapLayoutDto map = AiThemeMapBuilderUtility.CreateEmptyMap(request);
        AiThemeMapBuilderUtility.Fill(map, 0);

        int centerX = map.width / 2;
        int centerY = map.height / 2;
        int halfWidth = (AiThemeMapBuilderUtility.IsLarge(intent) ? 6 : 5) + random.Next(0, 2);
        int halfHeight = 2;

        AiThemeMapBuilderUtility.FillRect(map, centerX - halfWidth, centerY - halfHeight, centerX + halfWidth, centerY + halfHeight, 1);
        AiThemeMapBuilderUtility.FillRect(map, centerX - halfWidth + 1, centerY - halfHeight - 1, centerX - halfWidth + 4, centerY - halfHeight - 1, 1);
        AiThemeMapBuilderUtility.FillRect(map, centerX + halfWidth - 4, centerY + halfHeight + 1, centerX + halfWidth - 1, centerY + halfHeight + 1, 1);

        map.player1Spawn = new AiVector2IntDto { x = Mathf.Max(2, centerX - halfWidth + 1), y = centerY };
        map.player2Spawn = new AiVector2IntDto { x = Mathf.Min(map.width - 3, centerX + halfWidth - 1), y = centerY };

        PlaceWarehouseCover(map, centerX, centerY, random);
        AiThemeMapBuilderUtility.ApplyDanger(map, intent);
        AiThemeMapBuilderUtility.EnsureSpawnRules(map);

        return map;
    }

    private void PlaceWarehouseCover(AiMapLayoutDto map, int centerX, int centerY, System.Random random)
    {
        AiThemeMapBuilderUtility.SetTile(map, centerX - 3 + random.Next(-1, 1), centerY, 2);
        AiThemeMapBuilderUtility.SetTile(map, centerX, centerY + 1, 2);
        AiThemeMapBuilderUtility.SetTile(map, centerX + 3 + random.Next(0, 2), centerY, 2);
    }
}
