using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 씬 로딩을 담당하는 인터페이스입니다. 
/// </summary>
public interface ISceneLoader
{
    public Task LoadSceneAsync(string sceneName);
}