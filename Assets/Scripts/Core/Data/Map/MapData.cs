using UnityEngine;

[CreateAssetMenu(fileName = "MapData", menuName = "Game/MapData")]
public class MapData : ScriptableObject
{
    // 외부 코드에서는 읽기만 가능하고 런타임 중 실수로 데이터 변경 방지하도록 설정
    [SerializeField] private string id;
    [SerializeField] private string mapName;
    [SerializeField] private string sceneName;
    [SerializeField] private Sprite mapImage;

    public string Id => id;
    public string MapName => mapName;
    public string SceneName => sceneName;
    public Sprite MapImage => mapImage;
}