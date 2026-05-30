using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// IRoomRepository에 정의된 방 기능을 Firestore로 실제 구현하는 클래스입니다.
/// UI나 RoomService는 Firestore API를 직접 알 필요 없이 이 클래스를 통해 방 데이터를 읽고 씁니다.
/// </summary>
public class FirestoreRoomRepository : IRoomRepository
{
    // Firestore 최상위 컬렉션 이름입니다.
    // 실제 방 문서 경로는 rooms/{roomId} 형태가 됩니다.
    private const string RoomsCollection = "rooms";

    // room 문서 아래에 들어가는 플레이어 하위 컬렉션 이름입니다.
    // 실제 플레이어 문서 경로는 rooms/{roomId}/players/{userId} 형태가 됩니다.
    private const string PlayersCollection = "players";

    private readonly FirebaseFirestore firestore;

    public FirestoreRoomRepository()
    {
        // FirebaseManager에서 Firebase 초기화가 끝난 뒤 사용하는 것을 전제로 합니다.
        // DefaultInstance는 현재 Firebase 앱의 Firestore 진입점입니다.
        firestore = FirebaseFirestore.DefaultInstance;
    }

    public async Task<RoomState> CreateRoomAsync(
       string hostUserId,
       string nickname,
       string colorHex,
       int maxPlayers)
    {
        // 방 코드는 유저가 입력/공유하는 값이므로, Firestore에 같은 문서가 없는 코드만 사용합니다.
        string roomId = await CreateUniqueRoomIdAsync();

        // rooms/{roomId} 문서를 가리키는 참조입니다.
        // 참조만 만든 상태이며, 아직 Firestore에 데이터가 쓰인 것은 아닙니다.
        DocumentReference roomRef = firestore.Collection(RoomsCollection).Document(roomId);

        // rooms/{roomId}/players/{hostUserId} 문서를 가리키는 참조입니다.
        // 방 생성과 동시에 방장 플레이어 문서도 같이 만들기 위해 사용합니다.
        DocumentReference hostPlayerRef = roomRef.Collection(PlayersCollection).Document(hostUserId);

        // 프로토타입에서는 클라이언트에서 만든 Timestamp를 사용합니다.
        // 더 엄격한 서버 시간 기준이 필요하면 FieldValue.ServerTimestamp를 고려할 수 있습니다.
        Timestamp now = Timestamp.GetCurrentTimestamp();

        // rooms/{roomId}에 저장할 방 전체 상태입니다.
        RoomState room = new RoomState
        {
            RoomId = roomId,
            HostUserId = hostUserId,
            Status = RoomStatus.Waiting.ToString(),
            MaxPlayers = maxPlayers,
            PlayerCount = 1,
            SelectedTheme = AiMapTheme.Balanced.ToString(),
            MapWidth = 17,
            MapHeight = 11,
            MapVersion = 0,
            FinalMap = null,
            RelayJoinCode = "",
            CreatedAt = now,
            UpdatedAt = now
        };

        // rooms/{roomId}/players/{hostUserId}에 저장할 방장 플레이어 상태입니다.
        RoomPlayerState hostPlayer = new RoomPlayerState
        {
            UserId = hostUserId,
            Nickname = nickname,
            ColorHex = colorHex,
            IsHost = true,
            IsReady = true,
            SlotIndex = 0,
            JoinedAt = now,
            LastSeenAt = now
        };

        // batch는 여러 쓰기 작업을 하나의 묶음으로 커밋합니다.
        // 여기서는 "방 문서 생성"과 "방장 플레이어 문서 생성"이 같이 성공하거나 같이 실패합니다.
        WriteBatch batch = firestore.StartBatch();
        batch.Set(roomRef, room);
        batch.Set(hostPlayerRef, hostPlayer);

        // CommitAsync가 호출되어야 실제 Firestore 쓰기 요청이 전송됩니다.
        await batch.CommitAsync();

        return room;
    }

    public async Task<bool> JoinRoomAsync(
        string roomId,
        string userId,
        string nickname,
        string colorHex)
    {
        // 참가하려는 방 문서와, 내 플레이어 문서 위치를 먼저 참조로 잡습니다.
        DocumentReference roomRef = firestore.Collection(RoomsCollection).Document(roomId);
        DocumentReference playerRef = roomRef.Collection(PlayersCollection).Document(userId);

        // GetSnapshotAsync는 현재 Firestore에 저장된 문서를 한 번 읽어옵니다.
        DocumentSnapshot roomSnapshot = await roomRef.GetSnapshotAsync();
        if (!roomSnapshot.Exists)
        {
            Debug.LogWarning($"[Room] Room not found: {roomId}");
            return false;
        }

        // Firestore 문서를 RoomState 클래스로 변환합니다.
        // RoomState에는 [FirestoreData], 필드에는 [FirestoreProperty]가 필요합니다.
        RoomState room = roomSnapshot.ConvertTo<RoomState>();

        // 이미 닫혔거나 시작된 방에는 참가하지 못하게 막습니다.
        if (room.Status == RoomStatus.Closed.ToString() ||
            room.Status == RoomStatus.Starting.ToString() ||
            room.Status == RoomStatus.InGame.ToString())
        {
            Debug.LogWarning($"[Room] Cannot join room in status: {room.Status}");
            return false;
        }

        // 현재 저장된 PlayerCount 기준으로 최대 인원을 검사합니다.
        // 동시에 여러 명이 들어오는 상황까지 완벽히 막으려면 나중에 Transaction으로 바꾸는 것이 좋습니다.
        if (room.PlayerCount >= room.MaxPlayers)
        {
            Debug.LogWarning($"[Room] Room is full: {roomId}");
            return false;
        }

        Timestamp now = Timestamp.GetCurrentTimestamp();

        // 참가자 플레이어 문서에 저장할 데이터입니다.
        // SlotIndex는 현재 PlayerCount를 사용해 방장 0, 첫 참가자 1처럼 배치합니다.
        RoomPlayerState player = new RoomPlayerState
        {
            UserId = userId,
            Nickname = nickname,
            ColorHex = colorHex,
            IsHost = false,
            IsReady = false,
            SlotIndex = room.PlayerCount,
            JoinedAt = now,
            LastSeenAt = now
        };

        // 참가자 문서 추가와 PlayerCount 증가를 한 번에 커밋합니다.
        WriteBatch batch = firestore.StartBatch();
        batch.Set(playerRef, player);
        batch.Update(roomRef, new Dictionary<string, object>
        {
            // FieldValue.Increment는 Firestore에서 숫자 필드를 원자적으로 증가시킵니다.
            { nameof(RoomState.PlayerCount), FieldValue.Increment(1) },
            { nameof(RoomState.UpdatedAt), now }
        });

        await batch.CommitAsync();
        return true;
    }

    public async Task<RoomState> GetRoomAsync(string roomId)
    {
        // 게임 씬은 GameSessionContext.RoomId를 통해 여기로 들어오므로 빈 값이면 바로 중단합니다.
        if (string.IsNullOrWhiteSpace(roomId))
        {
            Debug.LogWarning("[Room] RoomId is empty.");
            return null;
        }

        DocumentReference roomRef = firestore
            .Collection(RoomsCollection)
            .Document(roomId);

        DocumentSnapshot snapshot = await roomRef.GetSnapshotAsync();

        if (!snapshot.Exists)
        {
            // 방이 삭제되었거나 잘못된 roomId로 진입한 경우입니다.
            Debug.LogWarning($"[Room] Room not found: {roomId}");
            return null;
        }

        // Firestore의 rooms/{roomId} 문서를 RoomState로 변환해서 FinalMap까지 함께 돌려줍니다.
        return snapshot.ConvertTo<RoomState>();
    }

    public async Task<IReadOnlyList<RoomPlayerState>> GetPlayersAsync(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            Debug.LogWarning("[Room] RoomId is empty.");
            return new List<RoomPlayerState>();
        }

        CollectionReference playersRef = firestore
            .Collection(RoomsCollection)
            .Document(roomId)
            .Collection(PlayersCollection);

        QuerySnapshot snapshot = await playersRef.GetSnapshotAsync();
        List<RoomPlayerState> players = new List<RoomPlayerState>();

        foreach (DocumentSnapshot document in snapshot.Documents)
        {
            players.Add(document.ConvertTo<RoomPlayerState>());
        }

        // SlotIndex 0 -> Player1Spawn, SlotIndex 1 -> Player2Spawn 매핑이 안정적으로 되도록 정렬합니다.
        players.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
        return players;
    }

    public async Task LeaveRoomAsync(string roomId, string userId)
    {
        // 나가는 유저의 player 문서를 삭제하고, 방 상태도 함께 갱신합니다.
        DocumentReference roomRef = firestore.Collection(RoomsCollection).Document(roomId);
        DocumentReference playerRef = roomRef.Collection(PlayersCollection).Document(userId);

        DocumentSnapshot roomSnapshot = await roomRef.GetSnapshotAsync();
        if (!roomSnapshot.Exists)
        {
            return;
        }

        RoomState room = roomSnapshot.ConvertTo<RoomState>();
        Timestamp now = Timestamp.GetCurrentTimestamp();

        WriteBatch batch = firestore.StartBatch();

        // rooms/{roomId}/players/{userId} 문서를 삭제합니다.
        batch.Delete(playerRef);

        if (room.HostUserId == userId)
        {
            // 프로토타입에서는 방장이 나가면 방을 닫는 정책으로 둡니다.
            // 나중에 방장 위임을 만들고 싶으면 여기 로직을 바꾸면 됩니다.
            batch.Update(roomRef, new Dictionary<string, object>
            {
                { nameof(RoomState.Status), RoomStatus.Closed.ToString() },
                { nameof(RoomState.UpdatedAt), now }
            });
        }
        else
        {
            // 일반 참가자가 나가면 인원 수만 감소시킵니다.
            batch.Update(roomRef, new Dictionary<string, object>
            {
                { nameof(RoomState.PlayerCount), FieldValue.Increment(-1) },
                { nameof(RoomState.UpdatedAt), now }
            });
        }

        await batch.CommitAsync();
    }

    public Task UpdateThemeAsync(string roomId, AiMapTheme theme)
    {
        // 선택된 맵 테마를 room 문서에 저장합니다.
        // 방장 권한 체크는 RoomService와 Firestore Rules에서 처리하는 것을 추천합니다.
        DocumentReference roomRef = firestore.Collection(RoomsCollection).Document(roomId);

        return roomRef.UpdateAsync(new Dictionary<string, object>
        {
            { nameof(RoomState.SelectedTheme), theme.ToString() },
            { nameof(RoomState.UpdatedAt), Timestamp.GetCurrentTimestamp() }
        });
    }

    public Task UpdateReadyAsync(string roomId, string userId, bool isReady)
    {
        // Ready 상태는 room 문서가 아니라 각 플레이어 문서에 저장합니다.
        DocumentReference playerRef = firestore
            .Collection(RoomsCollection)
            .Document(roomId)
            .Collection(PlayersCollection)
            .Document(userId);

        return playerRef.UpdateAsync(new Dictionary<string, object>
        {
            { nameof(RoomPlayerState.IsReady), isReady },
            // Ready를 바꿀 때 LastSeenAt도 갱신해두면, 나중에 오래된 접속 정리에 사용할 수 있습니다.
            { nameof(RoomPlayerState.LastSeenAt), Timestamp.GetCurrentTimestamp() }
        });
    }

    public Task SetRoomStatusAsync(string roomId, RoomStatus status)
    {
        // Waiting, GeneratingMap, MapReady, Starting 같은 방 상태를 변경합니다.
        DocumentReference roomRef = firestore.Collection(RoomsCollection).Document(roomId);

        return roomRef.UpdateAsync(new Dictionary<string, object>
        {
            { nameof(RoomState.Status), status.ToString() },
            { nameof(RoomState.UpdatedAt), Timestamp.GetCurrentTimestamp() }
        });
    }

    public Task SaveFinalMapAsync(string roomId, AiMapLayoutDto finalMap)
    {
        // 방장이 생성하고 Validator를 통과한 최종 맵을 room 문서에 저장합니다.
        // 클라이언트들은 각자 맵을 다시 생성하지 않고 이 FinalMap을 그대로 사용해야 합니다.
        DocumentReference roomRef = firestore.Collection(RoomsCollection).Document(roomId);

        return roomRef.UpdateAsync(new Dictionary<string, object>
        {
            { nameof(RoomState.FinalMap), finalMap },
            // 맵이 재생성될 때마다 버전을 올려서 UI가 새 맵인지 판단하기 쉽게 합니다.
            { nameof(RoomState.MapVersion), FieldValue.Increment(1) },
            { nameof(RoomState.Status), RoomStatus.MapReady.ToString() },
            { nameof(RoomState.UpdatedAt), Timestamp.GetCurrentTimestamp() }
        });
    }

    public Task SetRelayJoinCodeAsync(string roomId, string relayJoinCode)
    {
        // 나중에 Relay Allocation을 만든 뒤, 참가자들이 사용할 Join Code를 room 문서에 저장합니다.
        DocumentReference roomRef = firestore.Collection(RoomsCollection).Document(roomId);

        return roomRef.UpdateAsync(new Dictionary<string, object>
        {
            { nameof(RoomState.RelayJoinCode), relayJoinCode ?? "" },
            { nameof(RoomState.UpdatedAt), Timestamp.GetCurrentTimestamp() }
        });
    }

    public ListenerRegistration ListenRoom(
        string roomId,
        Action<RoomState> onChanged,
        Action<Exception> onError)
    {
        // Listen은 문서가 바뀔 때마다 콜백을 호출하는 실시간 구독입니다.
        // 반환된 ListenerRegistration은 방을 나가거나 오브젝트가 파괴될 때 Stop() 해야 합니다.
        DocumentReference roomRef = firestore.Collection(RoomsCollection).Document(roomId);

        return roomRef.Listen(snapshot =>
        {
            try
            {
                if (!snapshot.Exists)
                {
                    // 방 문서가 삭제된 경우입니다.
                    onChanged?.Invoke(null);
                    return;
                }

                RoomState room = snapshot.ConvertTo<RoomState>();
                onChanged?.Invoke(room);
            }
            catch (Exception e)
            {
                onError?.Invoke(e);
            }
        });
    }

    public ListenerRegistration ListenPlayers(
        string roomId,
        Action<IReadOnlyList<RoomPlayerState>> onChanged,
        Action<Exception> onError)
    {
        // players 하위 컬렉션 전체를 실시간 구독합니다.
        // 누가 들어오거나 나가거나 ready를 바꾸면 snapshot이 다시 들어옵니다.
        CollectionReference playersRef = firestore
            .Collection(RoomsCollection)
            .Document(roomId)
            .Collection(PlayersCollection);

        return playersRef.Listen(snapshot =>
        {
            try
            {
                List<RoomPlayerState> players = new List<RoomPlayerState>();

                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    players.Add(document.ConvertTo<RoomPlayerState>());
                }

                // UI에서 방장, 1P, 2P 순서가 흔들리지 않도록 SlotIndex 기준으로 정렬합니다.
                players.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
                onChanged?.Invoke(players);
            }
            catch (Exception e)
            {
                onError?.Invoke(e);
            }
        });
    }

    private async Task<string> CreateUniqueRoomIdAsync()
    {
        const int maxAttempts = 10;

        for (int i = 0; i < maxAttempts; i++)
        {
            // 먼저 사람이 입력하기 쉬운 6자리 코드를 만듭니다.
            string roomId = CreateRoomId();
            DocumentReference roomRef = firestore.Collection(RoomsCollection).Document(roomId);

            // 같은 roomId 문서가 이미 있는지 Firestore에서 확인합니다.
            DocumentSnapshot snapshot = await roomRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                // 존재하지 않는 코드면 새 방 코드로 사용할 수 있습니다.
                return roomId;
            }
        }

        // 충돌이 계속되거나 읽기 요청이 계속 실패한 경우 상위 호출자가 처리하도록 예외를 던집니다.
        throw new Exception("Failed to create a unique room id.");
    }

    private string CreateRoomId()
    {
        // 헷갈리는 문자(0/O, 1/I)를 제외한 방 코드 문자셋입니다.
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] result = new char[6];

        for (int i = 0; i < result.Length; i++)
        {
            int index = UnityEngine.Random.Range(0, chars.Length);
            result[i] = chars[index];
        }

        return new string(result);
    }
}
