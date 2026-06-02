using System.Threading.Tasks;

public class LoginPresenter
{
    private const int NicknameSaveTimeoutMs = 8000;

    private readonly LoginUIView view;
    private readonly IAuthService authService;
    private readonly ISceneLoader sceneLoader;
    private readonly string lobbySceneName;
    private bool isNicknameSaveInProgress;

    public LoginPresenter(LoginUIView view, IAuthService authService, ISceneLoader sceneLoader, string lobbySceneName)
    {
        this.view = view;
        this.authService = authService;
        this.sceneLoader = sceneLoader;
        this.lobbySceneName = lobbySceneName;
    }

    public void Initialize()
    {
        view.Initialize();
    }

    public async Task HandleGuestLoginAsync()
    {
        view.SetGuestLoginInteractable(false);
        view.SetStatus("로그인 중...");

        bool success = await authService.GuestLoginAsync();

        if (success)
        {
            view.SetStatus("로그인되었습니다.");

            if (authService.CurrentUserData.IsNicknameSet)
            {
                await LoadLobbySceneAsync();
                return;
            }

            OpenNicknamePanel();
            return;
        }

        view.SetStatus("게스트 로그인에 실패했습니다.");
        view.SetGuestLoginInteractable(true);
    }

    public async Task HandleConfirmNicknameAsync()
    {
        if (isNicknameSaveInProgress)
        {
            return;
        }

        string nickname = view.Nickname;

        if (!UserDataService.IsValidNickname(nickname))
        {
            view.ShowNotice("닉네임은 2자 이상 12자 이하로 입력해 주세요.");
            return;
        }

        isNicknameSaveInProgress = true;
        view.SetNicknameConfirmInteractable(false);
        view.SetStatus("닉네임 저장 중...");

        try
        {
            NicknameUpdateResult result = await UpdateNicknameWithTimeoutAsync(nickname);

            if (result == NicknameUpdateResult.Success)
            {
                view.SetStatus("닉네임이 저장되었습니다.");
                await LoadLobbySceneAsync();
                return;
            }

            view.ShowNotice(GetNicknameErrorMessage(result));
        }
        catch
        {
            view.ShowNotice("닉네임 저장에 실패했습니다.");
        }
        finally
        {
            isNicknameSaveInProgress = false;
            view.SetNicknameConfirmInteractable(true);
        }
    }

    private void OpenNicknamePanel()
    {
        string nickname = authService.CurrentUserData != null
            ? authService.CurrentUserData.Nickname
            : "";

        view.OpenNicknamePanel(nickname);
    }

    private string GetNicknameErrorMessage(NicknameUpdateResult result)
    {
        switch (result)
        {
            case NicknameUpdateResult.Duplicate:
                return "이미 사용 중인 닉네임입니다.";
            case NicknameUpdateResult.Invalid:
                return "닉네임은 2자 이상 12자 이하로 입력해 주세요.";
            case NicknameUpdateResult.NotLoggedIn:
                return "로그인이 필요합니다.";
            default:
                return "닉네임 저장에 실패했습니다.";
        }
    }

    private async Task<NicknameUpdateResult> UpdateNicknameWithTimeoutAsync(string nickname)
    {
        Task<NicknameUpdateResult> updateTask = authService.UpdateNicknameAsync(nickname);
        Task timeoutTask = Task.Delay(NicknameSaveTimeoutMs);

        Task completedTask = await Task.WhenAny(updateTask, timeoutTask);
        if (completedTask != updateTask)
        {
            return NicknameUpdateResult.Failed;
        }

        return await updateTask;
    }

    private async Task LoadLobbySceneAsync()
    {
        await sceneLoader.LoadSceneAsync(lobbySceneName);
    }
}
