using UnityEngine;
using Firebase.Firestore;

[FirestoreData]
public class UserData
{
    [FirestoreProperty] public string Uid { get; set; }    
    [FirestoreProperty] public string Nickname { get; set; }
    [FirestoreProperty] public string NormalizedNickname { get; set; }
    [FirestoreProperty] public string ColorHex { get; set; }

    // 닉네임이 설정되었는지 여부를 나타냄
    [FirestoreProperty] public bool IsNicknameSet { get; set; }

    // Firestore에서 Timestamp는 DateTime과 호환되지 않으므로, Timestamp 타입으로 정의합니다.
    [FirestoreProperty] public Timestamp CreatedAt { get; set; }
    [FirestoreProperty] public Timestamp UpdatedAt { get; set; }
}
