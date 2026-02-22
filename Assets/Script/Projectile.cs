using UnityEngine;
using System.Collections;

public class Projectile : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float damage = 25f;
    [SerializeField, Tooltip("Tag of objects this projectile should damage (e.g. Enemy, Player)")]
    private string damageTag = "Enemy";

    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 10f;

    [Header("Hit Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private bool spawnHitEffect = true;

    [Header("Advanced")]
    [SerializeField] private bool destroyOnHit = true;
    [SerializeField] private bool pierce = false;
    [SerializeField] private float pierceLifetime = 2f;

    // Runtime
    private Rigidbody rb;
    private int hitCount;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Projectile requires Rigidbody!", this);
            Destroy(gameObject);
            return;
        }

        // Optimal RB setup for projectiles
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Start()
    {
        // Auto-destroy after lifetime
        Destroy(gameObject, maxLifetime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Only react to objects with the configured damage tag
        if (!other.CompareTag(damageTag)) return;

        hitCount++;

        // Try to apply damage
        ApplyDamage(other.gameObject);

        // Visual feedback
        SpawnHitEffect(other.ClosestPoint(transform.position));

        // Destroy / pierce logic
        if (!pierce || hitCount >= 2)
        {
            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
            else
            {
                // Disable physics & collider, then destroy after delay
                enabled = false;
                rb.linearVelocity = Vector3.zero;
                if (TryGetComponent<Collider>(out var col)) col.enabled = false;
                StartCoroutine(DestroyAfter(pierceLifetime));
            }
        }
    }

    private void ApplyDamage(GameObject target)
    {
        // First try EnemyHealth (for enemies/NPCs)
        if (target.TryGetComponent<EnemyHealth>(out var enemyHealth))
        {
            enemyHealth.TakeDamage(damage);
            return;
        }

        // Then try PlayerHealth (for the player)
        if (target.TryGetComponent<PlayerHealth>(out var playerHealth))
        {
            playerHealth.TakeDamage(damage);
            return;
        }

        // Fallback - no health component found
        Debug.LogWarning($"Projectile hit '{target.name}' (tag: {target.tag}) but no EnemyHealth or PlayerHealth component found!", target);
    }

    private void SpawnHitEffect(Vector3 position)
    {
        if (!spawnHitEffect || hitEffectPrefab == null) return;

        GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
        Destroy(effect, 2f); // auto clean-up
    }

    private IEnumerator DestroyAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    // Public methods
    public void SetDamage(float newDamage) => damage = newDamage;
    public void SetDamageTag(string newTag) => damageTag = newTag;

    // Debug visualization
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, rb ? rb.linearVelocity.normalized * 5f : transform.forward * 5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 endPos = transform.position + (rb ? rb.linearVelocity : transform.forward) * maxLifetime;
        Gizmos.DrawLine(transform.position, endPos);
    }
}