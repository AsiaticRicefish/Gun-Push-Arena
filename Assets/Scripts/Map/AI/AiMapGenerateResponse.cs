/// <summary>
/// AI 맵 생성이 성공했는지, 성공했다면 맵 데이터가 무엇인지 전달
/// Firebase Function이 Unity에 돌려주는 응답 데이터입니다.
/// </summary>
[System.Serializable]
public class AiMapGenerateResponse
{
    public bool success;
    public AiMapLayoutDto map;
    public string errorMessage;
}