using UnityEngine;
using Unity.Netcode;

public class NetworkStartUI : MonoBehaviour
{
     private void Start()
    {
         if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnDestroy()
    {
       if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

     public void StartHost()
    {
        bool result = NetworkManager.Singleton.StartHost();
        Debug.Log($"[Network] StartHost result: {result}");
    }

    public void StartClient()
    {
        bool result = NetworkManager.Singleton.StartClient();
        Debug.Log($"[Network] StartClient result: {result}");
    }

    public void StartServer()
    {
        bool result = NetworkManager.Singleton.StartServer();
        Debug.Log($"[Network] StartServer result: {result}");
    }
    
      private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[Network] Client Connected: {clientId}");
    }
}
