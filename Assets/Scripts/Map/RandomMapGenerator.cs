using Unity.Android.Gradle.Manifest;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RandomMapGenerator
{
    // 생성할 맵의 기본 크기
    private const int DefaultWidth = 17;
    private const int DefaultHeight = 11;

    // 벽 생성 확률
    private const float WallChance = 0.12f;

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
        PlaceRandomWalls(layout);
        EnsureSafeSpawnArea(layout, layout.Player1Spawn);
        EnsureSafeSpawnArea(layout, layout.Player2Spawn);

        return layout;
    }

    // 맵을 빈 타일로 초기화
    private void FillEmpty(MapLayoutData layout)
    {
        for (int i = 0; i < layout.Tiles.Length; i++)
        {
            layout.Tiles[i] = MapTileType.Empty;
        }
    }

    // 맵 안쪽을 Floor로 채웁니다
    // x = 1부터 시작하고, layout.Width - 1 전까지만 반복하여 가장자리에는 벽이 생성될 수 있도록 합니다
    private void CreateArenaFloor(MapLayoutData layout)
    {
        for(int y = 1; y < layout.Height - 1; y++)
        {
            for(int x = 1; x < layout.Width - 1; x++)
            {
                SetTile(layout, x, y, MapTileType.Floor);
            }
        }
    }

#region 벽 생성 로직
    /// <summary>
    /// 맵에서 일정 확률로 벽을 생성
    /// </summary>
    /// <param name="layout"></param>
    private void PlaceRandomWalls(MapLayoutData layout)
    {
        for (int y = 2; y < layout.Height - 1; y++)
        {
            for(int x = 2; x < layout.Width - 2; x++)
            {
                if (IsNearSpawn(layout, x, y))
                {
                    continue; // 스폰 지점 근처에는 벽을 생성하지 않습니다
                }

                if (UnityEngine.Random.value <= WallChance) // 일정 확률로 벽을 생성합니다
                {
                    SetTile(layout, x, y, MapTileType.Wall);
                }
            }
        }
    }

    /// <summary>
    /// 스폰 지점에서 일정 거리 이내에는 벽이 생성되지 않도록 합니다.
    /// 현재 죄표가 플레이어 스폰 위치 근처인지 확인 필요
    /// 플레이어 1 또는 플레이어 2 스폰에서 거리 2 이하이면 true를 반환하여 벽을 생성하지 않습니다.
    /// </summary>
    /// <param name="layout"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    private bool IsNearSpawn(MapLayoutData layout, int x, int y)
    {
        Vector2Int position = new Vector2Int(x, y);
        return Vector2Int.Distance(position, layout.Player1Spawn) <= 2f || 
            Vector2Int.Distance(position, layout.Player2Spawn) <= 2f;
    }

#endregion

    /// <summary>
    /// 플레이어 스폰 위치 주변에는 무조건 바닥이 생성되어야 됨
    /// 스폰 위치를 중심으로 3x3 영역을 바닥으로 채워서 플레이어가 안전하게 스폰될 수 있도록 합니다.
    /// </summary>
    /// <param name="layout"></param>
    /// <param name="spawn"></param>
    private void EnsureSafeSpawnArea(MapLayoutData layout, Vector2Int spawn)
    {
        for (int y = spawn.y - 1; y <= spawn.y + 1; y++)
        {
            for (int x = spawn.x - 1; x <= spawn.x + 1; x++)
            {
                if (!layout.IsInBounds(x, y)) // 검사하는 좌표가 맵 범위를 벗어나는 경우 건너뛰기
                {
                    continue;
                }

                SetTile(layout, x, y, MapTileType.Floor); // 맵 안에 있는 좌표라면 해당 칸을 Floor로 바꿉니다
            }
        }
    }


    private void SetTile(MapLayoutData layout, int x, int y, MapTileType tileType)
    {
        if (layout.IsInBounds(x, y))
        {
            layout.Tiles[layout.GetIndex(x, y)] = tileType;
        }
    }

}