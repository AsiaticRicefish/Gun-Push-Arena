using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Unity.Collections;
using Unity.VisualScripting;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    private PlayerInputAction input;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Visual")]
    [SerializeField] private Renderer playerRenderer;

    /// <summary>
    /// 플레이어의 고유 ID를 네트워크 변수로 관리합니다. 서버에서만 쓰기 권한이 있으며, 모든 클라이언트가 읽을 수 있습니다.
    /// </summary>
    public NetworkVariable<FixedString64Bytes> PlayerUid = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone, // 값을 읽을 수 있는 대상 → 모든 클라이언트 (Everyone)
        NetworkVariableWritePermission.Server   // 값을 쓸 수 있는 대상 → 서버만 (Server)
        );

    private void Awake()
    {
        input = new PlayerInputAction();

        if (playerRenderer == null)
        {
            playerRenderer = GetComponent<Renderer>();
        }
    }

    // 여기서 네트워크 스폰 시 초기화 작업을 수행할 수 있습니다(Netcode 기준 진입점)
    public override void OnNetworkSpawn()
    {
        Debug.Log($"[PlayerController] Spawned. IsOwner: {IsOwner}");

        PlayerUid.OnValueChanged += OnPlayerUidChanged;

        if (IsOwner)
        {
            input.Enable(); // Host만 입력 활성화

            // 로그인된 사용자의 UID를 서버로 전송하여 PlayerUid 네트워크 변수를 업데이트합니다.

            StartCoroutine(RegisterUidReady());
        }

        string currentUid = PlayerUid.Value.ToString();

        // 이미 PlayerUid가 설정되어 있는 경우 로그를 출력하여 확인합니다.
        if (!string.IsNullOrEmpty(currentUid))
        {
            ApplyPlayerVisual(currentUid);
        }

    }

    private IEnumerator RegisterUidReady()
    {
        // AuthManager가 준비될 때까지 대기
        while (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn)
        {
            yield return null;
        }

        RegisterUidServerRpc(AuthManager.Instance.UserId);
    }

    // 네트워크에서 제거될 때 입력을 비활성화하여 리소스 누수를 방지합니다
    public override void OnNetworkDespawn()
    {
        PlayerUid.OnValueChanged -= OnPlayerUidChanged;
        input.Disable();
    }

    private void Update()
    {
        if (!IsOwner) return;

        Vector2 moveInput = input.Player.Move.ReadValue<Vector2>();

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

    /// <summary>
    /// 클라이언트에서 로그인된 사용자의 UID를 서버로 전송하여 PlayerUid 네트워크 변수를 업데이트하는 RPC입니다.
    /// </summary>
    /// <param name="uid"></param>

    [ServerRpc]
    private void RegisterUidServerRpc(string uid)
    {
        PlayerUid.Value = uid;
        Debug.Log($"[PlayerController] Registered UID: {uid}");
    }

    /// <summary>
    /// PlayerUid 네트워크 변수의 값이 변경될 때마다 호출되는 콜백 메서드입니다. 변경된 UID 값을 로그로 출력하여 동기화 상태를 확인할 수 있도록 합니다.
    /// </summary>
    /// <param name="previousValue"></param>
    /// <param name="newValue"></param>
    private void OnPlayerUidChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        Debug.Log($"[PlayerController] UID Synced: {newValue}");
        ApplyPlayerVisual(newValue.ToString());
    }

    private void ApplyPlayerVisual(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return;

        string nickname = CreateNicknameFromUid(uid);
        Color color = CreateColorFromUid(uid);

        ApplyColor(color);

        Debug.Log($"[PlayerController] Applied Visuals - Nickname: {nickname}, Color: {color}");
    }

#region 플레이어 임시 커스터마이징

    private string CreateNicknameFromUid(string uid)
    {
        int length = Mathf.Min(4, uid.Length);
        return $"player_{uid.Substring(0, length)}";  
    }

    private Color CreateColorFromUid(string uid)
    {
        int hash = CreateStableHash(uid);

        UnityEngine.Random.InitState(hash);

        return new Color(
            UnityEngine.Random.Range(0.2f, 1f),
            UnityEngine.Random.Range(0.2f, 1f),
            UnityEngine.Random.Range(0.2f, 1f)
        );
    }

    /// <summary>
    /// UID 문자열을 입력으로 받아 안정적인 해시 값을 생성하는 메서드입니다. 
    /// FNV-1a 해시 알고리즘을 사용하여 동일한 UID에 대해 항상 동일한 해시 값을 반환하도록 구현되어 있습니다. 이 해시 값은 플레이어의 색상을 결정하는 데 사용됩니다.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>

    private int CreateStableHash(string value)
    {
        unchecked
        {
            int hash = (int)2166136261;

            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619;
            }

            return hash;
        }
    }

    private void ApplyColor(Color color)
    {
        if (playerRenderer != null)
        {
            playerRenderer.material.color = color;
        }
    }   


#endregion
}
