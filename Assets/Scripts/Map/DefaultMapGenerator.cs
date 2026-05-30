using UnityEngine;

public class DefaultMapGenerator : IMapGenerator
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
            Player1Spawn = new Vector2Int(3, 3),
            Player2Spawn = new Vector2Int(DefaultWidth - 4, 3)
        };

        FillEmpty(layout);
        CreatePlatformLayout(layout);
        CreateDefaultSpawnHeadroom(layout);
        EnsureSpawnFloor(layout, layout.Player1Spawn);
        EnsureSpawnFloor(layout, layout.Player2Spawn);

        return layout;
    }

    private void FillEmpty(MapLayoutData layout)
    {
        for (int i = 0; i < layout.Tiles.Length; i++)
        {
            layout.Tiles[i] = MapTileType.Empty;
        }
    }

    private void CreatePlatformLayout(MapLayoutData layout)
    {
        AddHorizontalPlatform(layout, 2, 14, 3);
        AddHorizontalPlatform(layout, 2, 7, 5);
        AddHorizontalPlatform(layout, 9, 14, 5);
        AddHorizontalPlatform(layout, 6, 10, 7);
    }

    private void CreateDefaultSpawnHeadroom(MapLayoutData layout)
    {
        SetTile(layout, layout.Player1Spawn.x, layout.Player1Spawn.y + 1, MapTileType.Empty);
        SetTile(layout, layout.Player1Spawn.x + 1, layout.Player1Spawn.y + 1, MapTileType.Empty);
        SetTile(layout, layout.Player2Spawn.x, layout.Player2Spawn.y + 1, MapTileType.Empty);
        SetTile(layout, layout.Player2Spawn.x - 1, layout.Player2Spawn.y + 1, MapTileType.Empty);
    }

    private void EnsureSpawnFloor(MapLayoutData layout, Vector2Int spawn)
    {
        SetTile(layout, spawn.x, spawn.y, MapTileType.Floor);
    }

    private void AddHorizontalPlatform(MapLayoutData layout, int startX, int endX, int y)
    {
        for (int x = startX; x <= endX; x++)
        {
            SetTile(layout, x, y, MapTileType.Floor);
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
