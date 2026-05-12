using System.Threading.Tasks;

public class LoginPresenter
{
    private readonly LoginUIView view;
    private readonly IAuthService authService;
    private readonly ISceneLoader sceneLoader;
    private readonly string lobbySceneName;

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
        string nickname = view.Nickname;

        if (!IsValidNickname(nickname))
        {
            view.SetStatus("Nickname must be 2-12 characters.");
            return;
        }

        view.SetNicknameConfirmInteractable(false);
        view.SetStatus("Saving nickname...");

        bool success = await authService.UpdateNicknameAsync(nickname);

        if (success)
        {
            view.SetStatus("Nickname saved.");
            await LoadLobbySceneAsync();
            return;
        }

        view.SetStatus("Nickname save failed.");
        view.SetNicknameConfirmInteractable(true);
    }

    private void OpenNicknamePanel()
    {
        string nickname = authService.CurrentUserData != null
            ? authService.CurrentUserData.Nickname
            : "";

        view.OpenNicknamePanel(nickname);
    }

    private bool IsValidNickname(string nickname)
    {
        return nickname.Length >= 2 && nickname.Length <= 12;
    }

    private async Task LoadLobbySceneAsync()
    {
        await sceneLoader.LoadSceneAsync(lobbySceneName);
    }
}