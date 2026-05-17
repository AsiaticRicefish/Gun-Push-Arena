
/// <summary>
/// 방장이 어떤 맵을 원하는지 서버에 요청
/// Unity가 Firebase Function에 보낼 요청 데이터입니다.
/// </summary>

[System.Serializable]
public class AiMapGenerateRequest
{
    public string roomId;
    public string prompt;
    public int width;
    public int height;
    public int playerCount;
}