// BulletDamageTrigger.cs
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BulletDamageTrigger : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] public int damageAmount = 10;
    [Tooltip("How many successful damage hits this bullet can apply before it is destroyed.")]
    [SerializeField] private int penetration = 1;

    [Header("Filters")]
    [Tooltip("Only objects on these layers will be damaged.")]
    [SerializeField] private LayerMask damageLayers;
    [Tooltip("If the bullet touches any of these layers, it is destroyed immediately (e.g., walls/obstacles).")]
    [SerializeField] private LayerMask destroyOnTouchLayers;

    // Track which healths we already hit (avoid duplicate damage on multi-collider targets)
    private readonly HashSet<SimpleHealth> _alreadyHit = new();

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Destroy immediately if it hits a blocked layer
        if (IsInLayerMask(other.gameObject, destroyOnTouchLayers))
        {
            Destroy(gameObject);
            return;
        }

        // Try to damage targets on allowed layers
        if (!IsInLayerMask(other.gameObject, damageLayers)) return;

        // Support child colliders by searching up the hierarchy
        var health = other.GetComponentInParent<SimpleHealth>();
        if (health == null || !health.IsAlive) return;

        // Already hit this target? skip
        if (_alreadyHit.Contains(health)) return;

        // Apply damage
        health.TakeDamage(damageAmount);
        _alreadyHit.Add(health);

        // Consume penetration and destroy if spent
        penetration--;
        if (penetration <= 0)
        {
            Destroy(gameObject);
        }
    }

    private static bool IsInLayerMask(GameObject go, LayerMask mask)
    {
        return (mask.value & (1 << go.layer)) != 0;
    }
}
