using UnityEngine;
using Firebase.Auth;
using Firebase.Extensions;
using System.Threading.Tasks;

public class AuthManager : GlobalSingleton<AuthManager>
{
    [Header("Test")]
    // 이 옵션을 활성화하면 매번 앱 시작 시 새로운 익명 사용자로 로그인하게 됩니다.
    // 테스트용이라 실제 게임에서는 비활성화할 예정입니다.
    [SerializeField] private bool AddUserTest;

    private FirebaseAuth auth;
    private FirebaseUser currentUser;
    private TaskCompletionSource<bool> authStateReady;

    // 로그인한 사용자의 고유 ID를 저장하는 프로퍼티입니다. 로그인 상태를 확인할 때 사용됩니다.
    public string UserId { get; private set; }
    // UserId가 null 또는 빈 문자열이 아닌 경우 로그인된 상태로 간주하는 프로퍼티입니다. 로그인 여부를 쉽게 확인할 수 있도록 도와줍니다.
    public bool IsLoggedIn => !string.IsNullOrEmpty(UserId);

    private UserDataService userDataService;
    public UserData CurrentUserData { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this) return;
    }

    /// <summary>
    /// Firebase 초기화가 완료될 때까지 대기한 후, Firebase Authentication을 초기화하고 익명 로그인 시도를 합니다.
    /// </summary>
    private async void Start()
    {
        await WaitForFirebase();
        InitAuth();
        await SignInAnonymously();
    }

    /// <summary>
    /// Firebase 초기화가 완료될 때까지 대기하는 비동기 메서드입니다. 
    /// FirebaseManager의 InReady 속성을 확인하여 초기화가 완료되었는지 판단합니다.
    /// Firebase는 비동기 초기화라서 바로 Auth 쓰면 터질 수 있습니다. 그래서 초기화 완료될 때까지 대기하는 로직이 필요합니다.
    /// </summary>
    /// <returns></returns>
    private async Task WaitForFirebase()
    {
        while (!FirebaseManager.Instance.InReady)
        {
            await Task.Yield();
        }
    }

    /// <summary>
    /// Firebase Authentication 초기화 메서드입니다. FirebaseAuth.DefaultInstance를 사용하여 auth 인스턴스를 초기화하고, 초기화 완료 로그를 출력합니다.
    /// </summary>
    private void InitAuth()
    {
        auth = FirebaseAuth.DefaultInstance;
        authStateReady = new TaskCompletionSource<bool>();
        auth.StateChanged += AuthStateChanged;
        userDataService = new UserDataService();

        AuthStateChanged(this, System.EventArgs.Empty);
        Debug.Log("[Auth] Firebase Authentication initialized.");
    }

    private void OnDestroy()
    {
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
            auth = null;
        }
    }

    private void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        FirebaseUser nextUser = auth.CurrentUser;

        if (currentUser != nextUser)
        {
            currentUser = nextUser;
            Debug.Log($"[Auth] StateChanged / UID: {currentUser?.UserId}");
        }

        authStateReady?.TrySetResult(true);
    }

    private async Task WaitForInitialAuthState()
    {
        if (authStateReady == null) return;

        while (!authStateReady.Task.IsCompleted)
        {
            await Task.Yield();
        }
    }

    /// <summary>
    /// 익명 로그인 시도 메서드입니다. SignInAnonymouslyAsync() 메서드를 사용하여 익명 로그인을 시도하고, 로그인 성공 시 UserId를 저장하고 로그를 출력합니다. 로그인 실패 시 예외를 캐치하여 에러 로그를 출력합니다.
    /// </summary>
    /// <returns></returns>
    private async Task SignInAnonymously()
    {
        try
        {
            Debug.Log("[Auth] Firebase Ready");

            await WaitForInitialAuthState();

            Debug.Log($"[Auth] CurrentUser before sign in: {currentUser?.UserId}");

            if (AddUserTest && currentUser != null)
            {
                auth.SignOut();
                currentUser = null;
                UserId = null;
                CurrentUserData = null;
                Debug.Log("[Auth] AddUserTest enabled. Signed out existing user.");
            }

            // 이미 로그인된 사용자가 있는 경우, 해당 사용자의 UID를 UserId에 저장합니다.
            // 이렇게 하면 앱이 재시작되거나 사용자가 이미 로그인된 상태에서 다시 로그인할 때
            // 기존 사용자 정보를 유지할 수 있습니다.
            if (currentUser != null && currentUser.IsValid())
            {
                UserId = currentUser.UserId;
                Debug.Log($"[Auth] Existing User / UID: {UserId}");
            }
            else
            {
                var result = await auth.SignInAnonymouslyAsync();
                currentUser = result.User;
                UserId = currentUser.UserId;
                Debug.Log($"[Auth] New Anonymous Login / UID: {UserId}");
            }

            Debug.Log($"[Auth] Final UID: {UserId}");

            Debug.Log("[Firestore] LoadOrCreate Start");

            CurrentUserData = await userDataService.GetUserDataAsync(UserId);
            if (CurrentUserData == null)
            {
                Debug.LogError($"[Auth] UserData load failed / UID: {UserId}");
                return;
            }

            Debug.Log($"[Auth] UserData Loaded / Nickname: {CurrentUserData.Nickname}, ColorHex: {CurrentUserData.ColorHex}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Auth] Login Failed: {e}");
        }
    }
}