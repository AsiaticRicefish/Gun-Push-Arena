using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// 맵 선택을 담당
/// 맵 데이터를 관리하고, 맵 ID로 검색하거나 랜덤으로 맵을 반환
/// </summary>
public class MapSelector : MonoBehaviour, IMapSelector
{
   [SerializeField] private MapData[] maps;

    public MapData[] Maps => maps;

    // 맵 ID로 맵 데이터를 검색
    public MapData GetMap(string mapId)
    {
        foreach (var map in maps)
        {
            if (map.Id == mapId)
            {
                return map;
            }
        }

        Debug.LogWarning($"{mapId}에 해당하는 맵을 찾을 수 없습니다.");
        return null;
    }

    // 랜덤으로 맵 데이터를 반환
    public MapData GetRandomMap()
    {
        if (maps == null || maps.Length == 0)
        {
            Debug.LogWarning("맵 데이터가 없습니다.");
            return null;
        }

        int index = Random.Range(0, maps.Length);
        return maps[index];
    }
}