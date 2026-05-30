using System.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class GameRoundManager : MonoBehaviour
{
    private const float CheckIntervalSeconds = 0.25f;

    private Coroutine watchCoroutine;
    private bool isRoundEnded;

    public void StartWatching(int expectedPlayerCount)
    {
        if (watchCoroutine != null)
        {
            StopCoroutine(watchCoroutine);
        }

        isRoundEnded = false;
        watchCoroutine = StartCoroutine(WatchRound(expectedPlayerCount));
    }

    private IEnumerator WatchRound(int expectedPlayerCount)
    {
        if (expectedPlayerCount <= 1)
        {
            yield break;
        }

        while (!isRoundEnded)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                yield break;
            }

            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            if (players.Length < expectedPlayerCount)
            {
                yield return new WaitForSeconds(CheckIntervalSeconds);
                continue;
            }

            int aliveCount = 0;
            PlayerController winner = null;

            foreach (PlayerController player in players)
            {
                if (!player.IsAlive)
                {
                    continue;
                }

                aliveCount++;
                winner = player;
            }

            if (aliveCount == 1 && winner != null)
            {
                EndRound(winner);
                yield break;
            }

            yield return new WaitForSeconds(CheckIntervalSeconds);
        }
    }

    private void EndRound(PlayerController winner)
    {
        isRoundEnded = true;
        Debug.Log($"[GameRoundManager] Round ended. WinnerClientId: {winner.OwnerClientId}");
        winner.AnnounceGameResultClientRpc(winner.OwnerClientId);
    }
}
