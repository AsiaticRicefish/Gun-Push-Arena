using UnityEngine;

public sealed class BridgeThemeMapBuilder : IAiThemeMapBuilder
{
    public string Theme => "bridge";

    public AiMapLayoutDto Build(AiMapIntentDto intent, AiMapGenerateRequest request)
    {
        System.Random random = AiThemeMapBuilderUtility.CreateRandom(intent, 101);
        AiMapLayoutDto map = AiThemeMapBuilderUtility.CreateEmptyMap(request);
        AiThemeMapBuilderUtility.Fill(map, 0);

        int centerY = map.height / 2;
        int leftCenterX = 3;
        int rightCenterX = map.width - 4;
        int islandHalfWidth = (AiThemeMapBuilderUtility.IsLarge(intent) ? 3 : 2) + random.Next(0, 2);
        int islandHalfHeight = AiThemeMapBuilderUtility.IsLarge(intent) || random.Next(0, 3) == 0 ? 2 : 1;

        AiThemeMapBuilderUtility.FillRect(map, leftCenterX - islandHalfWidth, centerY - islandHalfHeight, leftCenterX + islandHalfWidth, centerY + islandHalfHeight, 1);
        AiThemeMapBuilderUtility.FillRect(map, rightCenterX - islandHalfWidth, centerY - islandHalfHeight, rightCenterX + islandHalfWidth, centerY + islandHalfHeight, 1);

        int bridgeCount = Mathf.Clamp(intent?.bridgeCount ?? 1, 1, 3);
        int[] offsets = GetBridgeOffsets(bridgeCount);

        for (int i = 0; i < offsets.Length; i++)
        {
            int jitter = random.Next(0, 4) == 0 ? random.Next(-1, 2) : 0;
            int y = Mathf.Clamp(centerY + offsets[i] + jitter, 1, map.height - 2);
            AiThemeMapBuilderUtility.FillRect(map, leftCenterX + islandHalfWidth, y, rightCenterX - islandHalfWidth, y, 1);
        }

        map.player1Spawn = new AiVector2IntDto { x = leftCenterX, y = centerY };
        map.player2Spawn = new AiVector2IntDto { x = rightCenterX, y = centerY };

        AiThemeMapBuilderUtility.ApplyDanger(map, intent);
        AiThemeMapBuilderUtility.ApplyWalls(map, intent);
        AiThemeMapBuilderUtility.EnsureSpawnRules(map);

        return map;
    }

    private int[] GetBridgeOffsets(int bridgeCount)
    {
        if (bridgeCount >= 3)
        {
            return new[] { -2, 0, 2 };
        }

        if (bridgeCount == 2)
        {
            return new[] { -1, 1 };
        }

        return new[] { 0 };
    }
}