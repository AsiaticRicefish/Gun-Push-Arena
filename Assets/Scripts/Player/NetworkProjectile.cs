using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public sealed class NetworkProjectile : NetworkBehaviour
{
    private const float DefaultRadius = 0.08f;

    private Rigidbody2D rb;
    private Collider2D projectileCollider;
    private Vector2 direction = Vector2.right;
    private float speed = 12f;
    private float despawnTime;
    private ulong ownerNetworkObjectId;

    [SerializeField] private float lifetime = 2f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        projectileCollider = GetComponent<Collider2D>();
        if (projectileCollider == null)
        {
            CircleCollider2D circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.radius = DefaultRadius;
            projectileCollider = circleCollider;
        }

        projectileCollider.isTrigger = true;
    }

    public void Initialize(ulong ownerNetworkObjectId, Vector2 direction, float speed)
    {
        if (!IsServer)
        {
            return;
        }

        this.ownerNetworkObjectId = ownerNetworkObjectId;
        this.direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        this.speed = speed;
        despawnTime = Time.time + lifetime;
    }

    private void FixedUpdate()
    {
        if (!IsServer)
        {
            return;
        }

        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);

        if (Time.time >= despawnTime)
        {
            Despawn();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer)
        {
            return;
        }

        NetworkObject hitNetworkObject = other.GetComponentInParent<NetworkObject>();
        if (hitNetworkObject != null && hitNetworkObject.NetworkObjectId == ownerNetworkObjectId)
        {
            return;
        }

        PlayerController hitPlayer = other.GetComponentInParent<PlayerController>();
        if (hitPlayer != null)
        {
            if (!hitPlayer.ApplyProjectileHit(direction))
            {
                return;
            }
        }

        Despawn();
    }

    private void Despawn()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }
}
