using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Netcode 클라이언트가 Host에 접속할 때 NetworkConfig.ConnectionData로 보내는 최소 세션 정보입니다.
/// Host는 승인 단계에서 이 값을 읽어 Netcode clientId와 Firebase UserId를 연결합니다.
/// 이후 rooms/{roomId}/players의 SlotIndex를 찾아 올바른 위치에 PlayerObject를 스폰합니다.
/// </summary>
[Serializable]
public class NetworkConnectionPayload
{
    // 접속하려는 Firestore room 문서 ID입니다. 다른 방의 클라이언트가 섞이면 승인 단계에서 거절합니다.
    public string RoomId;

    // Firebase Auth UID입니다. Netcode clientId와 로비 player 문서를 연결하는 기준입니다.
    public string UserId;

    // JsonUtility.FromJson이 사용할 수 있도록 기본 생성자를 유지합니다.
    public NetworkConnectionPayload()
    {
    }

    public NetworkConnectionPayload(string roomId, string userId)
    {
        RoomId = roomId;
        UserId = userId;
    }

    public bool IsValid()
    {
        // 승인 단계에서는 최소한 roomId와 userId가 모두 있어야 접속을 허용합니다.
        return !string.IsNullOrWhiteSpace(RoomId) &&
               !string.IsNullOrWhiteSpace(UserId);
    }

    public byte[] ToBytes()
    {
        // Netcode ConnectionData는 byte[]이므로 JSON 문자열을 UTF8 바이트로 변환합니다.
        string json = JsonUtility.ToJson(this);
        return Encoding.UTF8.GetBytes(json);
    }

    public static bool TryFromBytes(byte[] bytes, out NetworkConnectionPayload payload, out string errorMessage)
    {
        payload = null;

        if (bytes == null || bytes.Length == 0)
        {
            errorMessage = "Connection payload is empty.";
            return false;
        }

        try
        {
            // 클라이언트가 보낸 UTF8 JSON을 다시 payload 객체로 복원합니다.
            string json = Encoding.UTF8.GetString(bytes);
            payload = JsonUtility.FromJson<NetworkConnectionPayload>(json);

            if (payload == null || !payload.IsValid())
            {
                errorMessage = "Connection payload is invalid.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
            return false;
        }
    }
}
