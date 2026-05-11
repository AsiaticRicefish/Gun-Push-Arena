using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour, ISceneLoader
{
    public async Task LoadSceneAsync(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) 
        {
            Debug.LogError("[SceneLoader] 씬 이름이 비어 있습니다.");
            return;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName); // 씬을 비동기적으로 로드

        if (operation == null)
        {
            Debug.LogError($"[SceneLoader] 씬 {sceneName}을(를) 로드할 수 없습니다.");
            return;
        }

        while (!operation.isDone) // 씬이 완전히 로드될 때까지 대기
        {
            await Task.Yield();
        }
    }
}