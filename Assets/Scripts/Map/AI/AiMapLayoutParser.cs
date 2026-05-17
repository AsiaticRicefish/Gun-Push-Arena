using UnityEngine;

/// <summary>
/// 실제 맵 데이터는 MapLayoutData를 사용하고 있습니다.
/// 그래서 중간 변환기 역할을 하는 것이 필요합니다.
/// 정리하면 AI/Firebase에서 받은 DTO를 실제 게임용 MapLayoutData로 변환
/// </summary>
public static class AiMapLayoutParser
{
    public static bool ToMapLayoutData(AiMapLayoutDto dto, out MapLayoutData layout, out string errorMessage)
    {
        layout = null;

        if (dto == null)
        {
            errorMessage = "AI 맵 데이터가 null입니다.";
            return false;
        }

        if (dto.tiles == null)
        {
            errorMessage = "AI 맵 타일 배열이 null입니다.";
            return false;
        }

        int expectedTileCount = dto.width * dto.height;

        if (dto.tiles.Length != expectedTileCount)
        {
            errorMessage = $"AI 맵 타일 개수가 올바르지 않습니다. 예상: {expectedTileCount}, 현재: {dto.tiles.Length}";
            return false;
        }

        if (dto.player1Spawn == null || dto.player2Spawn == null)
        {
            errorMessage = "AI 맵 스폰 좌표 데이터가 null입니다.";
            return false;
        }

        MapTileType[] tiles = new MapTileType[dto.tiles.Length];

        for (int i = 0; i < dto.tiles.Length; i++)
        {
            if (dto.tiles[i] < 0 || dto.tiles[i] > 2)
            {
                errorMessage = $"AI 맵에 잘못된 타일 값이 포함되어 있습니다. 인덱스: {i}, 값: {dto.tiles[i]}";
                return false;
            }

            tiles[i] = (MapTileType)dto.tiles[i];
        }

        layout = new MapLayoutData
        {
            Width = dto.width,
            Height = dto.height,
            Tiles = tiles,
            Player1Spawn = new Vector2Int(dto.player1Spawn.x, dto.player1Spawn.y),
            Player2Spawn = new Vector2Int(dto.player2Spawn.x, dto.player2Spawn.y)
        };

        errorMessage = string.Empty;
        return true;
    }
}