# Firestore 기반 로비/방 시스템 1차 구현

**날짜:** 2026-05-25  
**작업 기간:** 2026-05-25  
**관련 파일:**
- `Assets/Scripts/Lobby/Data/RoomStatus.cs`
- `Assets/Scripts/Lobby/Data/RoomState.cs`
- `Assets/Scripts/Lobby/Data/RoomPlayerState.cs`
- `Assets/Scripts/Lobby/Repository/IRoomRepository.cs`
- `Assets/Scripts/Lobby/Repository/FirestoreRoomRepository.cs`
- `Assets/Scripts/Lobby/Repository/RoomService.cs`
- `Assets/Scripts/Lobby/Repository/RoomMapService.cs`
- `Assets/Scripts/UI/Lobby/LobbyUIController.cs`
- `Assets/Scripts/UI/Lobby/LobbyUIView.cs`
- `Assets/Scripts/UI/Lobby/LobbyPresenter.cs`
- `Assets/Scripts/UI/Lobby/LobbyPlayerSlotView.cs`
- `Assets/Scripts/UI/Lobby/LobbyMapPreviewView.cs`
- `Assets/Scripts/Map/AI/AiMapLayoutConverter.cs`
- `Assets/Scripts/Map/AI/AiMapLayoutDto.cs`
- `Assets/Scripts/Map/AI/AiVector2IntDto.cs`
- `Assets/Scripts/UI/Login/LoginUIController.cs`
- `Assets/Scenes/LobbyScene.unity`
- `Assets/Scenes/LoginScene.unity`

---

## 작업 배경

이전 작업에서 AI 맵 생성은 1차 구조가 정리되었다.

현재 AI 맵 생성은 다음 원칙을 따른다.

```text
AI가 최종 타일맵을 직접 만들지 않는다.
AI는 선택된 테마 안에서 intent 옵션만 만든다.
실제 맵은 Unity의 테마별 Builder가 생성한다.
Validator와 fallback으로 최종 안전성을 확보한다.
```

이번 작업의 목표는 이 맵 생성 결과를 온라인/로컬 프로토타입의 방 시스템에 연결하기 위한 로비 기반을 만드는 것이었다.

특히 멀티플레이에서는 각 클라이언트가 따로 AI 맵을 생성하면 안 된다. 방장이 한 번 생성한 최종 맵을 모든 클라이언트가 같은 값으로 사용해야 한다.

따라서 이번 작업에서는 다음 방향으로 구조를 잡았다.

```text
Firestore
= 로비/방/플레이어/최종 맵 상태 공유

Relay + Netcode
= 이후 게임 시작 후 실제 플레이 동기화
```

이번 단계에서는 Relay/Netcode 연결 전, Firestore 기반 방 생성/참가/방장 권한/맵 생성 저장 흐름을 먼저 구현했다.

---

## Firestore를 선택한 이유

방 상태는 초당 계속 바뀌는 게임 플레이 상태가 아니라, 다음과 같은 문서형 상태에 가깝다.

```text
방 생성
방 참가
방장 uid
플레이어 목록
ready 상태
선택된 맵 테마
AI 맵 생성 상태
최종 맵 데이터
게임 시작 상태
```

Realtime Database도 사용할 수 있지만, 현재 프로젝트에서는 Firestore의 문서/컬렉션 구조가 더 잘 맞는다고 판단했다.

또한 이후 Firebase Function이나 OpenAI API로 확장할 경우에도 다음 구조로 이어가기 쉽다.

```text
rooms/{roomId}
-> 방 상태 저장

rooms/{roomId}/players/{userId}
-> 방 안의 플레이어 상태 저장

rooms/{roomId}.finalMap
-> 방장이 확정한 최종 맵 저장
```

게임 중 플레이어 위치, 총알, 충돌 같은 프레임 단위 상태는 Firestore에 넣지 않고 Netcode에서 처리할 예정이다.

---

## Room 데이터 모델

방 상태를 표현하기 위해 세 가지 데이터 클래스를 추가했다.

```text
RoomStatus
RoomState
RoomPlayerState
```

### RoomStatus

방의 현재 단계를 enum으로 표현한다.

현재 상태는 다음과 같다.

```text
Waiting
GeneratingMap
MapReady
Starting
InGame
Closed
```

Firestore에는 enum int가 아니라 문자열로 저장한다.

예를 들어 다음과 같이 저장된다.

```text
Status = "Waiting"
Status = "GeneratingMap"
Status = "MapReady"
```

Firestore 콘솔에서 확인하기 쉽고, 디버깅할 때 의미를 바로 읽을 수 있기 때문이다.

### RoomState

`rooms/{roomId}` 문서 하나를 표현한다.

주요 필드는 다음과 같다.

```text
RoomId
HostUserId
Status
MaxPlayers
PlayerCount
SelectedTheme
AiStyleHint
MapWidth
MapHeight
MapVersion
FinalMap
RelayJoinCode
CreatedAt
UpdatedAt
```

`FinalMap`은 방장이 생성하고 검증까지 통과한 최종 `AiMapLayoutDto`다.

각 클라이언트는 맵을 다시 생성하지 않고 이 값을 그대로 사용해야 한다.

### RoomPlayerState

`rooms/{roomId}/players/{userId}` 문서 하나를 표현한다.

주요 필드는 다음과 같다.

```text
UserId
Nickname
ColorHex
IsHost
IsReady
SlotIndex
JoinedAt
LastSeenAt
```

`SlotIndex`는 플레이어 슬롯 표시와 이후 스폰 위치 매칭에 사용한다.

현재는 다음 규칙을 사용한다.

```text
방장 = 0
첫 참가자 = 1
```

---

## Repository 계층

Firestore 접근을 UI나 Presenter가 직접 하지 않도록 `IRoomRepository`를 추가했다.

`IRoomRepository`는 방 저장소가 가져야 하는 기능 목록이다.

현재 주요 메서드는 다음과 같다.

```text
CreateRoomAsync
JoinRoomAsync
LeaveRoomAsync
UpdateThemeAsync
UpdateAiStyleHintAsync
UpdateReadyAsync
SetRoomStatusAsync
SaveFinalMapAsync
SetRelayJoinCodeAsync
ListenRoom
ListenPlayers
```

실제 Firestore 구현은 `FirestoreRoomRepository`가 담당한다.

구조는 다음과 같다.

```text
LobbyPresenter
-> RoomService
-> IRoomRepository
-> FirestoreRoomRepository
-> Firebase Firestore
```

이렇게 분리한 이유는 다음과 같다.

```text
UI가 Firestore API를 직접 알지 않아도 됨
테스트용 Fake Repository를 나중에 만들 수 있음
Firestore 저장 구조가 바뀌어도 상위 로직 수정이 줄어듦
```

### FirestoreRoomRepository

`FirestoreRoomRepository`는 실제 Firestore 문서 읽기/쓰기를 담당한다.

방 생성 시에는 다음 두 문서를 batch로 함께 만든다.

```text
rooms/{roomId}
rooms/{roomId}/players/{hostUserId}
```

batch를 사용한 이유는 방 문서와 방장 플레이어 문서가 같이 성공하거나 같이 실패하게 하기 위해서다.

방 참가 시에는 다음 흐름을 처리한다.

```text
room 문서 존재 확인
Closed / Starting / InGame 상태인지 확인
인원 초과 확인
players/{userId} 문서 추가
PlayerCount 증가
```

방 나가기 시에는 다음 정책을 적용했다.

```text
방장이 나가면 방을 Closed 처리
참가자가 나가면 PlayerCount 감소
```

프로토타입 단계에서는 방장 위임보다 방 폐쇄가 단순하고 명확하다고 판단했다.

### 방 코드 생성

처음에는 6자리 숫자 코드를 고려했지만, 충돌 가능성과 가독성을 위해 영문+숫자 6자리 코드로 만들었다.

사용 문자셋은 다음과 같다.

```text
ABCDEFGHJKLMNPQRSTUVWXYZ23456789
```

`0/O`, `1/I`처럼 헷갈리는 문자는 제외했다.

생성 흐름은 다음과 같다.

```text
6자리 roomId 생성
Firestore에서 rooms/{roomId} 존재 여부 확인
없으면 사용
있으면 최대 10번 재시도
실패 시 예외
```

---

## RoomService

`RoomService`는 UI와 Repository 사이에서 현재 유저 기준의 방 규칙을 처리한다.

Repository가 Firestore 읽기/쓰기만 담당한다면, RoomService는 다음을 담당한다.

```text
현재 유저가 방장인지 판단
방 생성/참가/나가기 흐름 관리
방장 전용 명령 차단
ready 상태 변경
게임 시작 가능 조건 계산
Firestore listener 등록/해제
```

현재 주요 상태는 다음과 같다.

```text
CurrentRoom
CurrentPlayers
```

UI 갱신을 위해 다음 이벤트를 제공한다.

```text
RoomChanged
PlayersChanged
ErrorOccurred
```

`RoomService`는 Firestore SDK의 `Task` 기반 Repository를 호출하지만, UI/Presenter 흐름에서는 UniTask를 사용한다.

현재 비동기 계층은 다음처럼 나뉘었다.

```text
FirestoreRoomRepository = Task
RoomService = UniTask
LobbyPresenter = UniTask
LobbyUIController = UniTask Forget
```

Firebase SDK는 기본적으로 `Task`를 반환하므로 Repository는 `Task` 유지가 자연스럽고, Unity UI 쪽은 UniTask가 더 편하다.

---

## RoomMapService

`RoomMapService`는 로비 방 상태와 기존 AI 맵 생성 파이프라인을 연결하는 서비스다.

역할은 다음과 같다.

```text
현재 room 상태 확인
방장 권한 확인
SelectedTheme 문자열을 AiMapTheme으로 변환
GeneratingMap 상태 저장
AiMapGenerateRequest 생성
IAiMapClient 생성
AiMapGenerator 실행
최종 MapLayoutData 검증
AiMapLayoutDto로 변환
RoomService.SaveFinalMapAsync 호출
```

구조는 다음과 같다.

```text
LobbyPresenter
-> RoomMapService
-> AiMapGenerator
-> OllamaAiMapClient
-> MapLayoutValidator
-> AiMapLayoutConverter
-> RoomService.SaveFinalMapAsync
-> Firestore finalMap 저장
```

`RoomMapService`는 `Func<AiMapTheme, IAiMapClient>`를 받도록 만들었다.

현재는 다음처럼 Ollama 클라이언트를 생성한다.

```csharp
theme => new OllamaAiMapClient(theme)
```

나중에는 같은 구조에서 다음처럼 바꿀 수 있다.

```csharp
theme => new FakeAiMapClient()
theme => new FirebaseAiMapClient()
```

---

## AiMapLayoutConverter

기존에는 `AiMapLayoutParser`가 다음 방향만 담당했다.

```text
AiMapLayoutDto
-> MapLayoutData
```

이번에는 방장이 생성한 최종 `MapLayoutData`를 Firestore에 저장해야 하므로 반대 방향 변환이 필요했다.

따라서 `AiMapLayoutConverter`를 추가했다.

```text
MapLayoutData
-> AiMapLayoutDto
```

변환 내용은 다음과 같다.

```text
MapTileType[] -> int[]
Vector2Int Player1Spawn -> AiVector2IntDto
Vector2Int Player2Spawn -> AiVector2IntDto
```

`AiMapLayoutDto`와 `AiVector2IntDto`에는 Firestore 저장/복원을 위해 `[FirestoreData]`, `[FirestoreProperty]`를 추가했다.

---

## Lobby MVP 구조

기존 로그인 UI가 MVP 패턴으로 구성되어 있었기 때문에, 로비 UI도 같은 구조로 통일했다.

현재 로비 UI 구조는 다음과 같다.

```text
LobbyUIController
LobbyUIView
LobbyPresenter
```

### LobbyUIController

Unity 이벤트 어댑터 역할만 담당한다.

주요 역할은 다음과 같다.

```text
LobbyUIView 참조
RoomService 생성
RoomMapService 생성
LobbyPresenter 생성
버튼 OnClick을 Presenter로 전달
씬 종료 시 Presenter Dispose
```

### LobbyPresenter

로비 화면의 흐름을 제어한다.

주요 역할은 다음과 같다.

```text
유저 정보 표시
방 생성 버튼 처리
방 참가 버튼 처리
방 나가기 처리
ready 토글 처리
theme 변경 처리
AI style hint 변경 처리
맵 생성 처리
게임 시작 가능 조건에 따른 버튼 상태 갱신
RoomService 이벤트를 받아 View 갱신
```

중복 클릭을 막기 위해 `isBusy` 플래그를 사용한다.

### LobbyUIView

실제 UI 표시와 입력값 제공만 담당한다.

현재 View는 다음 패널 구조를 기준으로 한다.

```text
LobbyRoot
├─ UserInfoPanel
├─ CreateJoinPanel
└─ RoomPanel
   ├─ RoomHeaderPanel
   ├─ PlayerSlotsPanel
   ├─ MapSetupPanel
   └─ RoomActionPanel
```

기존에는 별도 `StatusPanel`, `LoadingIndicator`를 고려했지만, `RoomPanel`이 화면을 크게 차지하는 구조에서는 별도 전역 상태 패널이 불필요하다고 판단했다.

현재 `SetStatus()`는 UI 텍스트를 갱신하지 않고 로그만 남긴다. 방 내부 상태는 다음 텍스트들이 담당한다.

```text
RoomStatusText
MapStatusText
PlayerSlot ready text
```

---

## RoomPanel UI 구조

이번 작업에서 RoomPanel을 다음 구조로 정리했다.

```text
RoomPanel
├─ RoomHeaderPanel
│  ├─ RoomCodeText
│  ├─ CopyRoomCodeButton
│  ├─ RoomStatusText
│  └─ LeaveRoomButton
│
├─ PlayerSlotsPanel
│  ├─ PlayerSlot1
│  │  ├─ ColorImage
│  │  ├─ NicknameText
│  │  ├─ RoleText
│  │  └─ ReadyText
│  └─ PlayerSlot2
│     ├─ ColorImage
│     ├─ NicknameText
│     ├─ RoleText
│     └─ ReadyText
│
├─ MapSetupPanel
│  ├─ ThemeDropdown
│  ├─ AiStyleHintInput
│  ├─ GenerateMapButton
│  ├─ MapStatusText
│  └─ MapPreviewArea
│
└─ RoomActionPanel
   ├─ ReadyButton
   └─ StartGameButton
```

방 생성 전에는 `CreateJoinPanel`을 보여주고, 방 생성/참가 후에는 `RoomPanel`을 보여준다.

```text
방 밖
UserInfoPanel ON
CreateJoinPanel ON
RoomPanel OFF

방 안
UserInfoPanel ON
CreateJoinPanel OFF
RoomPanel ON
```

MapSetupPanel은 참가자도 볼 수 있다.

다만 조작은 방장만 가능하다.

```text
방장
ThemeDropdown 조작 가능
AiStyleHintInput 조작 가능
GenerateMapButton 표시/활성
StartGameButton 표시

참가자
ThemeDropdown 읽기 전용
AiStyleHintInput 읽기 전용
GenerateMapButton 숨김
ReadyButton 표시
```

---

## Player Slot 표시

기존에는 플레이어 목록을 `playersText` 하나에 여러 줄 문자열로 표시하려고 했다.

하지만 매칭 상태를 명확하게 보여주기 위해 `LobbyPlayerSlotView`를 추가했다.

각 슬롯은 다음을 표시한다.

```text
ColorImage
NicknameText
RoleText
ReadyText
```

방 생성 직후에는 다음처럼 보인다.

```text
PlayerSlot1 = 방장 정보
PlayerSlot2 = Waiting...
```

상대방이 참가하면 Firestore `players` listener를 통해 `PlayerSlot2`가 갱신된다.

```text
PlayerSlot2 = 상대 닉네임 / Guest / Not Ready
```

상대방이 ready를 누르면 다음처럼 갱신된다.

```text
PlayerSlot2 = 상대 닉네임 / Guest / Ready
```

---

## 로비 맵 미리보기

방장이 맵을 생성한 뒤 `FinalMap`이 Firestore에 저장되어도, 처음에는 `MapPreviewArea`에 실제 타일을 그리는 코드가 없었다.

이 때문에 UI에서는 맵이 생성되었는지 확인하기 어려웠다.

이를 해결하기 위해 `LobbyMapPreviewView`를 추가했다.

역할은 다음과 같다.

```text
AiMapLayoutDto를 입력받음
UI Image 사각형으로 Floor / Wall 표시
Player1Spawn / Player2Spawn 표시
맵이 없으면 Clear
```

이 미리보기는 실제 게임 맵 생성기가 아니다.

목적은 로비에서 다음을 확인하는 것이다.

```text
방장이 finalMap을 생성했는지
Firestore listener로 모든 클라이언트가 같은 finalMap을 받는지
현재 선택 테마가 맵 생성에 반영되는지
```

---

## LoginUIController 보완

테스트 중 LoginScene에서 다음 에러가 발생했다.

```text
[LoginUIController] SceneLoader is missing.
```

`LoginUIController`는 `SceneLoader`를 Inspector 참조 또는 같은 GameObject의 컴포넌트로 찾는 구조다.

LoginScene의 UI 오브젝트에 `SceneLoader`를 추가하고, `LoginUIController`에 연결해야 한다.

또한 `LoginUIController.Construct(AuthManager.Instance)`를 호출하는 코드가 없다는 점을 확인했다.

따라서 `authService`가 주입되지 않은 경우 `AuthManager.Instance`를 fallback으로 사용하도록 보완했다.

```text
Construct 호출이 있으면 주입된 IAuthService 사용
없으면 AuthManager.Instance 사용
```

이를 통해 LoginScene에서 Guest Login 후 LobbyScene으로 이동하는 기존 흐름을 유지할 수 있게 했다.

---

## 현재 확인한 내용

이번 작업에서 확인한 내용은 다음과 같다.

```text
LoginScene에서 익명 로그인 후 LobbyScene 진입 가능
LobbyScene에서 방 생성 UI 표시
방 생성 후 RoomPanel 표시
방장 정보가 PlayerSlot1에 표시
상대 슬롯은 Waiting 상태로 표시
방장이 ThemeDropdown으로 맵 테마 선택 가능
방장이 GenerateMapButton으로 맵 생성 가능
맵 생성 후 FinalMap이 저장되고 MapPreviewArea에 미리보기 표시
```

Dropdown 테스트 중 `OnValueChanged(Int32)`가 Static int 값으로 연결되면 항상 0만 전달된다는 점도 확인했다.

`ThemeDropdown` 이벤트는 반드시 Dynamic int 방식으로 연결해야 한다.

```text
정상
ThemeDropdown OnValueChanged(int)
-> LobbyUIController.OnThemeChanged
-> Dynamic int

오류
Static int 0 입력
-> 어떤 옵션을 골라도 Bridge로 처리됨
```

---

## 현재 한계

이번 작업은 로비/방 시스템의 1차 구현이다.

아직 남은 부분은 다음과 같다.

```text
Firestore Rules 정리 필요
JoinRoomAsync 동시 입장 처리 Transaction 보완 필요
방장 나가기 시 방장 위임 없음
오래된 room 정리 없음
앱 종료 시 player 문서 정리 없음
Relay/Netcode 연결 전
게임 씬 이동 전
게임 씬에서 finalMap 로드 전
RoomMapPreview는 로비 확인용 UI 미리보기일 뿐 실제 게임 맵 생성은 아님
```

또한 현재 `RoomService`와 `RoomMapService`가 `Lobby/Repository` 폴더 아래에 있다.

역할상 Repository보다는 Service에 가까우므로 이후 정리 시 다음 구조로 옮기는 것이 좋다.

```text
Assets/Scripts/Lobby/Service/RoomService.cs
Assets/Scripts/Lobby/Service/RoomMapService.cs
```

---

## 다음 작업 계획

다음 작업은 크게 세 단계로 나눌 수 있다.

```text
1. 방 참가/ready를 2클라이언트 환경에서 테스트
2. Firestore finalMap을 게임 씬에서 로드해 실제 맵 생성에 사용
3. Relay/Netcode 시작 흐름 연결
```

우선순위는 2클라이언트 테스트다.

확인해야 할 흐름은 다음과 같다.

```text
Host
-> 방 생성
-> Theme 선택
-> AI 맵 생성
-> finalMap 저장

Client
-> 방 코드로 참가
-> PlayerSlot2 표시
-> Ready
-> Host 화면의 Ready 상태 갱신
-> finalMap 미리보기 동기화
```

이 흐름이 안정화되면 다음 단계에서 `StartGame`을 Relay/Netcode와 연결한다.

예상 흐름은 다음과 같다.

```text
방장 StartGame
-> Relay Allocation 생성
-> relayJoinCode를 room 문서에 저장
-> 방장 StartHost
-> 참가자 relayJoinCode 수신
-> 참가자 StartClient
-> 게임 씬 이동
-> roomId로 finalMap 로드
-> 모든 클라이언트가 같은 맵 생성
```

---

## 정리

이번 작업으로 AI 맵 생성 파이프라인은 로비/방 시스템과 연결되기 시작했다.

핵심 구조는 다음과 같다.

```text
Firestore room
-> 방 상태와 플레이어 상태 공유

RoomService
-> 현재 유저 기준 방 규칙 처리

RoomMapService
-> 방장이 선택한 테마로 AI 맵 생성
-> 검증된 finalMap 저장

Lobby MVP
-> 방 생성/참가/매칭 상태/맵 설정 UI 표시
```

포트폴리오 관점에서 이번 작업의 설명 포인트는 다음과 같다.

```text
Firestore 기반 로비 상태 동기화
Repository / Service / MVP 계층 분리
방장 권한 기반 맵 설정
방장이 생성한 finalMap을 모든 클라이언트가 공유
AI 맵 생성 파이프라인을 로비 방 시스템에 연결
로비에서 finalMap 미리보기 제공
```

이제 로비는 단순 버튼 화면이 아니라, 실제 온라인 프로토타입으로 이어질 수 있는 대기방 구조를 갖추기 시작했다.
