using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponRarity : MonoBehaviour
{
    public enum Rarity { Common, Uncommon, Rare, Legendary }

    [Header("Roll on Awake")]
    [Tooltip("If true, Awake() rolls rarity (weighted) and applies upgrades immediately.")]
    [SerializeField] private bool rollOnAwake = true;

    [Header("Current State (read-only at runtime)")]
    [SerializeField] private Rarity rarity = Rarity.Common;

    [Header("Rarity Weights (relative)")]
    [SerializeField] private float weightCommon = 60f;
    [SerializeField] private float weightUncommon = 25f;
    [SerializeField] private float weightRare = 12f;
    [SerializeField] private float weightLegendary = 3f;

    [Header("Roll Ranges (per picked upgrade)")]
    [Tooltip("Percent damage per roll, applied multiplicatively: 1.10 = +10%. Stored and undone as an additive delta.")]
    [SerializeField] private Vector2 damageMultRange = new Vector2(1.10f, 1.30f);
    [Tooltip("Flat damage added per roll.")]
    [SerializeField] private Vector2Int damageFlatAddRange = new Vector2Int(3, 12);

    [Tooltip("Attack speed = reduce WeaponTick.interval by this FRACTION per roll (we store actual applied delta and undo by adding it back).")]
    [SerializeField] private Vector2 atkSpeedFracRange = new Vector2(0.10f, 0.25f);
    [SerializeField] private Vector2 critChanceAddRange = new Vector2(0.05f, 0.20f);
    [SerializeField] private Vector2 critMultAddRange = new Vector2(0.25f, 1.00f);

    // Knife-only
    [SerializeField] private Vector2 knifeRadiusMultRange = new Vector2(1.10f, 1.30f);
    [SerializeField] private Vector2 knifeSplashRadiusMultRange = new Vector2(1.10f, 1.30f);
    [SerializeField] private Vector2 knifeLifestealAddRange = new Vector2(0.05f, 0.20f);
    [SerializeField] private Vector2Int knifeMaxTargetsAddRange = new Vector2Int(1, 3);

    // Shooter-only
    [SerializeField] private Vector2 shooterLifetimeAddRange = new Vector2(0.5f, 2.0f);
    [SerializeField] private Vector2 shooterForceAddRange = new Vector2(1.0f, 4.0f);
    [SerializeField] private Vector2Int shooterProjectilesAddRange = new Vector2Int(1, 2);

    // NOTE: accuracy previously used a multiplicative reduce (spread *= 1 - frac).
    // To guarantee clean undo using only adds/subtracts, we change it to subtract a stored delta:
    // delta = currentSpread * frac; spread -= delta; undo: spread += delta.
    [SerializeField] private Vector2 shooterSpreadReduceFracRange = new Vector2(0.10f, 0.35f);

    // cached
    private Knife knife;
    private SimpleShooter shooter;
    private WeaponTick tick;

    private readonly List<string> notes = new();

    // ——— Applied modifiers from the last roll (so we can undo by subtracting) ———
    [Serializable]
    private class AppliedMods
    {
        // Shared
        public int damageFlatAdded;                 // + to damage (knife/shooter)
        public int damageFromPercentAdded;          // + to damage computed from % (stored as flat int)

        // Tick
        public float tickIntervalDelta;             // interval reduced by this amount (undo by +delta)

        // Crits
        public float critChanceAdded;               // + to crit chance
        public float critMultAdded;                 // + to crit multiplier

        // Knife
        public float knifeLifestealAdded;           // + to lifesteal
        public float knifeRadiusDelta;              // + to radius (computed from mult; undo by -delta)
        public float knifeSplashRadiusDelta;        // + to splash radius (computed from mult)
        public int knifeMaxTargetsAdded;            // + to max targets

        // Shooter
        public float shooterLifetimeAdded;          // + to bullet lifetime
        public float shooterForceAdded;             // + to shoot force
        public int shooterProjectilesAdded;         // + to projectile count
        public float shooterSpreadDelta;            // amount subtracted from spread (undo by +delta)

        public bool HasAny =>
            damageFlatAdded != 0 || damageFromPercentAdded != 0 ||
            !Mathf.Approximately(tickIntervalDelta, 0f) ||
            !Mathf.Approximately(critChanceAdded, 0f) || !Mathf.Approximately(critMultAdded, 0f) ||
            !Mathf.Approximately(knifeLifestealAdded, 0f) || !Mathf.Approximately(knifeRadiusDelta, 0f) ||
            !Mathf.Approximately(knifeSplashRadiusDelta, 0f) || knifeMaxTargetsAdded != 0 ||
            !Mathf.Approximately(shooterLifetimeAdded, 0f) || !Mathf.Approximately(shooterForceAdded, 0f) ||
            shooterProjectilesAdded != 0 || !Mathf.Approximately(shooterSpreadDelta, 0f);
    }

    private readonly AppliedMods last = new();

    private void Awake()
    {
        knife = GetComponent<Knife>();
        shooter = GetComponent<SimpleShooter>();
        tick = GetComponent<WeaponTick>();

        if (rollOnAwake)
            RerollRarity();
    }

    public void RerollRarity()
    {
        rarity = RollWeightedRarity();
        RerollStats();
    }

    public void RerollStats()
    {
        // Remove previous roll effects (strictly by subtracting what we added last time)
        UndoLastApplied();

        notes.Clear();

        int rolls = rarity switch
        {
            Rarity.Common => 1,
            Rarity.Uncommon => 2,
            Rarity.Rare => 4,
            Rarity.Legendary => 5,
            _ => 1
        };

        var candidates = BuildCandidates();
        if (candidates.Count == 0)
        {
            WriteExtra(BlueWrap("<size=80%><i>No applicable upgrades.</i></size>"));
            return;
        }

        Shuffle(candidates);
        for (int i = 0; i < Mathf.Min(rolls, candidates.Count); i++)
            candidates[i].Invoke();

        var sb = new StringBuilder();
        sb.AppendLine($"<b>Rarity:</b> {FormatRarity(rarity)}");
        for (int i = 0; i < notes.Count; i++)
            sb.AppendLine(notes[i]);

        WriteExtra(BlueWrap($"<size=80%>{sb}</size>"));

        if (tick != null && Application.isPlaying)
            tick.ResetAndStart();
    }

    [ContextMenu("Rarity/Reroll Rarity + Stats")]
    private void Ctx_RerollAll() => RerollRarity();

    [ContextMenu("Rarity/Reroll Stats Only")]
    private void Ctx_RerollStats() => RerollStats();

    private void UndoLastApplied()
    {
        if (!last.HasAny) return;

        // Damage (flat + percent-as-flat)
        if (knife != null)
            knife.damage -= (last.damageFlatAdded + last.damageFromPercentAdded);
        if (shooter != null)
            shooter.damage -= (last.damageFlatAdded + last.damageFromPercentAdded);

        // Attack Speed (tick interval): we previously decreased interval by tickIntervalDelta; now add it back
        if (tick != null && !Mathf.Approximately(last.tickIntervalDelta, 0f))
            tick.interval += last.tickIntervalDelta;

        // Crits
        if (knife != null)
        {
            knife.critChance = Mathf.Clamp01(knife.critChance - last.critChanceAdded);
            knife.critMultiplier -= last.critMultAdded;
        }
        if (shooter != null)
        {
            shooter.critChance = Mathf.Clamp01(shooter.critChance - last.critChanceAdded);
            shooter.critMultiplier -= last.critMultAdded;
        }

        // Knife specifics
        if (knife != null)
        {
            knife.lifestealPercent = Mathf.Clamp01(knife.lifestealPercent - last.knifeLifestealAdded);
            knife.radius -= last.knifeRadiusDelta;
            knife.splashRadius -= last.knifeSplashRadiusDelta;
            knife.maxTargetsPerTick -= last.knifeMaxTargetsAdded;
        }

        // Shooter specifics
        if (shooter != null)
        {
            shooter.bulletLifetime -= last.shooterLifetimeAdded;
            shooter.shootForce -= last.shooterForceAdded;
            shooter.projectileCount -= last.shooterProjectilesAdded;
            shooter.spreadAngle += last.shooterSpreadDelta; // we subtracted this before, so add back
        }

        // Clear for next round
        Clear(last);
    }

    private static void Clear(AppliedMods m)
    {
        m.damageFlatAdded = 0;
        m.damageFromPercentAdded = 0;
        m.tickIntervalDelta = 0f;
        m.critChanceAdded = 0f;
        m.critMultAdded = 0f;
        m.knifeLifestealAdded = 0f;
        m.knifeRadiusDelta = 0f;
        m.knifeSplashRadiusDelta = 0f;
        m.knifeMaxTargetsAdded = 0;
        m.shooterLifetimeAdded = 0f;
        m.shooterForceAdded = 0f;
        m.shooterProjectilesAdded = 0;
        m.shooterSpreadDelta = 0f;
    }

    private Rarity RollWeightedRarity()
    {
        float c = Mathf.Max(0f, weightCommon);
        float u = Mathf.Max(0f, weightUncommon);
        float r = Mathf.Max(0f, weightRare);
        float l = Mathf.Max(0f, weightLegendary);
        float total = c + u + r + l;
        if (total <= 0f) return Rarity.Common;

        float roll = UnityEngine.Random.value * total;
        if (roll < c) return Rarity.Common; roll -= c;
        if (roll < u) return Rarity.Uncommon; roll -= u;
        if (roll < r) return Rarity.Rare;
        return Rarity.Legendary;
    }

    private List<Action> BuildCandidates()
    {
        var list = new List<Action>();

        if (knife != null || shooter != null) list.Add(Upgrade_DamageFlat);
        if (knife != null || shooter != null) list.Add(Upgrade_DamagePercentAsFlatDelta);
        if (tick != null) list.Add(Upgrade_FasterAttack_StoreDelta);
        if (knife != null || shooter != null) list.Add(Upgrade_Crit);
        if (knife != null) list.Add(Upgrade_KnifeLifesteal);
        if (knife != null) list.Add(Upgrade_KnifeSplashRadius_AsDelta);
        if (shooter != null) list.Add(Upgrade_ShooterProjectiles);
        if (knife != null) list.Add(Upgrade_KnifeMainRadius_AsDelta);
        if (shooter != null) list.Add(Upgrade_ShooterRange);
        if (knife != null) list.Add(Upgrade_KnifeMaxTargets);
        if (shooter != null) list.Add(Upgrade_ShooterAccuracy_AsDelta);

        return list;
    }

    // ——— Upgrades that record their applied deltas so we can undo later ———

    private void Upgrade_DamageFlat()
    {
        int add = UnityEngine.Random.Range(damageFlatAddRange.x, damageFlatAddRange.y + 1);
        if (knife != null) knife.damage += add;
        if (shooter != null) shooter.damage += add;
        last.damageFlatAdded += add;
        notes.Add($"+{add} Damage");
    }

    private void Upgrade_DamagePercentAsFlatDelta()
    {
        float mult = UnityEngine.Random.Range(damageMultRange.x, damageMultRange.y);
        float pct = (mult - 1f) * 100f;

        // Convert multiplicative to a flat delta to guarantee clean undo.
        if (knife != null)
        {
            int baseDamage = knife.damage;
            int delta = Mathf.RoundToInt(baseDamage * (mult - 1f));
            knife.damage += delta;
            last.damageFromPercentAdded += delta;
        }
        if (shooter != null)
        {
            int baseDamage = shooter.damage;
            int delta = Mathf.RoundToInt(baseDamage * (mult - 1f));
            shooter.damage += delta;
            last.damageFromPercentAdded += delta;
        }

        notes.Add($"+{pct:F0}% Damage");
    }

    private void Upgrade_FasterAttack_StoreDelta()
    {
        if (tick == null) return;

        float frac = UnityEngine.Random.Range(atkSpeedFracRange.x, atkSpeedFracRange.y);
        float before = tick.interval;
        float reduceBy = before * frac;                 // subtract a delta
        float newInterval = Mathf.Max(0.05f, before - reduceBy);
        float actuallyReduced = before - newInterval;   // respect clamp; store what we really changed
        tick.interval = newInterval;

        last.tickIntervalDelta += actuallyReduced;      // undo by adding this back
        notes.Add($"+{frac * 100f:F0}% Attack Speed");
    }

    private void Upgrade_Crit()
    {
        bool buffChance = UnityEngine.Random.value < 0.6f;
        if (buffChance)
        {
            float add = UnityEngine.Random.Range(critChanceAddRange.x, critChanceAddRange.y);
            if (knife != null) knife.critChance = Mathf.Clamp01(knife.critChance + add);
            if (shooter != null) shooter.critChance = Mathf.Clamp01(shooter.critChance + add);
            last.critChanceAdded += add;
            notes.Add($"+{add * 100f:F0}% Crit Chance");
        }
        else
        {
            float add = UnityEngine.Random.Range(critMultAddRange.x, critMultAddRange.y);
            if (knife != null) knife.critMultiplier += add;
            if (shooter != null) shooter.critMultiplier += add;
            last.critMultAdded += add;
            notes.Add($"+{add:F2} Crit Mult");
        }
    }

    private void Upgrade_KnifeLifesteal()
    {
        float add = UnityEngine.Random.Range(knifeLifestealAddRange.x, knifeLifestealAddRange.y);
        if (knife == null) return;
        knife.lifestealPercent = Mathf.Clamp01(knife.lifestealPercent + add);
        last.knifeLifestealAdded += add;
        notes.Add($"+{add * 100f:F0}% Lifesteal");
    }

    private void Upgrade_KnifeMainRadius_AsDelta()
    {
        if (knife == null) return;
        float mult = UnityEngine.Random.Range(knifeRadiusMultRange.x, knifeRadiusMultRange.y);
        float delta = knife.radius * (mult - 1f);   // convert to additive change
        knife.radius += delta;
        last.knifeRadiusDelta += delta;
        notes.Add($"+{(mult - 1f) * 100f:F0}% Range");
    }

    private void Upgrade_KnifeSplashRadius_AsDelta()
    {
        if (knife == null) return;
        float mult = UnityEngine.Random.Range(knifeSplashRadiusMultRange.x, knifeSplashRadiusMultRange.y);
        float delta = knife.splashRadius * (mult - 1f);
        knife.splashRadius += delta;
        last.knifeSplashRadiusDelta += delta;
        notes.Add($"+{(mult - 1f) * 100f:F0}% AOE");
    }

    private void Upgrade_KnifeMaxTargets()
    {
        if (knife == null) return;
        int add = UnityEngine.Random.Range(knifeMaxTargetsAddRange.x, knifeMaxTargetsAddRange.y + 1);
        knife.maxTargetsPerTick += add;
        last.knifeMaxTargetsAdded += add;
        notes.Add($"+{add} Max Targets");
    }

    private void Upgrade_ShooterProjectiles()
    {
        if (shooter == null) return;
        int add = UnityEngine.Random.Range(shooterProjectilesAddRange.x, shooterProjectilesAddRange.y + 1);
        shooter.projectileCount += add;
        last.shooterProjectilesAdded += add;
        notes.Add($"+{add} Projectiles");
    }

    private void Upgrade_ShooterRange()
    {
        if (shooter == null) return;

        bool buffLifetime = UnityEngine.Random.value < 0.5f;
        if (buffLifetime)
        {
            float add = UnityEngine.Random.Range(shooterLifetimeAddRange.x, shooterLifetimeAddRange.y);
            shooter.bulletLifetime += add;
            last.shooterLifetimeAdded += add;
            notes.Add($"+{add:F1}s Projectile Lifetime");
        }
        else
        {
            float add = UnityEngine.Random.Range(shooterForceAddRange.x, shooterForceAddRange.y);
            shooter.shootForce += add;
            last.shooterForceAdded += add;
            notes.Add($"+{add:F1} Projectile Speed");
        }
    }

    private void Upgrade_ShooterAccuracy_AsDelta()
    {
        if (shooter == null) return;

        float frac = UnityEngine.Random.Range(shooterSpreadReduceFracRange.x, shooterSpreadReduceFracRange.y);
        float before = shooter.spreadAngle;
        float delta = before * frac;                    // subtract this amount
        float newSpread = Mathf.Max(0f, before - delta);
        float actuallyReduced = before - newSpread;     // store the true reduction after clamp
        shooter.spreadAngle = newSpread;

        last.shooterSpreadDelta += actuallyReduced;     // undo by adding this back
        notes.Add($"+{frac * 100f:F0}% Accuracy");
    }

    // ——— UI helpers ———

    private void WriteExtra(string block)
    {
        if (knife != null) AppendIntoExtra(knife, block);
        if (shooter != null) AppendIntoExtra(shooter, block);
    }

    private void AppendIntoExtra(object component, string block)
    {
        if (component is Knife k)
        {
            string cur = k.extraTextField ?? "";
            string cleaned = RemoveRaritySection(cur).TrimEnd();
            k.extraTextField = string.IsNullOrEmpty(cleaned) ? block : (cleaned + "\n" + block);
        }
        else if (component is SimpleShooter s)
        {
            string cur = s.extraTextField ?? "";
            string cleaned = RemoveRaritySection(cur).TrimEnd();
            s.extraTextField = string.IsNullOrEmpty(cleaned) ? block : (cleaned + "\n" + block);
        }
    }

    private static string RemoveRaritySection(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int idx = s.IndexOf("<b>Rarity:</b>", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? s[..idx].TrimEnd() : s;
    }

    private static string BlueWrap(string inner) => $"<color=#00AEEF>{inner}</color>";

    private static string FormatRarity(Rarity r) => r switch
    {
        Rarity.Common => "<color=#B0B0B0>Common</color>",
        Rarity.Uncommon => "<color=#3EC46D>Uncommon</color>",
        Rarity.Rare => "<color=#3AA0FF>Rare</color>",
        Rarity.Legendary => "<color=#FFB347>Legendary</color>",
        _ => "Common"
    };

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
