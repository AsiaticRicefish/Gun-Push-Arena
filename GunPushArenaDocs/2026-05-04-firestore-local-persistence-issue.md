# Firestore 로컬 Persistence 충돌 이슈

**날짜:** 2026-05-04  
**관련 파일:**
- `Assets/Scripts/Firebase/FirebaseManager.cs`
- `Assets/Scripts/Core/AuthManager.cs`
- `Assets/Scripts/Core/Data/User/UserDataService.cs`
- `Assets/Scripts/Core/Data/User/UserData.cs`

---

## 문제 증상

멀티플레이 테스트를 위해 같은 PC에서 두 개의 클라이언트를 동시에 실행했을 때, 첫 번째 클라이언트는 정상 접속하지만 두 번째 클라이언트가 Firestore 접근 시점에서 튕기는 현상이 발생했다.

Firestore 관련 코드를 주석 처리하면 두 클라이언트 모두 정상 실행되었다.

---

## 재현 조건

- 동일한 PC에서 같은 빌드(또는 에디터 + 빌드)를 동시에 두 개 이상 실행
- Firestore 로컬 persistence가 활성화된 상태(`PersistenceEnabled = true`, 기본값)

---

## 로그 기반 분석

두 번째 클라이언트의 로그는 아래 시점 이후로 이어지지 않았다.

```
[Auth] Final UID: {UserId}
[Firestore] LoadOrCreate Start         ← 이 로그 이후 중단
```

`[UserDataService] 기존 유저 데이터 로드` 또는 `[UserDataService] 신규 유저 데이터 생성` 로그에 도달하지 못했다.

로그 흐름 기준으로 `AuthManager.SignInAnonymously()` 내에서 `userDataService.GetUserDataAsync(UserId)`를 호출한 직후, 즉 Firestore SDK가 내부적으로 로컬 캐시에 접근하는 시점에서 크래시가 발생했다.

---

## 원인 분석

### 왜 서버 데이터 충돌이 아닌가

Firestore 문서 경로는 `users/{uid}` 구조이며, 각 클라이언트는 익명 로그인으로 서로 다른 UID를 부여받는다. 따라서 두 클라이언트가 서버의 동일한 문서를 동시에 쓰는 상황은 아니다.

### 실제 원인: 로컬 persistence 캐시 충돌

Firestore SDK는 기본적으로 로컬 persistence(오프라인 캐시)를 활성화한다. 이 캐시는 Unity의 `Application.persistentDataPath` 아래 SQLite 파일로 저장된다.

같은 PC에서 실행하는 여러 클라이언트 인스턴스는 **동일한 `persistentDataPath`를 공유**한다. 두 프로세스가 동시에 같은 SQLite 파일에 접근하려 하면 파일 락(lock) 충돌이 발생하고, 나중에 접근을 시도한 프로세스(두 번째 클라이언트)가 크래시된다.

---

## Firestore 로컬 Persistence란

Firestore SDK의 로컬 persistence는 네트워크가 끊겼을 때도 캐시된 데이터를 반환하거나, 쓰기 작업을 큐에 보관했다가 연결 복구 시 서버에 전송하는 기능이다.

이 캐시는 단일 프로세스를 전제로 설계되어 있어, 같은 경로를 두 프로세스가 동시에 잠그면 SDK 수준에서 예외 처리 없이 크래시로 이어진다.

---

## 수정 방식

`FirebaseManager.cs`에 `ConfigureFirestore()` 메서드를 추가하고, Firebase dependency 확인 직후 `InReady = true` 이전에 호출한다.

```csharp
// FirebaseManager.cs
private void InitFirebase()
{
    FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
    {
        var dependencyStatus = task.Result;
        if (dependencyStatus == DependencyStatus.Available)
        {
            firebaseApp = FirebaseApp.DefaultInstance;
            ConfigureFirestore();   // InReady 이전에 설정 적용
            InReady = true;

            Debug.Log("[Firebase] Initialized successfully.");
        }
        else
        {
            InReady = false;
            Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
        }
    });
}

private void ConfigureFirestore()
{
    FirebaseFirestore firestore = FirebaseFirestore.DefaultInstance;
    firestore.Settings.PersistenceEnabled = false;

    Debug.Log("[Firestore] Local persistence disabled.");
}
```

`InReady = true` 전에 설정을 적용하는 이유는, `AuthManager`가 `InReady`를 폴링(`WaitForFirebase`)하다가 `true`가 되는 순간 Firestore에 접근하기 때문이다. 설정이 먼저 완료되어 있어야 한다.

---

## 이 수정의 장점

- 같은 PC에서 여러 클라이언트를 동시에 실행해도 로컬 캐시 충돌이 발생하지 않는다.
- 코드 변경이 최소화되며, Firestore 사용 방식 자체는 바뀌지 않는다.
- 항상 서버 데이터를 기준으로 읽고 쓰기 때문에 데이터 일관성이 높아진다.

---

## 트레이드오프

로컬 persistence를 끄면 **오프라인 캐시 기능이 비활성화**된다.

- 네트워크가 없는 상태에서 Firestore 읽기/쓰기를 시도하면 요청이 실패한다.
- 오프라인 중 발생한 쓰기 작업은 큐에 보관되지 않아 유실된다.

현재 Gun Push Arena는 실시간 멀티플레이 게임으로 네트워크 연결이 전제되어 있어, 오프라인 캐시의 필요성은 낮다.

---

## 배포 환경에서의 고려 사항

| 환경 | 로컬 persistence 영향 |
|---|---|
| 같은 PC 다중 실행 (개발/테스트) | **off 권장** — 파일 락 충돌 방지 |
| 실제 유저, 각자 다른 기기 | 충돌 없음 — 기기마다 별도 `persistentDataPath` |
| 오프라인 플레이 지원이 필요한 경우 | persistence on + 단일 프로세스 보장 + 별도 오프라인 전략 필요 |

현재 테스트 환경에서는 `PersistenceEnabled = false`가 안전하다. 실제 유저 기기에서는 캐시 충돌 자체는 발생하지 않지만, 오프라인 접근이 필요한 기능이 추가될 경우 별도 전략을 검토해야 한다.

---

## 검증 방법

1. Unity 에디터와 빌드된 클라이언트를 동시에 실행한다.
2. 두 클라이언트 모두 아래 로그 순서를 완주하는지 확인한다.

```
[Firebase] Initialized successfully.
[Firestore] Local persistence disabled.
[Auth] Firebase Authentication initialized.
[Auth] Firebase Ready
[Firestore] LoadOrCreate Start
[UserDataService] 기존 유저 데이터 로드  (또는 신규 유저 데이터 생성)
[Auth] UserData Loaded / Nickname: ..., ColorHex: ...
```

3. 어느 쪽 클라이언트도 `[Firestore] LoadOrCreate Start` 이후 중단되지 않으면 수정이 정상 적용된 것이다.
