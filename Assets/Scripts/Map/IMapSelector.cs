using System.Collections.Generic;
using UnityEngine;

public interface IMapSelector
{
    MapData[] Maps { get;}

    MapData GetMap(string mapId);
    MapData GetRandomMap();
}