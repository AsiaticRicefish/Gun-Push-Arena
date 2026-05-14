using UnityEngine;

/// <summary>
/// 맵 생성 데이터
/// </summary>
public class MapLayoutData
{
    public int Width; // 맵의 가로 크기
    public int Height; // 맵의 세로 크기
    public MapTileType[] Tiles;
    public Vector2Int Player1Spawn;
    public Vector2Int Player2Spawn;

    // 좌표를 인덱스로 변환
    public int GetIndex(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            throw new System.ArgumentOutOfRangeException($"Coordinates are out of bounds: ({x}, {y})");
        }

        return y * Width + x;
    }

    // 현재 좌표가 맵 범위 내에 있는지 확인
    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    public MapTileType GetTile(int x, int y)
    {
        return Tiles[GetIndex(x, y)];
    }
}