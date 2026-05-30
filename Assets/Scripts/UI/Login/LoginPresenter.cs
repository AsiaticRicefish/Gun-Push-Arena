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
        view.SetStatus("Signing in...");

        bool success = await authService.GuestLoginAsync();

        if (success)
        {
            view.SetStatus("Signed in.");

            if (authService.CurrentUserData.IsNicknameSet)
            {
                await LoadLobbySceneAsync();
                return;
            }

            OpenNicknamePanel();
            return;
        }

        view.SetStatus("Guest login failed.");
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
            view.ShowNotice("Nickname must be 2-12 characters.");
            return;
        }

        isNicknameSaveInProgress = true;
        view.SetNicknameConfirmInteractable(false);
        view.SetStatus("Saving nickname...");

        try
        {
            NicknameUpdateResult result = await UpdateNicknameWithTimeoutAsync(nickname);

            if (result == NicknameUpdateResult.Success)
            {
                view.SetStatus("Nickname saved.");
                await LoadLobbySceneAsync();
                return;
            }

            view.ShowNotice(GetNicknameErrorMessage(result));
        }
        catch
        {
            view.ShowNotice("Nickname save failed.");
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
                return "Nickname is already taken.";
            case NicknameUpdateResult.Invalid:
                return "Nickname must be 2-12 characters.";
            case NicknameUpdateResult.NotLoggedIn:
                return "Login is required.";
            default:
                return "Nickname save failed.";
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
