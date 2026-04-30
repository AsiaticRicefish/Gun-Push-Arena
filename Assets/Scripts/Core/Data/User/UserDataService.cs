using UnityEngine;
using Firebase.Firestore;
using System.Threading.Tasks;

public class UserDataService
{
    private FirebaseFirestore firestore;

    public UserDataService()
    {
        firestore = FirebaseFirestore.DefaultInstance;
    }

    public async Task<UserData> GetUserDataAsync(string uid)
    {
        try
        {
            DocumentReference docRef = firestore.Collection("users").Document(uid);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                Debug.Log("[UserDataService] 기존 유저 데이터 로드");
                return snapshot.ConvertTo<UserData>();
            }

            Debug.Log("[UserDataService] 신규 유저 데이터 생성");

            UserData newUser = new UserData
            {
                Uid = uid,
                Nickname = CreateDefaultNickname(uid),
                ColorHex = CreateDefaultColorHex(uid),
                CreatedAt = Timestamp.GetCurrentTimestamp(),
                UpdatedAt = Timestamp.GetCurrentTimestamp()
            };

            await docRef.SetAsync(newUser);

            return newUser;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UserDataService] Error getting user data: {e}");
            return null;
        }

    }

    private string CreateDefaultNickname(string uid)
    {
        string shortUid = uid.Length >= 4 ? uid.Substring(0, 4) : uid;
        return $"Player_{shortUid}";
    }

    private string CreateDefaultColorHex(string uid)
    {
        int hash = uid.GetHashCode();

        byte r = (byte)(hash & 0xFF);
        byte g = (byte)((hash >> 8) & 0xFF);
        byte b = (byte)((hash >> 16) & 0xFF);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

}