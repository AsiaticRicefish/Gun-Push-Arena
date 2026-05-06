using UnityEngine;
using Unity.Netcode;
using System;
using Unity.Collections;

/// <summary>
/// 플레이어의 네트워크 데이터를 정의하는 구조체입니다. 
/// INetworkSerializable 인터페이스를 구현하여 네트워크를 통해 데이터를 직렬화
/// </summary>
public struct PlayerNetworkData : INetworkSerializable
{
    public FixedString64Bytes Uid;
    public FixedString64Bytes Nickname;
    public FixedString64Bytes ColorHex;

    // 네트워크를 통해 데이터를 직렬화하는 메서드입니다. 
    // BufferSerializer를 사용하여 각 필드를 직렬화합니다.
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        // 각 필드를 직렬화
        serializer.SerializeValue(ref Uid);
        serializer.SerializeValue(ref Nickname);
        serializer.SerializeValue(ref ColorHex);
    }

}
