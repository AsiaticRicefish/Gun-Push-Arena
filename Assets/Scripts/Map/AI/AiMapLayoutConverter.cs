/// <summary>
/// 게임 내부에서 사용하는 MapLayoutData를 AI/Firestore 공유용 AiMapLayoutDto로 변환합니다.
/// AiMapLayoutParser가 DTO -> MapLayoutData 방향이라면, 이 클래스는 그 반대 방향을 담당합니다.
/// </summary>
public static class AiMapLayoutConverter
{
    public static AiMapLayoutDto ToDto(MapLayoutData layout)
    {
        // 변환할 맵이 없으면 저장할 DTO도 만들 수 없습니다.
        if (layout == null)
        {
            return null;
        }

        // MapLayoutData는 MapTileType enum 배열을 사용하지만,
        // DTO는 JSON/Firestore 저장이 쉬운 int 배열을 사용합니다.
        int[] tiles = new int[layout.Tiles.Length];

        for (int i = 0; i < layout.Tiles.Length; i++)
        {
            // Empty = 0, Floor = 1, Wall = 2 값을 그대로 저장합니다.
            tiles[i] = (int)layout.Tiles[i];
        }

        // 최종적으로 모든 클라이언트가 공유할 수 있는 DTO 형태로 포장합니다.
        return new AiMapLayoutDto
        {
            width = layout.Width,
            height = layout.Height,
            tiles = tiles,
            // Unity의 Vector2Int는 Firestore/JSON 공유 DTO로 직접 쓰지 않고,
            // x/y만 가진 AiVector2IntDto로 변환합니다.
            player1Spawn = new AiVector2IntDto
            {
                x = layout.Player1Spawn.x,
                y = layout.Player1Spawn.y
            },
            player2Spawn = new AiVector2IntDto
            {
                x = layout.Player2Spawn.x,
                y = layout.Player2Spawn.y
            }
        };
    }
}
