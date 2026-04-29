using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    private PlayerInputAction input;
    [SerializeField] private float moveSpeed = 5f;

    private void Awake()
    {
        input = new PlayerInputAction();
    }

    // 여기서 네트워크 스폰 시 초기화 작업을 수행할 수 있습니다(Netcode 기준 진입점)
    public override void OnNetworkSpawn()
    {
        Debug.Log($"[Player] Spawned / IsOwner: {IsOwner} / IsHost: {IsHost} / IsClient: {IsClient}");

        if (IsOwner)
        {
            input.Enable(); // Host만 입력 활성화
        }
    }

    // 네트워크에서 제거될 때 입력을 비활성화하여 리소스 누수를 방지합니다
    public override void OnNetworkDespawn()
    {
        input.Disable();
    }

    private void Update()
    {
        if (!IsOwner) return;

        Vector2 moveInput = input.Player.Move.ReadValue<Vector2>();

        if (moveInput != Vector2.zero)
        {
            Debug.Log($"[Input] {moveInput}");
        }

        // 입력에 따라 서버로 이동 명령을 보냅니다
        MoveServerRpc(moveInput);
    }

    /// <summary>
    /// 서버에서 클라이언트의 이동 입력을 받아 처리하는 RPC입니다.
    /// 해당 작업을 하지 않으면 클라이언트에서 이동 입력이 발생해도 서버에서 이를 인식하지 못하여 플레이어가 움직이지 않게 됩니다.
    /// </summary>
    /// <param name="moveInput"></param>
    [ServerRpc]
    private void MoveServerRpc(Vector2 moveInput)
    {
        Vector3 moveDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }
}