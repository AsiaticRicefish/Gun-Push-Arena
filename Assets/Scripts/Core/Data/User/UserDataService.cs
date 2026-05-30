using UnityEngine;
using Firebase.Firestore;
using System.Collections.Generic;
using System.Threading.Tasks;

public class UserDataService
{
    private const int MinNicknameLength = 2;
    private const int MaxNicknameLength = 12;
    private const string UsersCollection = "users";
    private const string NicknamesCollection = "nicknames";

    private FirebaseFirestore firestore;

    public UserDataService()
    {
        firestore = FirebaseFirestore.DefaultInstance;
    }

    public async Task<UserData> GetUserDataAsync(string uid)
    {
        try
        {
            DocumentReference docRef = firestore.Collection(UsersCollection).Document(uid);
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
                NormalizedNickname = "",
                ColorHex = CreateDefaultColorHex(uid),
                IsNicknameSet = false,
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

    public async Task<NicknameUpdateResult> UpdateNicknameAsync(string uid, string nickname)
    {
        string normalizedNickname = NormalizeNickname(nickname);
        if (!IsValidNickname(nickname, normalizedNickname))
        {
            return NicknameUpdateResult.Invalid;
        }

        try
        {
            DocumentReference userRef = firestore.Collection(UsersCollection).Document(uid);
            DocumentReference nicknameRef = firestore.Collection(NicknamesCollection).Document(normalizedNickname);

            bool updated = await firestore.RunTransactionAsync(async transaction =>
            {
                DocumentSnapshot userSnapshot = await transaction.GetSnapshotAsync(userRef);
                if (!userSnapshot.Exists)
                {
                    return false;
                }

                UserData userData = userSnapshot.ConvertTo<UserData>();
                string previousNormalizedNickname = NormalizeNickname(userData.NormalizedNickname);

                if (string.IsNullOrEmpty(previousNormalizedNickname))
                {
                    previousNormalizedNickname = NormalizeNickname(userData.Nickname);
                }

                DocumentSnapshot nicknameSnapshot = await transaction.GetSnapshotAsync(nicknameRef);
                if (nicknameSnapshot.Exists && !IsNicknameOwnedByUser(nicknameSnapshot, uid))
                {
                    return false;
                }

                DocumentReference previousNicknameRef = null;
                DocumentSnapshot previousNicknameSnapshot = null;
                if (!string.IsNullOrEmpty(previousNormalizedNickname) &&
                    previousNormalizedNickname != normalizedNickname)
                {
                    previousNicknameRef = firestore
                        .Collection(NicknamesCollection)
                        .Document(previousNormalizedNickname);
                    previousNicknameSnapshot = await transaction.GetSnapshotAsync(previousNicknameRef);
                }

                if (userData.IsNicknameSet && previousNormalizedNickname == normalizedNickname)
                {
                    Dictionary<string, object> sameNicknameUpdates = CreateNicknameUpdates(nickname, normalizedNickname);
                    transaction.Update(userRef, sameNicknameUpdates);
                    transaction.Set(nicknameRef, CreateNicknameReservation(uid, nickname));
                    return true;
                }

                Dictionary<string, object> updates = CreateNicknameUpdates(nickname, normalizedNickname);
                transaction.Update(userRef, updates);
                transaction.Set(nicknameRef, CreateNicknameReservation(uid, nickname));

                if (previousNicknameRef != null &&
                    previousNicknameSnapshot != null &&
                    previousNicknameSnapshot.Exists &&
                    IsNicknameOwnedByUser(previousNicknameSnapshot, uid))
                {
                    transaction.Delete(previousNicknameRef);
                }

                return true;
            });

            if (!updated)
            {
                return NicknameUpdateResult.Duplicate;
            }

            Debug.Log($"[UserDataService] Nickname updated: {nickname}");
            return NicknameUpdateResult.Success;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[UserDataService] Error updating nickname: {e.Message}");
            return NicknameUpdateResult.Failed;
        }
    }

    public static bool IsValidNickname(string nickname)
    {
        return IsValidNickname(nickname, NormalizeNickname(nickname));
    }

    public static string NormalizeNickname(string nickname)
    {
        return string.IsNullOrWhiteSpace(nickname)
            ? ""
            : nickname.Trim().ToLowerInvariant();
    }

    private static bool IsValidNickname(string nickname, string normalizedNickname)
    {
        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrEmpty(normalizedNickname))
        {
            return false;
        }

        string trimmedNickname = nickname.Trim();
        return trimmedNickname.Length >= MinNicknameLength && trimmedNickname.Length <= MaxNicknameLength;
    }

    private Dictionary<string, object> CreateNicknameUpdates(string nickname, string normalizedNickname)
    {
        return new Dictionary<string, object>
        {
            { "Nickname", nickname.Trim() },
            { "NormalizedNickname", normalizedNickname },
            { "IsNicknameSet", true },
            { "UpdatedAt", Timestamp.GetCurrentTimestamp() }
        };
    }

    private Dictionary<string, object> CreateNicknameReservation(string uid, string nickname)
    {
        return new Dictionary<string, object>
        {
            { "Uid", uid },
            { "Nickname", nickname.Trim() },
            { "UpdatedAt", Timestamp.GetCurrentTimestamp() }
        };
    }

    private bool IsNicknameOwnedByUser(DocumentSnapshot snapshot, string uid)
    {
        Dictionary<string, object> data = snapshot.ToDictionary();

        if (data != null &&
            data.TryGetValue("Uid", out object ownerUid) &&
            ownerUid is string ownerUidText)
        {
            return ownerUidText == uid;
        }

        return false;
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
