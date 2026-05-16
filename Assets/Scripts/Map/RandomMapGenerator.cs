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

    private const int FloorExpandSteps = 35; // 바닥 확장을 몇 번 시도할지
    private const float FloorExpandChance = 0.65f; // 후보 위치를 실제 Floor로 바꿀 확률


    // 구멍을 의도적으로 내는 역할
    private const int FallHoleAttempts = 8; // 구멍 생성을 몇 번 시도할지
    private const float FallHoleChance = 0.7f; // 선택한 Floor를 실제로 Empty로 바꿀 확률
    private const int SpawnProtectRadius = 2; // 스폰 주변 몇 칸까지 구멍 생성을 막을지

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
        CreateCenterFloor(layout);
        ExpandFoor(layout);
        CreateFallHoles(layout);
        EnsureSpawnFloor(layout, layout.Player1Spawn);
        EnsureSpawnFloor(layout, layout.Player2Spawn);
        EnsureSpawnHeadroom(layout, layout.Player1Spawn, 1);
        EnsureSpawnHeadroom(layout, layout.Player2Spawn, -1);

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

    /// <summary>
    /// 중앙에 작은 기본 발판을 생성합니다
    /// </summary>
    /// <param name="layout"></param>
    private void CreateCenterFloor(MapLayoutData layout)
    {
        int centerX = layout.Width / 2;
        int centerY = layout.Height / 2;

        int halfWidth = 4;
        int halfHeight = 2;

        // 맵 중앙 좌표에서 왼쪽/오른쪽으로 halfWidth만큼, 아래/위로 halfHeight만큼 Floor를 채우기
        for (int y = centerY - halfHeight; y <= centerY + halfHeight; y++)
        {
            for (int x = centerX - halfWidth; x <= centerX + halfWidth; x++)
            {
                SetTile(layout, x, y, MapTileType.Floor);
            }
        }
    }

    #region Floor 확장 로직
    /// <summary>
    /// 기존 Floor 주변에 새로운 Floor를 랜덤하게 추가하여 발판 영역을 확장
    /// </summary>
    /// <param name="layout"></param>
    private void ExpandFoor(MapLayoutData layout)
    {
        for (int i = 0; i < FloorExpandSteps; i++)
        {
            int x = UnityEngine.Random.Range(1, layout.Width - 1);
            int y = UnityEngine.Random.Range(1, layout.Height - 1);

            if (layout.GetTile(x, y) != MapTileType.Empty)
            {
                continue;
            }

            if (!HasNeighborFloor(layout, x, y))
            {
                continue;
            }

            if (UnityEngine.Random.value > FloorExpandChance)
            {
                continue;
            }

            SetTile(layout, x, y, MapTileType.Floor);


        }
    }

    /// <summary>
    /// 지정한 위치의 상하좌우 중 하나라도 Floor인지 확인
    /// </summary>
    /// <param name="layout"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    private bool HasNeighborFloor(MapLayoutData layout, int x, int y)
    {
        return IsFloor(layout, x + 1, y)
            || IsFloor(layout, x - 1, y)
            || IsFloor(layout, x, y + 1)
            || IsFloor(layout, x, y - 1);
    }

    /// <summary>
    /// 지정한 위치가 맵 안에 있고 Floor인지 확인
    /// </summary>
    /// <param name="layout"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    private bool IsFloor(MapLayoutData layout, int x, int y)
    {
        if (!layout.IsInBounds(x, y))
        {
            return false;
        }

        return layout.GetTile(x, y) == MapTileType.Floor;
    }
    #endregion

    #region 벽 생성 로직
    /// <summary>
    /// 맵에서 일정 확률로 벽을 생성
    /// </summary>
    /// <param name="layout"></param>
    private void PlaceRandomWalls(MapLayoutData layout)
    {
        for (int y = 2; y < layout.Height - 1; y++)
        {
            for (int x = 2; x < layout.Width - 2; x++)
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
    /// 기존 Floor 일부를 Empty로 바꿔 맵 안쪽에 낙사 구멍을 만듭니다.
    /// </summary>
    /// <param name="layout"></param>
    private void CreateFallHoles(MapLayoutData layout)
    {
        for (int i = 0; i < FallHoleAttempts; i++)
        {
            int x = UnityEngine.Random.Range(1, layout.Width - 1);
            int y = UnityEngine.Random.Range(1, layout.Height - 1);

            if (layout.GetTile(x, y) != MapTileType.Floor)
            {
                continue;
            }

            if (IsNearSpawn(layout, x, y, SpawnProtectRadius))
            {
                continue;
            }

            if (UnityEngine.Random.value > FallHoleChance)
            {
                continue;
            }

            SetTile(layout, x, y, MapTileType.Empty);
        }
    }

    private bool IsNearSpawn(MapLayoutData layout, int x, int y, float radius)
    {
        Vector2Int position = new Vector2Int(x, y);

        return Vector2Int.Distance(position, layout.Player1Spawn) <= radius ||
               Vector2Int.Distance(position, layout.Player2Spawn) <= radius;
    }


    // 플레이어가 서 있을 발밑 한 칸만 Floor로 보장
    private void EnsureSpawnFloor(MapLayoutData layout, Vector2Int spawn)
    {
        SetTile(layout, spawn.x, spawn.y, MapTileType.Floor);
    }

    // 스폰 머리 위 공간을 비우는 메서드
    private void EnsureSpawnHeadroom(MapLayoutData layout, Vector2Int spawn, int direction)
    {
        SetTile(layout, spawn.x, spawn.y + 1, MapTileType.Empty);
        SetTile(layout, spawn.x + direction, spawn.y + 1, MapTileType.Empty);
    }


    private void SetTile(MapLayoutData layout, int x, int y, MapTileType tileType)
    {
        if (layout.IsInBounds(x, y))
        {
            layout.Tiles[layout.GetIndex(x, y)] = tileType;
        }
    }

}