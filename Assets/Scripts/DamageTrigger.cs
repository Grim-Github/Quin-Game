// BulletDamageTrigger.cs
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class BulletDamageTrigger : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] public SimpleHealth.DamageType damageType;
    [SerializeField] public int damageAmount = 10;
    [Tooltip("How many successful damage hits this bullet can apply before it is destroyed.")]
    [SerializeField] public int penetration = 1;

    [Header("Filters")]
    [Tooltip("Only objects on these layers will be damaged.")]
    [SerializeField] private LayerMask damageLayers = ~0;
    [Tooltip("If the bullet touches any of these layers, it is destroyed immediately (e.g., walls/obstacles).")]
    [SerializeField] private LayerMask destroyOnTouchLayers;

    [Header("On Hit Effects")]
    public bool applyStatusEffectOnHit = false;
    public float statusApplyChance = 1f; // optional: chance to apply on hit (0..1)
    public StatusEffectSystem.StatusType statusEffectOnHit = StatusEffectSystem.StatusType.Bleeding;
    [Tooltip("Duration in seconds for the applied status effect.")]
    public float statusEffectDuration = 3f;


    [Header("Impact VFX")]
    [Tooltip("Prefab to spawn at the impact point (both on block-hit and on damage).")]
    [SerializeField] private GameObject impactPrefab;
    [Tooltip("Also spawn impact when hitting a blocking layer.")]
    [SerializeField] private bool spawnOnBlockedHit = true;
    [Tooltip("Spawn impact when damaging a target.")]
    [SerializeField] private bool spawnOnDamageHit = true;

    // Track which healths we already hit (avoid duplicate damage on multi-collider targets)
    private readonly HashSet<SimpleHealth> _alreadyHit = new();

    private Collider2D _col;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col != null) _col.isTrigger = true;

        // IMPORTANT: Don't touch impactPrefab's components here.
        // Shooter sets 'damageAmount' (incl. crit) AFTER Instantiate.
        // We will copy 'damageAmount' to the *spawned impact instance* at spawn time.
    }

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
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
        health.TakeDamage(damageAmount, damageType);
        _alreadyHit.Add(health);

        // ---- Apply status effect (if enabled and target supports it) ----
        if (applyStatusEffectOnHit && statusEffectDuration > 0f)
        {
            var statusSys = other.GetComponentInParent<StatusEffectSystem>();
            if (statusSys != null)
            {
                if (Random.Range(0f, 1f) <= statusApplyChance)
                {
                    statusSys.AddStatus(statusEffectOnHit, statusEffectDuration);
                }
                // This will refresh if the same status already exists (per your StatusEffectSystem)

            }
        }

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

        // Instantiate the impact and copy the *current* bullet damage to it (if it has ExplosionDamage2D)
        var impactInstance = Instantiate(impactPrefab, hitPos, Quaternion.identity);
        if (impactInstance.TryGetComponent<ExplosionDamage2D>(out var explosionInstance))
        {
            // Use current damageAmount (already includes crits if SimpleShooter set it)
            explosionInstance.baseDamage = damageAmount;
        }
    }

    private static bool IsInLayerMask(GameObject go, LayerMask mask)
    {
        return (mask.value & (1 << go.layer)) != 0;
    }
}
