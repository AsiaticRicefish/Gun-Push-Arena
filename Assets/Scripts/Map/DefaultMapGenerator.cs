using UnityEngine;

// AI가 파싱을 실패하는 경우 보험용으로 만든 맵
public class DefaultMapGenerator
{
    private const int DefaultWidth = 17;
    private const int DefaultHeight = 11;

    public MapLayoutData Generate()
    {
        MapLayoutData layout = new MapLayoutData
        {
            Width = DefaultWidth,
            Height = DefaultHeight,
            Tiles = new MapTileType[DefaultWidth * DefaultHeight],
            Player1Spawn = new Vector2Int(3, DefaultHeight / 2),
            Player2Spawn = new Vector2Int(DefaultWidth - 4, DefaultHeight / 2)
        };

        FillEmpty(layout);
        CreateArenaFloor(layout);
        PlaceDefaultWalls(layout);
        EnsureSafeSpawnArea(layout, layout.Player1Spawn);
        EnsureSafeSpawnArea(layout, layout.Player2Spawn);

        return layout;
    }

    private void FillEmpty(MapLayoutData layout)
    {
        for (int i = 0; i < layout.Tiles.Length; i++)
        {
            layout.Tiles[i] = MapTileType.Empty;
        }
    }

    private void CreateArenaFloor(MapLayoutData layout)
    {
        for (int y = 1; y < layout.Height - 1; y++)
        {
            for (int x = 1; x < layout.Width - 1; x++)
            {
                SetTile(layout, x, y, MapTileType.Floor);
            }
        }
    }

    // 맵 중앙에 고정된 벽을 배치하여 기본적인 맵을 만듭니다.
    private void PlaceDefaultWalls(MapLayoutData layout)
    {
        int centerX = layout.Width / 2;
        int centerY = layout.Height / 2;

        SetTile(layout, centerX, centerY - 2, MapTileType.Wall);
        SetTile(layout, centerX, centerY + 2, MapTileType.Wall);
        SetTile(layout, centerX - 2, centerY, MapTileType.Wall);
        SetTile(layout, centerX + 2, centerY, MapTileType.Wall);

        SetTile(layout, centerX - 4, centerY - 2, MapTileType.Wall);
        SetTile(layout, centerX + 4, centerY + 2, MapTileType.Wall);
    }

    private void EnsureSafeSpawnArea(MapLayoutData layout, Vector2Int spawn)
    {
        for (int y = spawn.y - 1; y <= spawn.y + 1; y++)
        {
            for (int x = spawn.x - 1; x <= spawn.x + 1; x++)
            {
                if (!layout.IsInBounds(x, y))
                {
                    continue;
                }

                SetTile(layout, x, y, MapTileType.Floor);
            }
        }
    }

    private void SetTile(MapLayoutData layout, int x, int y, MapTileType tileType)
    {
        if (!layout.IsInBounds(x, y))
        {
            return;
        }

        layout.Tiles[layout.GetIndex(x, y)] = tileType;
    }
}