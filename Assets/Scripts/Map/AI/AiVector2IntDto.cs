
using Firebase.Firestore;

/// <summary>
/// JSON에서 좌표를 받기 위한 작은 데이터 클래스입니다.
/// x, y 좌표를 JSON으로 주고받습니다.
/// </summary>
[FirestoreData]
[System.Serializable]
public class AiVector2IntDto
{
    [FirestoreProperty] public int x { get; set; }
    [FirestoreProperty] public int y { get; set; }
}
