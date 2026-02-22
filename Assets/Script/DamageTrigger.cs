using UnityEngine;

public class DamageTrigger : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private string damageTag = "Enemy"; // Set to "Player" or "Enemy"

    void OnTriggerEnter(Collider other)
    {
        // Only damage objects with matching tag
        if (!other.CompareTag(damageTag)) return;

        // Apply damage (works with EnemyHealth or PlayerHealth)
        ApplyDamage(other.gameObject);
    }

    private void ApplyDamage(GameObject target)
    {
        // Try EnemyHealth first
        if (target.TryGetComponent<EnemyHealth>(out var enemyHealth))
        {
            enemyHealth.TakeDamage(damage);
            return;
        }

        // Then PlayerHealth
        if (target.TryGetComponent<PlayerHealth>(out var playerHealth))
        {
            playerHealth.TakeDamage(damage);
            return;
        }

        // Warning if no health component
        Debug.LogWarning($"DamageTrigger hit '{target.name}' (tag: {target.tag}) but no EnemyHealth or PlayerHealth found!");
    }
}