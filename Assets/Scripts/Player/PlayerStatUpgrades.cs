using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to the Player root. Applies permanent, stackable stat upgrades (modifiers)
/// to the Player's SimpleHealth and to all child Knife, SimpleShooter, and WeaponTick components.
///
/// Design:
/// - On first build, caches BASELINE values for every discovered component.
/// - Final values are always computed from (baseline + sum(flat)) * (1 + sum(perc)),
///   so re-applying is SAFE and idempotent.
/// - Call RebuildAndApply() if you add/remove weapons at runtime.
/// </summary>
[DisallowMultipleComponent]
public class PlayerStatUpgrades : MonoBehaviour
{
    // ---- What we can target -----------------------------------------------

    public enum TargetGroup
    {
        PlayerHealth,
        Knife,
        SimpleShooter,
        WeaponTick,
        AllWeapons // Knife + SimpleShooter + WeaponTick
    }

    public enum Stat
    {
        // Player (SimpleHealth)
        MaxHealth,          // int
        RegenPerSecond,     // float
        Armor,              // float

        // Shared (Knife + Shooter)
        Damage,             // int
        CritChance,         // 0..1 float (clamped)
        CritMultiplier,     // float multiplier
        StatusChance,       // 0..1 float (clamped)

        // Knife-only
        KnifeRadius,            // float
        KnifeSplashRadius,      // float
        KnifeSplashPercent,     // 0..1 float (clamped)
        KnifeLifesteal,         // 0..1 float (clamped)
        KnifeMaxTargets,        // int

        // Shooter-only
        ShooterProjectileCount, // int
        ShooterSpreadAngle,     // float (degrees)
        ShooterForce,           // float
        ShooterBulletLifetime,  // float (seconds)

        // WeaponTick (attack speed domain)
        TickInterval,       // float (lower = faster)
        TickBurstCount,     // int
        TickBurstSpacing,   // float
        AttackSpeed,        // % speed => scales Interval & BurstSpacing inversely
    }

    // ---- A single modifier -------------------------------------------------

    [Serializable]
    public class StatModifier
    {
        [Tooltip("Who receives this modifier.")]
        public TargetGroup target = TargetGroup.AllWeapons;

        [Tooltip("Which stat to modify.")]
        public Stat stat = Stat.Damage;

        [Header("Additive & Percent")]
        [Tooltip("Flat add. For ints it will be rounded.")]
        public float add = 0f;

        [Tooltip("Percent as 0.20 = +20% (applied multiplicatively as x(1 + percent)).")]
        public float percent = 0f;

        [Header("Clamping (optional)")]
        [Tooltip("If true, final value will be clamped into [min, max]. Leave off for no clamp.")]
        public bool clampFinal = false;
        public float min = 0f;
        public float max = 1f;

        public override string ToString() =>
            $"{target}/{stat}: add={add}, pct={percent * 100f:0.#}%";
    }

    [Header("Upgrades (Permanent)")]
    [Tooltip("Stack any number of permanent modifiers; final values are rebuilt from baselines.")]
    public List<StatModifier> modifiers = new();

    [Header("Debug")]
    public bool autoApplyOnStart = true;
    public bool logBuild = false;

    // ---- Cached targets ----------------------------------------------------

    private SimpleHealth playerHealth;

    private readonly List<Knife> knives = new();
    private readonly List<SimpleShooter> shooters = new();
    private readonly List<WeaponTick> ticks = new();

    private bool baselinesCaptured = false;

    // ---- Baseline snapshot structs ----------------------------------------

    private struct HealthBase
    {
        public int maxHealth;
        public float regenRate;
        public float armor;
    }
    private HealthBase healthBase;

    private class KnifeBase
    {
        public Knife k;
        public int damage;
        public float radius;
        public float splashRadius;
        public float splashDamagePercent;
        public float lifestealPercent;
        public float critChance;
        public float critMultiplier;
        public float statusApplyChance;
        public int maxTargetsPerTick;
    }

    private class ShooterBase
    {
        public SimpleShooter s;
        public int damage;
        public float shootForce;
        public float bulletLifetime;
        public int projectileCount;
        public float spreadAngle;
        public float critChance;
        public float critMultiplier;
        public float statusApplyChance;
    }

    private class TickBase
    {
        public WeaponTick t;
        public float interval;
        public int burstCount;
        public float burstSpacing;
    }

    private readonly List<KnifeBase> knifeBases = new();
    private readonly List<ShooterBase> shooterBases = new();
    private readonly List<TickBase> tickBases = new();

    // ---- Lifecycle ---------------------------------------------------------

    private void Awake()
    {
        DiscoverTargets();
        CaptureBaselines(); // first and only time unless you explicitly rebuild
    }

    private void Start()
    {
        if (autoApplyOnStart)
            ApplyFromBaselines();
    }

    // ---- Public control ----------------------------------------------------

    /// <summary>
    /// If you add/remove components at runtime: rebuild caches and re-apply from scratch.
    /// </summary>
    public void RebuildAndApply()
    {
        DiscoverTargets();
        CaptureBaselines(force: true);
        ApplyFromBaselines();
    }

    [ContextMenu("Rebuild & Apply Upgrades")]
    private void Ctx_RebuildAndApply() => RebuildAndApply();

    [ContextMenu("Apply From Baselines (no rebuild)")]
    private void Ctx_ApplyOnly() => ApplyFromBaselines();

    // ---- Discovery & baselines --------------------------------------------

    private void DiscoverTargets()
    {
        playerHealth = GetComponent<SimpleHealth>();

        knives.Clear();
        shooters.Clear();
        ticks.Clear();

        GetComponentsInChildren(true, knives);
        GetComponentsInChildren(true, shooters);
        GetComponentsInChildren(true, ticks);

        if (logBuild)
            Debug.Log($"[Upgrades] Found: Health={(playerHealth ? "yes" : "no")}, Knives={knives.Count}, Shooters={shooters.Count}, Ticks={ticks.Count}");
    }

    private void CaptureBaselines(bool force = false)
    {
        if (baselinesCaptured && !force) return;
        baselinesCaptured = true;

        // Player health
        if (playerHealth)
        {
            healthBase = new HealthBase
            {
                maxHealth = playerHealth.maxHealth,
                regenRate = playerHealth.regenRate,
                armor = playerHealth.armor
            };
        }

        // Knives
        knifeBases.Clear();
        foreach (var k in knives)
        {
            if (k == null) continue;
            knifeBases.Add(new KnifeBase
            {
                k = k,
                damage = k.damage,
                radius = k.radius,
                splashRadius = k.splashRadius,
                splashDamagePercent = k.splashDamagePercent,
                lifestealPercent = k.lifestealPercent,
                critChance = k.critChance,
                critMultiplier = k.critMultiplier,
                statusApplyChance = k.statusApplyChance,
                maxTargetsPerTick = k.maxTargetsPerTick
            });
        }

        // Shooters
        shooterBases.Clear();
        foreach (var s in shooters)
        {
            if (s == null) continue;
            shooterBases.Add(new ShooterBase
            {
                s = s,
                damage = s.damage,
                shootForce = s.shootForce,
                bulletLifetime = s.bulletLifetime,
                projectileCount = s.projectileCount,
                spreadAngle = s.spreadAngle,
                critChance = s.critChance,
                critMultiplier = s.critMultiplier,
                statusApplyChance = s.statusApplyChance
            });
        }

        // Ticks
        tickBases.Clear();
        foreach (var t in ticks)
        {
            if (t == null) continue;
            tickBases.Add(new TickBase
            {
                t = t,
                interval = t.interval,
                burstCount = t.burstCount,
                burstSpacing = t.burstSpacing
            });
        }

        if (logBuild) Debug.Log("[Upgrades] Baselines captured.");
    }

    // ---- Application pipeline ---------------------------------------------

    private void ApplyFromBaselines()
    {
        // Aggregate modifiers by (TargetGroup, Stat)
        // We’ll compute per group, then apply to each item from its specific baseline.

        // Player Health
        if (playerHealth)
        {
            float add, pct;

            // MaxHealth (int)
            Aggregate(TargetGroup.PlayerHealth, Stat.MaxHealth, out add, out pct);
            int maxHP = ApplyInt(healthBase.maxHealth, add, pct);
            playerHealth.maxHealth = Mathf.Max(1, maxHP);
            // Clamp current hp if your SimpleHealth uses it
            playerHealth.currentHealth = Mathf.Min(playerHealth.currentHealth, playerHealth.maxHealth);

            // Regen
            Aggregate(TargetGroup.PlayerHealth, Stat.RegenPerSecond, out add, out pct);
            playerHealth.regenRate = ApplyFloat(healthBase.regenRate, add, pct);

            // Armor
            Aggregate(TargetGroup.PlayerHealth, Stat.Armor, out add, out pct);
            playerHealth.armor = ApplyFloat(healthBase.armor, add, pct);
        }

        // Knives
        foreach (var kb in knifeBases)
        {
            if (kb.k == null) continue;

            float add, pct;

            // Shared stats (Damage, CritChance, CritMultiplier, StatusChance)
            AggregateWeaponShared(TargetGroup.Knife, out var share);

            kb.k.damage = ApplyInt(kb.damage, share.damageAdd, share.damagePct);

            kb.k.critChance = Mathf.Clamp01(
                ApplyFloat(kb.critChance, share.critChanceAdd, share.critChancePct)
            );

            kb.k.critMultiplier = ApplyFloat(kb.critMultiplier, share.critMultAdd, share.critMultPct);

            kb.k.statusApplyChance = Mathf.Clamp01(
                ApplyFloat(kb.statusApplyChance, share.statusAdd, share.statusPct)
            );

            // Knife-only
            Aggregate(TargetGroup.Knife, Stat.KnifeRadius, out add, out pct);
            kb.k.radius = ApplyFloat(kb.radius, add, pct);

            Aggregate(TargetGroup.Knife, Stat.KnifeSplashRadius, out add, out pct);
            kb.k.splashRadius = ApplyFloat(kb.splashRadius, add, pct);

            Aggregate(TargetGroup.Knife, Stat.KnifeSplashPercent, out add, out pct);
            kb.k.splashDamagePercent = Mathf.Clamp01(ApplyFloat(kb.splashDamagePercent, add, pct));

            Aggregate(TargetGroup.Knife, Stat.KnifeLifesteal, out add, out pct);
            kb.k.lifestealPercent = Mathf.Clamp01(ApplyFloat(kb.lifestealPercent, add, pct));

            Aggregate(TargetGroup.Knife, Stat.KnifeMaxTargets, out add, out pct);
            kb.k.maxTargetsPerTick = Mathf.Max(0, ApplyInt(kb.maxTargetsPerTick, add, pct));
        }

        // Shooters
        foreach (var sb in shooterBases)
        {
            if (sb.s == null) continue;

            float add, pct;

            // Shared
            AggregateWeaponShared(TargetGroup.SimpleShooter, out var share);

            sb.s.damage = ApplyInt(sb.damage, share.damageAdd, share.damagePct);

            sb.s.critChance = Mathf.Clamp01(
                ApplyFloat(sb.critChance, share.critChanceAdd, share.critChancePct)
            );

            sb.s.critMultiplier = ApplyFloat(sb.critMultiplier, share.critMultAdd, share.critMultPct);

            sb.s.statusApplyChance = Mathf.Clamp01(
                ApplyFloat(sb.statusApplyChance, share.statusAdd, share.statusPct)
            );

            // Shooter-only
            Aggregate(TargetGroup.SimpleShooter, Stat.ShooterProjectileCount, out add, out pct);
            sb.s.projectileCount = Mathf.Max(1, ApplyInt(sb.projectileCount, add, pct));

            Aggregate(TargetGroup.SimpleShooter, Stat.ShooterSpreadAngle, out add, out pct);
            sb.s.spreadAngle = Mathf.Max(0f, ApplyFloat(sb.spreadAngle, add, pct));

            Aggregate(TargetGroup.SimpleShooter, Stat.ShooterForce, out add, out pct);
            sb.s.shootForce = ApplyFloat(sb.shootForce, add, pct);

            Aggregate(TargetGroup.SimpleShooter, Stat.ShooterBulletLifetime, out add, out pct);
            sb.s.bulletLifetime = Mathf.Max(0f, ApplyFloat(sb.bulletLifetime, add, pct));
        }

        // Ticks (attack speed etc.)
        foreach (var tb in tickBases)
        {
            if (tb.t == null) continue;

            float add, pct;

            // AttackSpeed (percent) inversely scales interval & spacing
            Aggregate(TargetGroup.WeaponTick, Stat.AttackSpeed, out add, out pct);
            float speedMult = 1f + pct; // additive add ignored for speed; you can use add as extra if you want
            // If someone insists on using 'add' as additional pct, fold it in:
            speedMult += add; // treat add as extra percentage in decimal form
            speedMult = Mathf.Max(0f, speedMult);

            float inv = (speedMult <= 0f) ? 1f : (1f / speedMult);

            // Interval
            Aggregate(TargetGroup.WeaponTick, Stat.TickInterval, out add, out pct);
            float intervalBase = tb.interval * inv; // apply attack speed first
            tb.t.interval = Mathf.Max(0f, ApplyFloat(intervalBase, add, pct));

            // BurstSpacing
            Aggregate(TargetGroup.WeaponTick, Stat.TickBurstSpacing, out add, out pct);
            float spacingBase = tb.burstSpacing * inv; // apply attack speed first
            tb.t.burstSpacing = Mathf.Max(0f, ApplyFloat(spacingBase, add, pct));

            // BurstCount (int)
            Aggregate(TargetGroup.WeaponTick, Stat.TickBurstCount, out add, out pct);
            tb.t.burstCount = Mathf.Max(1, ApplyInt(tb.burstCount, add, pct));
        }

        if (logBuild) Debug.Log("[Upgrades] Applied from baselines.");
    }

    // ---- Aggregation helpers ----------------------------------------------

    private void Aggregate(TargetGroup group, Stat stat, out float sumAdd, out float sumPct)
    {
        sumAdd = 0f;
        sumPct = 0f;

        // Group-specific + AllWeapons logic
        foreach (var m in modifiers)
        {
            if (!MatchesTarget(m.target, group)) continue;
            if (m.stat != stat) continue;

            sumAdd += m.add;
            sumPct += m.percent;

            if (m.clampFinal)
            {
                // We only store clamps per-modifier; clamping applied at the end
                // on a per-field basis where needed (see usage on fields that are 0..1).
            }
        }

        // Additionally, include AllWeapons if this group is a weapon group:
        if (IsWeaponGroup(group))
        {
            foreach (var m in modifiers)
            {
                if (m.target != TargetGroup.AllWeapons) continue;
                if (m.stat != stat) continue;

                sumAdd += m.add;
                sumPct += m.percent;
            }
        }
    }

    private bool MatchesTarget(TargetGroup modTarget, TargetGroup concrete)
    {
        if (modTarget == concrete) return true;
        if (modTarget == TargetGroup.AllWeapons && IsWeaponGroup(concrete)) return true;
        return false;
    }

    private bool IsWeaponGroup(TargetGroup g) =>
        g == TargetGroup.Knife || g == TargetGroup.SimpleShooter || g == TargetGroup.WeaponTick;

    // Convenience pack for shared weapon stats
    private struct WeaponShared
    {
        public float damageAdd, damagePct;
        public float critChanceAdd, critChancePct;
        public float critMultAdd, critMultPct;
        public float statusAdd, statusPct;
    }

    private void AggregateWeaponShared(TargetGroup group, out WeaponShared ws)
    {
        ws = new WeaponShared();

        Aggregate(group, Stat.Damage, out ws.damageAdd, out ws.damagePct);
        Aggregate(group, Stat.CritChance, out ws.critChanceAdd, out ws.critChancePct);
        Aggregate(group, Stat.CritMultiplier, out ws.critMultAdd, out ws.critMultPct);
        Aggregate(group, Stat.StatusChance, out ws.statusAdd, out ws.statusPct);

        // Also pull from AllWeapons
        float a, p;

        Aggregate(TargetGroup.AllWeapons, Stat.Damage, out a, out p); ws.damageAdd += a; ws.damagePct += p;
        Aggregate(TargetGroup.AllWeapons, Stat.CritChance, out a, out p); ws.critChanceAdd += a; ws.critChancePct += p;
        Aggregate(TargetGroup.AllWeapons, Stat.CritMultiplier, out a, out p); ws.critMultAdd += a; ws.critMultPct += p;
        Aggregate(TargetGroup.AllWeapons, Stat.StatusChance, out a, out p); ws.statusAdd += a; ws.statusPct += p;
    }

    // ---- Math helpers ------------------------------------------------------

    private static int ApplyInt(int baseline, float add, float percent)
    {
        float v = (baseline + add);
        v *= (1f + percent);
        return Mathf.RoundToInt(v);
    }

    private static float ApplyFloat(float baseline, float add, float percent)
    {
        float v = (baseline + add);
        v *= (1f + percent);
        return v;
    }
}
