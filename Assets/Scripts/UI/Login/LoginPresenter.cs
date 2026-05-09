using System;
using System.Threading.Tasks;

public class LoginPresenter
{
    private readonly LoginUIView view;
    private readonly IAuthService authService;
    private readonly Action loadLobbyScene;

    public LoginPresenter(LoginUIView view, IAuthService authService, Action loadLobbyScene)
    {
        this.view = view;
        this.authService = authService;
        this.loadLobbyScene = loadLobbyScene;
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
                loadLobbyScene.Invoke();
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
            loadLobbyScene.Invoke();
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
}
