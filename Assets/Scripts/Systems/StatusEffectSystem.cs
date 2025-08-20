using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight status effect system with add/remove/has, events, and ticking.
/// Re-adding the same effect REFRESHES its duration (no stacking).
/// Includes built-in Bleeding that damages a SimpleHealth component each tick.
/// </summary>
[AddComponentMenu("Gameplay/Status Effect System")]
public class StatusEffectSystem : MonoBehaviour
{
    // ---- Public API ----
    public enum StatusType
    {
        Bleeding = 0,
        Stun = 1,
        Speed = 2,
        Rush = 3,
        Ignite = 4,
        Shock = 5,
        Poison = 6,
        Frozen = 7,
        // Add more: Poison, Stunned, Shielded, etc.
    }

    /// <summary>Events for effect lifecycle and ticks.</summary>
    public event Action<StatusType> OnStart;
    public event Action<StatusType> OnEnd;
    public event Action<StatusType, int> OnTick; // tick index (1-based) for that effect

    [Header("Time")]
    [Tooltip("If true, uses unscaled time (ignores slow-mo/pauses).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Bleeding")]
    [Tooltip("If true, Bleeding ticks will call SimpleHealth.TakeDamage().")]
    [SerializeField] private bool enableBleeding = true;

    [Tooltip("Damage applied per tick while Bleeding is active (rounded to int).")]
    [SerializeField] public float bleedingDamagePerTick = 5f;

    [Header("Ignite")]
    [Tooltip("If true, Ignite ticks will call SimpleHealth.TakeDamage().")]
    [SerializeField] private bool enableIgnite = true;

    [Tooltip("Damage applied per tick while Ignite is active (rounded to int).")]
    [SerializeField] public float igniteDamagePerTick = 5f;

    [Header("Poison")]
    [Tooltip("If true, Poison ticks will call SimpleHealth.TakeDamage().")]
    [SerializeField] private bool enablePoison = true;

    [Tooltip("Damage applied per tick while Poison is active (rounded to int).")]
    [SerializeField] public float poisonDamagePerTick = 5f;

    [Tooltip("Optional: target health. If not set, auto-finds on this GameObject.")]
    [SerializeField] private SimpleHealth health; // your health system

    [Header("UnityEvent Defaults")]
    [SerializeField, Min(0.01f)] private float defaultDuration = 5f;
    [SerializeField, Min(0f)] private float defaultTickInterval = 1f;


    // ---- Internal model ----
    private class Effect
    {
        public StatusType type;
        public float remaining;        // seconds left
        public float tickInterval;     // seconds between ticks
        public float tickTimer;        // accumulates until tick
        public int tickCount;          // number of ticks fired for this activation
    }

    private readonly Dictionary<StatusType, Effect> _active = new Dictionary<StatusType, Effect>(8);
    private static readonly List<StatusType> _keysCache = new List<StatusType>(8);

    private void Awake()
    {
        if (health == null)
            health = GetComponent<SimpleHealth>();
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f || _active.Count == 0) return;

        _keysCache.Clear();
        _keysCache.AddRange(_active.Keys);

        foreach (var type in _keysCache)
        {
            var e = _active[type];
            e.remaining -= dt;
            e.tickTimer += dt;

            // Fire all ticks that fit this frame
            while (e.tickInterval > 0f && e.tickTimer >= e.tickInterval && e.remaining > 0f)
            {
                e.tickTimer -= e.tickInterval;
                e.tickCount++;
                OnTick?.Invoke(type, e.tickCount);
                HandleBuiltIn(type);
            }

            if (e.remaining <= 0f)
            {
                _active.Remove(type);
                OnEnd?.Invoke(type);
            }
        }
    }

    /// <summary>
    /// UnityEvent-friendly: apply by enum int (cast). 
    /// In the Inspector you can pass an int for the enum (0=Bleeding, 1=Stun, 2=Speed...).
    /// Uses defaultDuration and defaultTickInterval.
    /// </summary>
    public void ApplyStatusEffect_Int(int statusTypeInt)
    {
        var type = (StatusType)Mathf.Clamp(statusTypeInt, 0, Enum.GetNames(typeof(StatusType)).Length - 1);
        AddStatus(type, defaultDuration, defaultTickInterval);
    }


    /// <summary>
    /// Adds or REFRESHES a status effect.
    /// If already active, resets remaining time to 'duration' (no stacking),
    /// and resets tick cadence. Does NOT fire OnStart again on refresh.
    /// </summary>

    public void AddStatus(StatusType type, float duration, float tickInterval = 1f)
    {
        duration = Mathf.Max(0f, duration);
        if (duration <= 0f) return;

        if (_active.TryGetValue(type, out var e))
        {
            // ---- REFRESH: duration only ----
            e.remaining = duration;              // reset remaining time
                                                 // keep e.tickInterval, e.tickTimer, e.tickCount as-is
                                                 // ignore tickInterval parameter on refresh
        }
        else
        {
            e = new Effect
            {
                type = type,
                remaining = duration,
                tickInterval = Mathf.Max(0f, tickInterval),
                tickTimer = 0f,
                tickCount = 0
            };
            _active.Add(type, e);
            OnStart?.Invoke(type);
        }
    }


    /// <summary>Remove a status immediately (fires OnEnd).</summary>
    public void RemoveStatus(StatusType type)
    {
        if (_active.Remove(type))
            OnEnd?.Invoke(type);
    }

    /// <summary>True if the status is currently active.</summary>
    public bool HasStatus(StatusType type) => _active.ContainsKey(type);

    /// <summary>Remaining seconds for a status (0 if not active).</summary>
    public float GetRemainingTime(StatusType type)
    {
        return _active.TryGetValue(type, out var e) ? Mathf.Max(0f, e.remaining) : 0f;
    }

    /// <summary>Clears all statuses (fires OnEnd for each).</summary>
    public void ClearAll()
    {
        _keysCache.Clear();
        _keysCache.AddRange(_active.Keys);
        foreach (var k in _keysCache)
            OnEnd?.Invoke(k);
        _active.Clear();
    }


    /// <summary>
    /// Sets bleeding damage per tick. 
    /// If the given amount is higher than the current bleed damage, it replaces it.
    /// </summary>
    public void SetBleedDamage(float amount)
    {
        if (amount <= 0f) return;

        if (amount > bleedingDamagePerTick)
            bleedingDamagePerTick = amount;
    }

    /// <summary>
    /// Sets ignite damage per tick.
    /// If the given amount is higher than the current ignite damage, it replaces it.
    /// </summary>
    public void SetIgniteDamage(float amount)
    {
        if (amount <= 0f) return;

        if (amount > igniteDamagePerTick)
            igniteDamagePerTick = amount;
    }

    /// <summary>
    /// Sets poison damage per tick.
    /// If the given amount is higher than the current poison damage, it replaces it.
    /// </summary>
    public void SetPoisonDamage(float amount)
    {
        if (amount <= 0f) return;

        if (amount > poisonDamagePerTick)
            poisonDamagePerTick = amount;
    }


    // ---- Built-in handlers ----
    private void HandleBuiltIn(StatusType type)
    {
        if (type == StatusType.Bleeding && enableBleeding && bleedingDamagePerTick > 0f)
        {
            if (health != null)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(bleedingDamagePerTick));
                // Uses your health system's public API:
                // SimpleHealth.TakeDamage(int amount)
                health.TakeDamage(dmg, SimpleHealth.DamageType.Physical, false, false); // will handle armor, invuln, popup, etc. :contentReference[oaicite:1]{index=1}
            }
            // If no health found, we silently skip (no debug spam).
        }

        if (type == StatusType.Ignite && enableIgnite && igniteDamagePerTick > 0f)
        {
            if (health != null)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(igniteDamagePerTick));
                // Uses your health system's public API:
                // SimpleHealth.TakeDamage(int amount)
                health.TakeDamage(dmg, SimpleHealth.DamageType.Fire, false, false);
            }
            // If no health found, we silently skip (no debug spam).
        }

        if (type == StatusType.Poison && enablePoison && poisonDamagePerTick > 0f)
        {
            if (health != null)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(poisonDamagePerTick));
                // Uses your health system's public API:
                // SimpleHealth.TakeDamage(int amount)
                health.TakeDamage(dmg, SimpleHealth.DamageType.Poison, false, false);
            }
            // If no health found, we silently skip (no debug spam).
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Add Bleeding (5s, 1s tick)")]
    private void _TestAddBleeding() => AddStatus(StatusType.Bleeding, 5f, 1f);

    [ContextMenu("Test: Add Ignite (5s, 1s tick)")]
    private void _TestAddIgnite() => AddStatus(StatusType.Ignite, 5f, 1f);

    [ContextMenu("Test: Add Poison (5s, 1s tick)")]
    private void _TestAddPoison() => AddStatus(StatusType.Poison, 5f, 1f);
#endif
}
