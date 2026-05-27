# Relay/Netcode 게임 씬 진입 및 플레이어 스폰

**날짜:** 2026-05-27  
**작업 기간:** 2026-05-27  
**관련 파일:**
- `Assets/Scripts/Core/GameSession/GameSessionContext.cs`
- `Assets/Scripts/Core/SceneManager/GameSceneBootstrap.cs`
- `Assets/Scripts/Core/Data/Network/NetworkConnectionPayload.cs`
- `Assets/Scripts/Core/Data/Network/NetworkSessionRegistry.cs`
- `Assets/Scripts/Core/Data/Network/NetworkConnectionApprovalHandler.cs`
- `Assets/Scripts/Relay/UnityGameServicesInitializer.cs`
- `Assets/Scripts/Relay/RelayGameStartService.cs`
- `Assets/Scripts/Map/GameMapSpawner.cs`
- `Assets/Scripts/Player/GamePlayerPreviewSpawner.cs`
- `Assets/Scripts/Player/NetworkPlayerSpawner.cs`
- `Assets/Scripts/Lobby/Repository/IRoomRepository.cs`
- `Assets/Scripts/Lobby/Repository/FirestoreRoomRepository.cs`
- `Assets/Scripts/UI/Lobby/LobbyPresenter.cs`
- `Assets/Scripts/UI/Lobby/LobbyUIController.cs`
- `Assets/Scenes/Map1.unity`
- `Assets/Scenes/LobbyScene.unity`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `ProjectSettings/EditorBuildSettings.asset`

---

## 작업 배경

이전 단계까지는 로비에서 방을 만들고, 방장이 AI 맵을 생성한 뒤, 최종 `AiMapLayoutDto`를 Firestore `rooms/{roomId}.FinalMap`에 저장하는 흐름까지 구현되어 있었다.

이번 작업의 목표는 Firestore에 저장된 `FinalMap`을 실제 게임 씬에서 모든 클라이언트가 동일하게 로드하고, Relay/Netcode 연결 위에서 각 플레이어를 `SlotIndex`에 맞게 스폰하는 것이었다.

현재 역할 경계는 다음처럼 정리했다.

```text
Firestore
-> 로비/방 상태, players 목록, finalMap, relayJoinCode 공유

Relay + Netcode
-> 게임 시작 후 실제 클라이언트 연결, PlayerObject 스폰, 이후 게임 플레이 동기화
```

게임 플레이 중 위치, 총알, 충돌 같은 프레임 단위 상태는 Firestore에 쓰지 않고 Netcode로 처리한다.

---

## GameSessionContext 추가

`LobbyScene`에서 `Map1`으로 넘어갈 때 필요한 최소 런타임 정보를 보관하기 위해 `GameSessionContext`를 추가했다.

저장하는 값은 다음과 같다.

```text
RoomId
LocalUserId
HostUserId
```

`RoomId`는 게임 씬에서 Firestore `rooms/{roomId}` 문서를 다시 읽는 데 사용한다.  
`LocalUserId`는 현재 클라이언트의 Firebase UID이며, Netcode connection payload와 player 문서 조회 기준으로 사용한다.  
`HostUserId`는 현재 클라이언트가 Host인지 판단하는 데 사용한다.

`RoomState` 전체를 들고 씬을 넘기지 않은 이유는 게임 씬에서 Firestore를 단건 조회하면 모든 클라이언트가 같은 저장 상태를 기준으로 초기화할 수 있기 때문이다.

---

## RoomRepository 단건 조회 추가

게임 씬에서 로비의 listener 상태에 의존하지 않고 필요한 데이터를 직접 읽기 위해 `IRoomRepository`에 단건 조회 메서드를 추가했다.

```text
GetRoomAsync(roomId)
GetPlayersAsync(roomId)
```

`GetRoomAsync`는 `rooms/{roomId}` 문서를 읽어 `RoomState`로 변환한다.  
`GetPlayersAsync`는 `rooms/{roomId}/players` 하위 컬렉션을 읽고 `SlotIndex` 기준으로 정렬한다.

이 조회는 게임 씬 초기화 전용이며, 게임 플레이 상태 동기화 용도는 아니다.

---

## GameSceneBootstrap

`Map1`을 게임 씬으로 사용하기로 결정하고, 씬 진입점으로 `GameSceneBootstrap`을 추가했다.

주요 흐름은 다음과 같다.

```text
Map1 로드
-> GameSessionContext.RoomId 확인
-> Firestore rooms/{roomId} 단건 조회
-> room.FinalMap 확인
-> AiMapLayoutParser로 MapLayoutData 변환
-> GameMapSpawner로 실제 맵 생성
-> players 목록 조회
-> Netcode Host면 NetworkPlayerSpawner로 PlayerObject 스폰
-> Netcode 없이 단독 테스트 중이면 GamePlayerPreviewSpawner로 임시 마커 표시
```

`GameSceneBootstrap`은 `RuntimeInitializeOnLoadMethod`와 `SceneManager.sceneLoaded`를 사용해 `Map1` 로드 시 자동 생성되도록 했다. 그래서 씬에 직접 배치하지 않아도 기본 초기화가 동작한다.

또한 기존 테스트용 `MapPreviewSpawner`가 게임 씬에서 같은 맵을 중복 생성하지 않도록 비활성화한다.

---

## GameMapSpawner

로비 미리보기용 `MapPreviewSpawner`와 별개로, 실제 게임 씬에서 사용할 `GameMapSpawner`를 추가했다.

역할은 다음과 같다.

```text
MapLayoutData 기반 Floor / Wall 타일 생성
Wall 타일에 BoxCollider2D 추가
Player1Spawn / Player2Spawn 월드 좌표 계산
SlotIndex에 맞는 스폰 위치 반환
```

현재는 프로토타입 단계라 Unity 기본 흰 텍스처를 스프라이트로 변환해서 사각 타일을 그리고 있다. 나중에 실제 타일 아트가 들어오면 이 스포너의 시각화 부분만 교체하면 된다.

---

## 임시 플레이어 프리뷰

Relay/Netcode 스폰을 붙이기 전에 `rooms/{roomId}/players`의 `SlotIndex`가 맵 스폰 위치와 올바르게 매핑되는지 확인하기 위해 `GamePlayerPreviewSpawner`를 추가했다.

처음에는 같은 GameObject에 `SpriteRenderer`와 `TextMesh`를 붙여 충돌이 발생했다.

```text
Can't add component 'MeshRenderer' because it conflicts with SpriteRenderer
```

이를 해결하기 위해 다음 구조로 분리했다.

```text
PlayerPreview
-> Body  : SpriteRenderer
-> Label : TextMesh / MeshRenderer
```

현재 실제 Netcode 실행 중에는 사용하지 않고, Netcode 없이 Map1만 단독 확인할 때만 임시 마커를 표시한다.

---

## Netcode Connection Payload

Netcode 접속 승인 단계에서 클라이언트가 어떤 Firebase 유저인지 알기 위해 `NetworkConnectionPayload`를 추가했다.

전달하는 값은 다음과 같다.

```text
RoomId
UserId
```

클라이언트는 `NetworkManager.NetworkConfig.ConnectionData`에 이 payload를 UTF8 JSON byte 배열로 넣고 접속한다. Host는 `ConnectionApprovalCallback`에서 이를 읽어 접속을 승인하거나 거절한다.

---

## NetworkSessionRegistry

Host 런타임에서 Netcode `clientId`와 Firebase `UserId`를 연결하기 위해 `NetworkSessionRegistry`를 추가했다.

필요한 이유는 Netcode의 연결 ID와 Firestore player 문서의 ID가 서로 다르기 때문이다.

```text
Netcode clientId
-> Firebase UserId
-> rooms/{roomId}/players/{userId}
-> SlotIndex
-> spawn position
```

이 매핑 덕분에 Host는 어떤 클라이언트가 몇 번 슬롯인지 판단하고, 맞는 위치에 `PlayerObject`를 스폰할 수 있다.

---

## NetworkConnectionApprovalHandler

`NetworkConnectionApprovalHandler`는 `NetworkManager.ConnectionApprovalCallback`을 등록하고, 클라이언트의 접속 payload를 검사한다.

승인 조건은 현재 다음과 같다.

```text
payload가 비어 있지 않아야 함
RoomId와 UserId가 있어야 함
Host의 현재 RoomId와 payload.RoomId가 같아야 함
NetworkSessionRegistry 등록에 성공해야 함
```

승인 시 `response.CreatePlayerObject = false`로 설정했다.

이유는 Netcode의 자동 PlayerPrefab 생성을 사용하지 않고, Host가 Firestore `SlotIndex`를 확인한 뒤 `SpawnAsPlayerObject(clientId)`로 직접 스폰해야 하기 때문이다.

---

## UnityGameServicesInitializer

Relay API 호출 전에 Unity Gaming Services 초기화와 Unity Services 익명 로그인을 보장하기 위해 `UnityGameServicesInitializer`를 추가했다.

Firebase Auth와 Unity Services Authentication은 별개다.

```text
Firebase Auth
-> 로비/Firestore userId 기준

Unity Services Authentication
-> Relay/UGS API 호출 권한 기준
```

`EnsureInitializedAsync`는 이미 초기화되어 있으면 바로 반환하고, 동시에 여러 번 호출될 경우 기존 초기화가 끝날 때까지 기다린다.

---

## RelayGameStartService

Relay 연결과 Netcode 시작을 담당하는 `RelayGameStartService`를 추가했다.

Host 흐름은 다음과 같다.

```text
UGS 초기화
-> Relay Allocation 생성
-> JoinCode 발급
-> UnityTransport에 RelayServerData 설정
-> ConnectionApprovalCallback 등록
-> Host payload 설정
-> NetworkManager.StartHost()
-> LastRelayJoinCode 저장
```

Client 흐름은 다음과 같다.

```text
UGS 초기화
-> Firestore에서 받은 relayJoinCode로 JoinAllocation 참가
-> UnityTransport에 RelayServerData 설정
-> ConnectionApprovalCallback 등록
-> Client payload 설정
-> NetworkManager.StartClient()
```

`RelayServerData` 생성자는 패키지 버전에 따라 인자가 달라질 수 있어, 현재 프로젝트에서는 `allocation.ToRelayServerData("dtls")`와 `joinAllocation.ToRelayServerData("dtls")`를 사용했다.

---

## LobbyPresenter 게임 시작 흐름 연결

기존 `StartGame` 흐름에 Relay 시작 흐름을 연결했다.

방장 흐름은 다음과 같다.

```text
StartGame 버튼
-> GameSessionContext.SetSession
-> RelayGameStartService.StartHostWithRelayAsync
-> relayJoinCode를 Firestore room에 저장
-> room status를 Starting으로 변경
-> Map1 로드
```

참가자 흐름은 다음과 같다.

```text
room status Starting 수신
-> relayJoinCode 확인
-> GameSessionContext.SetSession
-> RelayGameStartService.StartClientWithRelayAsync
-> Map1 로드
```

Firestore는 여기서 `relayJoinCode` 전달과 `Starting` 상태 공유까지만 담당한다. 실제 접속은 Relay/Netcode가 담당한다.

---

## NetworkPlayerSpawner

`NetworkPlayerSpawner`는 Host 전용 실제 플레이어 스포너다.

`NetworkManager.NetworkConfig.PlayerPrefab`에 등록된 `TestPlayer`를 사용하고, 각 Firestore player 문서의 `SlotIndex`를 기준으로 스폰 위치를 결정한다.

흐름은 다음과 같다.

```text
Host가 Map1 로드
-> Firestore players 조회
-> NetworkSessionRegistry에서 userId에 해당하는 clientId 조회
-> ConnectedClients에 실제 연결이 있는지 확인
-> mapSpawner.GetSpawnWorldPosition(slotIndex)
-> Instantiate(PlayerPrefab)
-> NetworkObject.SpawnAsPlayerObject(clientId)
```

Host가 Map1을 먼저 로드하고 Client approval이 조금 늦게 들어오는 상황이 있어, 최대 10초 동안 모든 player 연결 등록이 끝날 때까지 기다리도록 했다.

테스트 결과 `TestPlayer`가 각 클라이언트에 맞게 정상 스폰되는 것을 확인했다.

---

## 현재 확인한 내용

이번 작업에서 확인한 흐름은 다음과 같다.

```text
Host
-> 방 생성
-> AI 맵 생성
-> StartGame
-> UGS 초기화
-> Relay Allocation 생성
-> JoinCode 발급
-> StartHost
-> Map1 이동
-> FinalMap 로드
-> TestPlayer 스폰

Client
-> 방 참가
-> room status Starting 수신
-> relayJoinCode 수신
-> Relay JoinAllocation 참가
-> StartClient
-> Map1 이동
-> FinalMap 로드
-> Host가 스폰한 TestPlayer 수신
```

로그 기준으로 `NetworkConnectionApprovalHandler` 승인, `NetworkSessionRegistry` 등록, `NetworkPlayerSpawner` 스폰이 정상 동작했다.

Unity Services Wire 쪽에서 WebSocket 관련 fatal 로그가 한 번 출력되었지만, Relay/Netcode 접속과 PlayerObject 스폰 흐름은 정상적으로 진행되었다.

---

## 현재 한계

아직 구현하지 않은 부분은 다음과 같다.

```text
PlayerController가 아직 3D XZ 이동 기준이라 2D XY 이동으로 수정 필요
TestPlayer에 Rigidbody2D / Collider2D 기반 충돌 처리 정리 필요
카메라 추적 로직 미구현
총 발사 / 넉백 / 탄환 NetworkObject 미구현
게임 종료 조건 미구현
disconnect / 재접속 / 방 정리 흐름 미구현
```

현재 단계는 “같은 맵 로드 + Relay/Netcode 연결 + SlotIndex 기반 TestPlayer 스폰”까지의 연결 검증 단계다.

---

## 다음 작업 계획

다음 우선순위는 실제 플레이 조작을 붙이는 것이다.

```text
1. PlayerController를 2D 이동 기준으로 수정
2. Rigidbody2D / Collider2D 충돌 적용
3. Owner 입력만 처리하고 Host가 위치를 확정하는 구조 유지
4. NetworkTransform으로 위치 동기화 확인
5. 카메라 추적 추가
6. 총알 / 넉백 / 충돌 판정 추가
```

가장 먼저 할 일은 `PlayerController`의 이동축을 기존 `x/z`에서 2D 게임에 맞는 `x/y`로 바꾸고, 벽 `BoxCollider2D`와 충돌하도록 물리 구성을 맞추는 것이다.

---

## 정리

이번 작업으로 로비에서 생성한 최종 맵이 실제 게임 씬으로 이어지고, Relay/Netcode 위에서 각 플레이어가 Firestore `SlotIndex`에 맞게 스폰되는 첫 번째 온라인 플레이 뼈대가 완성되었다.

핵심 구조는 다음과 같다.

```text
Lobby
-> Firestore room/finalMap/relayJoinCode 공유

GameSessionContext
-> 씬 이동 사이의 최소 세션 정보 유지

GameSceneBootstrap
-> Firestore finalMap 로드 후 게임 맵 생성

RelayGameStartService
-> Relay Allocation/JoinAllocation 및 StartHost/StartClient 처리

NetworkConnectionApprovalHandler
-> ConnectionData 검증 및 clientId/userId 매핑

NetworkPlayerSpawner
-> SlotIndex 기반 TestPlayer NetworkObject 스폰
```

이제 프로젝트는 단순 로비 프로토타입에서 실제 네트워크 게임 플레이를 붙일 수 있는 단계로 넘어왔다.
