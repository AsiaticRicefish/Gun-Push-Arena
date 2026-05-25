
using Firebase.Firestore;

/// <summary>
/// AI가 만든 실제 맵 데이터입니다.
/// JSON 형태의 맵 데이터를 임시로 담도록 합니다.
/// </summary>

[FirestoreData]
[System.Serializable]
public class AiMapLayoutDto
{
    [FirestoreProperty] public int width { get; set; }
    [FirestoreProperty] public int height { get; set; }
    [FirestoreProperty] public int[] tiles { get; set; }
    [FirestoreProperty] public AiVector2IntDto player1Spawn { get; set; }
    [FirestoreProperty] public AiVector2IntDto player2Spawn { get; set; }
}
