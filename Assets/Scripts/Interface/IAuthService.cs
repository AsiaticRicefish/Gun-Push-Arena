using System.Threading.Tasks;

public interface IAuthService
{
    string UserId { get; }
    bool IsLoggedIn { get; }
    UserData CurrentUserData { get; }

    Task<bool> GuestLoginAsync();
    Task<NicknameUpdateResult> UpdateNicknameAsync(string nickname);
}
