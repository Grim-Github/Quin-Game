// BulletDamageTrigger.cs
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BulletDamageTrigger : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] public int damageAmount = 10;
    [Tooltip("How many successful damage hits this bullet can apply before it is destroyed.")]
    [SerializeField] public int penetration = 1;

    [Header("Filters")]
    [Tooltip("Only objects on these layers will be damaged.")]
    [SerializeField] private LayerMask damageLayers;
    [Tooltip("If the bullet touches any of these layers, it is destroyed immediately (e.g., walls/obstacles).")]
    [SerializeField] private LayerMask destroyOnTouchLayers;

    [Header("Impact VFX")]
    [Tooltip("Prefab to spawn at the impact point (both on block-hit and on damage).")]
    [SerializeField] private GameObject impactPrefab;
    [Tooltip("Also spawn impact when hitting a blocking layer.")]
    [SerializeField] private bool spawnOnBlockedHit = true;
    [Tooltip("Spawn impact when damaging a target.")]
    [SerializeField] private bool spawnOnDamageHit = true;

    // Track which healths we already hit (avoid duplicate damage on multi-collider targets)
    private readonly HashSet<SimpleHealth> _alreadyHit = new();

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // If it hits a blocked layer -> spawn impact + destroy immediately
        if (IsInLayerMask(other.gameObject, destroyOnTouchLayers))
        {
            if (spawnOnBlockedHit) SpawnImpactAt(other, transform.position);
            Destroy(gameObject);
            return;
        }

        // Only damage allowed layers
        if (!IsInLayerMask(other.gameObject, damageLayers)) return;

        // Support child colliders by searching up
        var health = other.GetComponentInParent<SimpleHealth>();
        if (health == null || !health.IsAlive) return;

        // Already hit this target? skip
        if (_alreadyHit.Contains(health)) return;

        // Apply damage
        health.TakeDamage(damageAmount);
        _alreadyHit.Add(health);

        if (spawnOnDamageHit) SpawnImpactAt(other, transform.position);

        // Consume penetration and destroy if spent
        penetration--;
        if (penetration <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void SpawnImpactAt(Collider2D other, Vector3 fallback)
    {
        if (impactPrefab == null) return;

        // Best-effort contact point for triggers
        Vector3 hitPos = fallback;
        try
        {
            Vector2 cp = other.ClosestPoint(transform.position);
            hitPos = new Vector3(cp.x, cp.y, fallback.z);
        }
        catch { /* ignore */ }

        Instantiate(impactPrefab, hitPos, Quaternion.identity);
    }

    private static bool IsInLayerMask(GameObject go, LayerMask mask)
    {
        return (mask.value & (1 << go.layer)) != 0;
    }
}
