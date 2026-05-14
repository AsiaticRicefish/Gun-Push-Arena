using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// AI가 맵을 이상하게 생성해도 맵 레이아웃이 유효한지 검사하는 클래스입니다.
/// </summary>
public class MapLayoutValidator
{
    private readonly MapValidationSettings settings;

    public MapLayoutValidator(MapValidationSettings settings)
    {
        this.settings = settings;
    }

    public bool Validate(MapLayoutData layout, out string errorMessage)
    {
        if (settings == null)
        {
            errorMessage = "MapValidationSettings가 할당되지 않았습니다.";
            return false;
        }

        if(layout == null)
        {
            errorMessage = "MapLayoutData가 null입니다.";
            return false;
        }

        if (layout.Width < settings.MinWidth || layout.Width > settings.MaxWidth)
        {
            errorMessage = $"맵의 너비는 {settings.MinWidth} 이상 {settings.MaxWidth} 이하이어야 합니다. 현재: {layout.Width}";
            return false;
        }

        if (layout.Height < settings.MinHeight || layout.Height > settings.MaxHeight)
        {
            errorMessage = $"맵의 높이는 {settings.MinHeight} 이상 {settings.MaxHeight} 이하이어야 합니다. 현재: {layout.Height}";
            return false;
        }

        if (layout.Tiles == null)
        {
            errorMessage = "맵의 타일이 null입니다.";
            return false;
        }

        int expectedTileCount = layout.Width * layout.Height;

        if (layout.Tiles.Length != expectedTileCount)
        {
            errorMessage = $"타일 배열의 길이는 너비 * 높이와 같아야 합니다. 예상: {expectedTileCount}, 현재: {layout.Tiles.Length}";
            return false;
        }


        if (!IsSpawnVaild(layout, layout.Player1Spawn, out errorMessage))
        {
            errorMessage = $"플레이어 1 스폰 지점 오류: {errorMessage}";
            return false;
        }

        if (!IsSpawnVaild(layout, layout.Player2Spawn, out errorMessage))
        {
            errorMessage = $"플레이어 2 스폰 지점 오류: {errorMessage}";
            return false;
        }

       if (Vector2Int.Distance(layout.Player1Spawn, layout.Player2Spawn) < settings.MinSpawnDistance)
        {
            errorMessage = "플레이어 스폰지점의 거리가 너무 가깝습니다.";
            return false;
        }

        if (CountFloorTiles(layout) < settings.MinFloorCount)
        {
            errorMessage = "바닥 타일이 충분하지 않습니다.";
            return false;
        }

        if (settings.RequireSpawnPath && !SpawnsConnected(layout))
        {
            errorMessage = "플레이어 스폰 지점이 바닥을 따라 연결되어 있지 않습니다.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 스폰 지점이 맵 안에 있고, 바닥 타일 위에 있는지 검사합니다.
    /// </summary>
    /// <param name="layout"></param>
    /// <param name="spawn"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    private bool IsSpawnVaild(MapLayoutData layout, Vector2Int spawn,  out string errorMessage)
    {
        // 스폰이 맵 밖이면 실패
         if (!layout.IsInBounds(spawn.x, spawn.y))
        {
            errorMessage = $"스폰 지점은 맵 안에 있어야 합니다. 현재 스폰 위치: {spawn}";
            return false;
        }

        // 스폰 위치가 바닥이 아니면 실패
        if (layout.GetTile(spawn.x, spawn.y) != MapTileType.Floor)
        {
            errorMessage = $"스폰 지점은 바닥 타일에 있어야 합니다. 현재 스폰 위치: {spawn}";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 맵에 바닥이 몇 칸 있는지 세는 메서드
    /// </summary>
    /// <param name="layout"></param>
    /// <returns></returns>
    private int CountFloorTiles(MapLayoutData layout)
    {
        int count = 0;

        for (int i = 0; i < layout.Tiles.Length; i++)
        {
            if (layout.Tiles[i] == MapTileType.Floor)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 플레이어 1 스폰 위치에서 플레이어 2 스폰 위치까지 바닥을 따라 갈 수 있는지 검사
    /// Todo : 점프 요소나 순간 이동 요소를 추가할 수 있어 후에 수정 필요
    /// </summary>
    /// <param name="layout"></param>
    /// <returns></returns>
    private bool SpawnsConnected(MapLayoutData layout)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(layout.Player1Spawn);
        visited.Add(layout.Player1Spawn);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == layout.Player2Spawn)
            {
                return true; // 스폰 지점이 연결되어 있음
            }

            TryAddNeighbor(layout, current + Vector2Int.up, queue, visited);
            TryAddNeighbor(layout, current + Vector2Int.down, queue, visited);
            TryAddNeighbor(layout, current + Vector2Int.left, queue, visited);
            TryAddNeighbor(layout, current + Vector2Int.right, queue, visited);
        }

        return false;
    }


    /// <summary>
    /// 옆 칸이 갈 수 있는 칸이면 검사 목록에 추가
    /// </summary>
    /// <param name="layout"></param>
    /// <param name="position"></param>
    /// <param name="queue"></param>
    /// <param name="visited"></param>
    private void TryAddNeighbor(MapLayoutData layout, Vector2Int position, Queue<Vector2Int> queue, HashSet<Vector2Int> visited)
    {
        if (visited.Contains(position))
        {
            return; // 이미 방문한 위치는 무시
        }

        if (!layout.IsInBounds(position.x, position.y))
        {
            return; // 맵 범위를 벗어나는 경우 무시
        }

        if (layout.GetTile(position.x, position.y) != MapTileType.Floor)
        {
            return; // 바닥이 아닌 타일은 무시
        }

        visited.Add(position); // 방문하면 이미 확인한 칸으로 기록
        queue.Enqueue(position); // 다음에 확인할 칸 목록에 추가
    }

}