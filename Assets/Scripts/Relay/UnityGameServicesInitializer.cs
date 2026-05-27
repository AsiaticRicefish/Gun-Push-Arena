using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Relay API를 사용하기 전에 Unity Gaming Services 초기화와 Unity Services 익명 로그인을 보장합니다.
/// Firebase Auth와는 별개의 인증이며, Relay/UGS 호출 권한을 얻기 위한 Unity Services 로그인입니다.
/// </summary>
public sealed class UnityGameServicesInitializer
{
    private bool isInitializing;
    private bool isInitialized;

    public bool IsInitialized => isInitialized &&
                                 UnityServices.State == ServicesInitializationState.Initialized &&
                                 AuthenticationService.Instance.IsSignedIn;

    /// <summary>
    /// Relay API 호출 전에 반드시 거치는 진입점입니다.
    /// 이미 초기화와 로그인이 끝났다면 바로 true를 반환합니다.
    /// </summary>
    public async UniTask<bool> EnsureInitializedAsync()
    {
        if (IsInitialized)
        {
            return true;
        }

        if (isInitializing)
        {
            await WaitUntilInitializedAsync();
            return IsInitialized;
        }

        isInitializing = true;

        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                Debug.Log("[UGS] Initializing Unity Services...");
                await UnityServices.InitializeAsync().AsUniTask();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[UGS] Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync().AsUniTask();
            }

            isInitialized = UnityServices.State == ServicesInitializationState.Initialized &&
                            AuthenticationService.Instance.IsSignedIn;

            Debug.Log($"[UGS] Initialized. PlayerId: {AuthenticationService.Instance.PlayerId}");

            return isInitialized;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UGS] Initialization failed: {e}");
            isInitialized = false;
            return false;
        }
        finally
        {
            isInitializing = false;
        }
    }

    private async UniTask WaitUntilInitializedAsync()
    {
        while (isInitializing)
        {
            await UniTask.Yield();
        }
    }
}
