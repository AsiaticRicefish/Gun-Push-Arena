using UnityEngine;

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
        CreateDefaultFloor(layout);
        CreateDefaultFallHoles(layout);
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

    private void CreateDefaultFloor(MapLayoutData layout)
    {
        int centerX = layout.Width / 2;
        int centerY = layout.Height / 2;

        for (int y = centerY - 2; y <= centerY + 2; y++)
        {
            for (int x = centerX - 5; x <= centerX + 5; x++)
            {
                SetTile(layout, x, y, MapTileType.Floor);
            }
        }

        for (int x = layout.Player1Spawn.x; x <= layout.Player2Spawn.x; x++)
        {
            SetTile(layout, x, centerY, MapTileType.Floor);
        }
    }

    private void CreateDefaultFallHoles(MapLayoutData layout)
    {
        int centerX = layout.Width / 2;
        int centerY = layout.Height / 2;

        SetTile(layout, centerX, centerY + 1, MapTileType.Empty);
        SetTile(layout, centerX, centerY - 1, MapTileType.Empty);
        SetTile(layout, centerX - 2, centerY + 1, MapTileType.Empty);
        SetTile(layout, centerX + 2, centerY - 1, MapTileType.Empty);
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

    private void SetTile(MapLayoutData layout, int x, int y, MapTileType tileType)
    {
        if (!layout.IsInBounds(x, y))
        {
            return;
        }

        layout.Tiles[layout.GetIndex(x, y)] = tileType;
    }
}