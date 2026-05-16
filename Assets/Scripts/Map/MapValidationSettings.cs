using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[CreateAssetMenu(fileName = "MapValidationSettings", menuName = "Gun Push Arena/Map Validation Settings")]
public class MapValidationSettings : ScriptableObject
{
    [Header("맵 사이즈")]
    [SerializeField] private int minWidth = 8;
    [SerializeField] private int maxWidth = 40;
    [SerializeField] private int minHeight = 6;
    [SerializeField] private int maxHeight = 25;

    [Header("맵이 플레이가 가능한 수준인지 판단")]
    [SerializeField] private int minFloorCount = 30; // 최소 바닥 칸 개수(게임이 정상적으로 플레이가 되는 수준의 타일은 나와야 함)
    [SerializeField] private float minSpawnDistance = 5f; // 플레이어 스폰 사이의 최소 거리
    [SerializeField] private bool requireSpawnPath = true; // 두 스폰 사이에 길이 있어야 한다.


    [Header("낙사 공간 생성에 대한 판단")]
    // 최소 빈곳의 개수를 파악해서 낙사공간이 실제로 충분히 있는지 확인
    [SerializeField] private int minEmptyCount = 25;

    // 밀려서 떨어질 수 있는 절벽 가장자리가 충분한지 확인
    [SerializeField] private int minFallEdgeCount = 20;

    // 맵이 다시 벽 중심으로 돌아가지 않게 방지
    [SerializeField] private int maxWallCount = 3;

    public int MaxWallCount => maxWallCount;
    public int MinFallEdgeCount => minFallEdgeCount;
    public int MinEmptyCount => minEmptyCount;
    public int MinWidth => minWidth;
    public int MaxWidth => maxWidth;
    public int MinHeight => minHeight;
    public int MaxHeight => maxHeight;
    public int MinFloorCount => minFloorCount;
    public float MinSpawnDistance => minSpawnDistance;
    public bool RequireSpawnPath => requireSpawnPath;
}