using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    private const int DefaultLives = 3;
    private const int DefaultMaxJumpCount = 2;
    private const float MinimumJumpForce = 12f;
    private const float MaximumGravityScale = 2.5f;
    private const float DefaultRespawnInvulnerabilitySeconds = 3f;

    private static PhysicsMaterial2D noFrictionMaterial;

    private PlayerInputAction input;
    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private float serverMoveInput;
    private bool serverJumpRequested;
    private int serverFacingDirection = 1;
    private int remainingJumpCount;
    private Vector3 respawnPosition;
    private float deathY = -20f;
    private float invulnerableUntil;
    private Color currentPlayerColor = Color.white;

    // 로그인된 사용자의 UID를 서버로 전송하기 위해 IAuthService 인터페이스를 사용하여 AuthManager에 접근합니다.
    // 이를 통해 PlayerController가 AuthManager에 직접 의존하지 않고도 로그인 정보를 사용할 수 있도록 합니다.
    private IAuthService authService;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float gravityScale = 2.5f;
    [SerializeField] private int maxJumpCount = DefaultMaxJumpCount;
    [SerializeField] private LayerMask groundLayerMask = Physics2D.DefaultRaycastLayers;
    [SerializeField] private float groundCheckDistance = 0.08f;

    [Header("Combat")]
    [SerializeField] private NetworkObject projectilePrefab;
    [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.45f, 0.05f);
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float fireCooldown = 0.25f;
    [SerializeField] private Vector2 hitKnockback = new Vector2(8f, 4f);
    [SerializeField] private float knockbackControlLock = 0.18f;
    [SerializeField] private float respawnInvulnerabilitySeconds = DefaultRespawnInvulnerabilitySeconds;

    [Header("Visual")]
    [SerializeField] private Renderer playerRenderer;

    private float nextServerFireTime;
    private float knockbackControlLockUntil;

    /// <summary>
    /// 플레이어의 네트워크 동기화 데이터를 관리합니다. 서버에서만 쓰기 권한이 있으며, 모든 클라이언트가 읽을 수 있습니다.
    /// </summary>
    public NetworkVariable<PlayerNetworkData> PlayerData = new NetworkVariable<PlayerNetworkData>(
        default,
        NetworkVariableReadPermission.Everyone, // 값을 읽을 수 있는 대상 → 모든 클라이언트 (Everyone)
        NetworkVariableWritePermission.Server   // 값을 쓸 수 있는 대상 → 서버만 (Server)
        );

    public NetworkVariable<int> Lives = new NetworkVariable<int>(
        DefaultLives,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> IsInvulnerable = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool IsAlive => Lives.Value > 0;

    private void Awake()
    {
        input = new PlayerInputAction();
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        jumpForce = Mathf.Max(jumpForce, MinimumJumpForce);
        gravityScale = Mathf.Min(gravityScale, MaximumGravityScale);
        maxJumpCount = Mathf.Max(1, maxJumpCount);
        remainingJumpCount = maxJumpCount;

        if (rb != null)
        {
            rb.gravityScale = gravityScale;
        }

        if (playerCollider != null)
        {
            playerCollider.sharedMaterial = GetNoFrictionMaterial();
        }

        if (playerRenderer == null)
        {
            playerRenderer = GetComponent<Renderer>();
        }
    }

    public void Construct(IAuthService authService)
    {
        this.authService = authService;
    }

    public void InitializeRoundState(Vector3 spawnPosition, float deathY)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        respawnPosition = spawnPosition;
        this.deathY = deathY;
        Lives.Value = DefaultLives;
        remainingJumpCount = maxJumpCount;
        BeginInvulnerability();
    }

    // 여기서 네트워크 스폰 시 초기화 작업을 수행할 수 있습니다(Netcode 기준 진입점)
    public override void OnNetworkSpawn()
    {
        Debug.Log($"[PlayerController] Spawned. IsOwner: {IsOwner}");

        PlayerData.OnValueChanged += OnPlayerDataChanged;
        IsInvulnerable.OnValueChanged += OnInvulnerabilityChanged;

        if (IsOwner)
        {
            input.Enable(); // Host만 입력 활성화

            // 로그인된 사용자의 UID를 서버로 전송하여 PlayerData 네트워크 변수를 업데이트합니다.

            StartCoroutine(RegisterUidReady());
        }

        string currentUid = PlayerData.Value.Uid.ToString();

        // 이미 PlayerData가 설정되어 있는 경우 로그를 출력하여 확인합니다.
        if (!string.IsNullOrEmpty(currentUid))
        {
            ApplyPlayerVisual(PlayerData.Value);
        }

        UpdatePlayerVisualState();
    }

    private IEnumerator RegisterUidReady()
    {
        IAuthService auth = authService ?? AuthManager.Instance;

        // AuthManager가 준비될 때까지 대기
        while (auth == null || !auth.IsLoggedIn)
        {
            auth = authService ?? AuthManager.Instance;
            yield return null;
        }

        while (auth.CurrentUserData == null)
        {
            yield return null;
        }

        UserData userData = auth.CurrentUserData;
        PlayerNetworkData playerData = new PlayerNetworkData
        {
            Uid = string.IsNullOrEmpty(userData.Uid) ? auth.UserId : userData.Uid,
            Nickname = string.IsNullOrEmpty(userData.Nickname) ? "Player" : userData.Nickname,
            ColorHex = string.IsNullOrEmpty(userData.ColorHex) ? "#FFFFFF" : userData.ColorHex
        };

        RegisterPlayerDataServerRpc(playerData);
    }

    // 네트워크에서 제거될 때 입력을 비활성화하여 리소스 누수를 방지합니다
    public override void OnNetworkDespawn()
    {
        PlayerData.OnValueChanged -= OnPlayerDataChanged;
        IsInvulnerable.OnValueChanged -= OnInvulnerabilityChanged;
        input.Disable();
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (Lives.Value <= 0) return;

        Vector2 moveInput = input.Player.Move.ReadValue<Vector2>();
        bool jumpPressed = input.Player.Jump.WasPressedThisFrame();
        bool firePressed = input.Player.Fire.WasPressedThisFrame();

        // 입력에 따라 서버로 이동 명령을 보냅니다
        MoveServerRpc(moveInput.x, jumpPressed);

        if (firePressed)
        {
            FireServerRpc();
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || rb == null)
        {
            return;
        }

        if (Lives.Value <= 0)
        {
            rb.linearVelocity = Vector2.zero;
            serverMoveInput = 0f;
            serverJumpRequested = false;
            return;
        }

        if (transform.position.y < deathY)
        {
            HandleFallOut();
            return;
        }

        UpdateInvulnerabilityState();

        bool isGrounded = IsGrounded();
        if (isGrounded && rb.linearVelocity.y <= 0.01f)
        {
            remainingJumpCount = maxJumpCount;
        }

        Vector2 velocity = rb.linearVelocity;
        if (Time.time >= knockbackControlLockUntil)
        {
            velocity.x = serverMoveInput * moveSpeed;
        }

        if (serverJumpRequested && remainingJumpCount > 0)
        {
            velocity.y = jumpForce;
            remainingJumpCount--;
        }

        rb.linearVelocity = velocity;
        serverJumpRequested = false;
    }

    /// <summary>
    /// 서버에서 클라이언트의 이동 입력을 받아 처리하는 RPC입니다.
    /// 해당 작업을 하지 않으면 클라이언트에서 이동 입력이 발생해도 서버에서 이를 인식하지 못하여 플레이어가 움직이지 않게 됩니다.
    /// </summary>
    /// <param name="moveInput"></param>
    [ServerRpc]
    private void MoveServerRpc(float horizontalInput, bool jumpPressed)
    {
        if (Lives.Value <= 0)
        {
            return;
        }

        serverMoveInput = Mathf.Clamp(horizontalInput, -1f, 1f);
        if (serverMoveInput > 0.01f)
        {
            serverFacingDirection = 1;
        }
        else if (serverMoveInput < -0.01f)
        {
            serverFacingDirection = -1;
        }

        serverJumpRequested |= jumpPressed;
    }

    [ServerRpc]
    private void FireServerRpc()
    {
        if (Lives.Value <= 0)
        {
            return;
        }

        if (Time.time < nextServerFireTime)
        {
            return;
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning("[PlayerController] Projectile prefab is missing.");
            return;
        }

        nextServerFireTime = Time.time + fireCooldown;

        int fireDirection = serverFacingDirection == 0 ? 1 : serverFacingDirection;
        Vector3 spawnOffset = new Vector3(projectileSpawnOffset.x * fireDirection, projectileSpawnOffset.y, 0f);
        NetworkObject projectileObject = Instantiate(projectilePrefab, transform.position + spawnOffset, Quaternion.identity);
        NetworkProjectile projectile = projectileObject.GetComponent<NetworkProjectile>();

        projectileObject.Spawn();

        if (projectile != null)
        {
            projectile.Initialize(NetworkObjectId, new Vector2(fireDirection, 0f), projectileSpeed);
        }
    }

    private void HandleFallOut()
    {
        Lives.Value = Mathf.Max(0, Lives.Value - 1);
        serverMoveInput = 0f;
        serverJumpRequested = false;

        if (Lives.Value > 0)
        {
            Respawn();
            Debug.Log($"[PlayerController] Player fell. Lives: {Lives.Value}");
            return;
        }

        IsInvulnerable.Value = false;
        rb.linearVelocity = Vector2.zero;
        Debug.Log("[PlayerController] Player eliminated.");
    }

    private void Respawn()
    {
        rb.linearVelocity = Vector2.zero;
        rb.position = respawnPosition;
        transform.position = respawnPosition;
        remainingJumpCount = maxJumpCount;
        knockbackControlLockUntil = 0f;
        BeginInvulnerability();
    }

    public bool ApplyProjectileHit(Vector2 hitDirection)
    {
        if (!IsServer || rb == null || Lives.Value <= 0)
        {
            return false;
        }

        if (IsInvulnerable.Value)
        {
            return false;
        }

        Vector2 normalizedDirection = hitDirection.sqrMagnitude > 0f ? hitDirection.normalized : Vector2.right;
        Vector2 velocity = rb.linearVelocity;
        velocity.x = normalizedDirection.x * hitKnockback.x;
        velocity.y = Mathf.Max(velocity.y, hitKnockback.y);
        rb.linearVelocity = velocity;

        knockbackControlLockUntil = Time.time + knockbackControlLock;
        return true;
    }

    [ClientRpc]
    public void AnnounceGameResultClientRpc(ulong winnerClientId)
    {
        GameResultPresenter.ShowResult(winnerClientId);
    }

    private bool IsGrounded()
    {
        if (playerCollider == null)
        {
            return false;
        }

        Bounds bounds = playerCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y - groundCheckDistance * 0.5f);
        Vector2 size = new Vector2(bounds.size.x * 0.8f, groundCheckDistance);

        RaycastHit2D hit = Physics2D.BoxCast(
            origin,
            size,
            0f,
            Vector2.down,
            groundCheckDistance,
            groundLayerMask);

        return hit.collider != null && hit.collider != playerCollider;
    }

    /// <summary>
    /// 클라이언트에서 로그인된 사용자의 UID를 서버로 전송하여 PlayerData 네트워크 변수를 업데이트하는 RPC입니다.
    /// </summary>
    /// <param name="uid"></param>

    [ServerRpc]
    private void RegisterPlayerDataServerRpc(PlayerNetworkData playerData)
    {
        PlayerData.Value = playerData;

        Debug.Log($"[PlayerController] Registered PlayerData: {playerData.Uid}, {playerData.Nickname}, {playerData.ColorHex}");
    }

    /// <summary>
    /// PlayerData 네트워크 변수의 값이 변경될 때마다 호출되는 콜백 메서드입니다. 변경된 UID 값을 로그로 출력하여 동기화 상태를 확인할 수 있도록 합니다.
    /// </summary>
    /// <param name="previousValue"></param>
    /// <param name="newValue"></param>
    private void OnPlayerDataChanged(PlayerNetworkData previousValue, PlayerNetworkData newValue)
    {
        Debug.Log($"[PlayerController] PlayerData Synced: {newValue.Uid}, {newValue.Nickname}, {newValue.ColorHex}");
        ApplyPlayerVisual(newValue);
    }

    private void ApplyPlayerVisual(PlayerNetworkData playerData)
    {
        string uid = playerData.Uid.ToString();
        if (string.IsNullOrEmpty(uid)) return;

        string nickname = playerData.Nickname.ToString();
        string colorHex = playerData.ColorHex.ToString();

        if (!TryCreateColorHex(colorHex, out Color color))
        {
            color = Color.white;
            Debug.LogWarning($"[PlayerController] Invalid ColorHex: {colorHex}");
        }

        ApplyColor(color);

        Debug.Log($"[PlayerController] Applied Visuals - Nickname: {nickname}, Color: {color}");
    }

    private bool TryCreateColorHex(string colorHex, out Color color)
    {
        if (string.IsNullOrEmpty(colorHex))
        {
            color = default;
            return false;
        }

        return ColorUtility.TryParseHtmlString(colorHex, out color);
    }

    private void ApplyColor(Color color)
    {
        currentPlayerColor = color;
        UpdatePlayerVisualState();
    }

    private void BeginInvulnerability()
    {
        if (!IsServer)
        {
            return;
        }

        invulnerableUntil = Time.time + Mathf.Max(0f, respawnInvulnerabilitySeconds);
        IsInvulnerable.Value = respawnInvulnerabilitySeconds > 0f;
    }

    private void UpdateInvulnerabilityState()
    {
        if (!IsServer || !IsInvulnerable.Value)
        {
            return;
        }

        if (Time.time >= invulnerableUntil)
        {
            IsInvulnerable.Value = false;
        }
    }

    private void OnInvulnerabilityChanged(bool previousValue, bool newValue)
    {
        UpdatePlayerVisualState();
    }

    private void UpdatePlayerVisualState()
    {
        if (playerRenderer != null)
        {
            playerRenderer.material.color = IsInvulnerable.Value
                ? Color.Lerp(currentPlayerColor, Color.white, 0.55f)
                : currentPlayerColor;
        }
    }   

    private static PhysicsMaterial2D GetNoFrictionMaterial()
    {
        if (noFrictionMaterial != null)
        {
            return noFrictionMaterial;
        }

        noFrictionMaterial = new PhysicsMaterial2D("NoFriction")
        {
            friction = 0f,
            bounciness = 0f
        };

        return noFrictionMaterial;
    }
}
