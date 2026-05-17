
/// <summary>
/// AI가 만든 실제 맵 데이터입니다.
/// JSON 형태의 맵 데이터를 임시로 담도록 합니다.
/// </summary>

[System.Serializable]
public class AiMapLayoutDto
{
    public int width;
    public int height;
    public int[] tiles;
    public AiVector2IntDto player1Spawn;
    public AiVector2IntDto player2Spawn;
}
