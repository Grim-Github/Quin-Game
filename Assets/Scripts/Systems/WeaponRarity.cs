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

    // =========================
    // Base Roll Ranges (pre-tier)
    // =========================
    [Header("Roll Ranges (per picked upgrade)")]
    [Tooltip("Percent damage per roll, as a multiplier: 1.10 = +10%. We convert to a flat delta for clean undo.")]
    [SerializeField] private Vector2 damageMultRange = new Vector2(1.10f, 1.30f);
    [Tooltip("Flat damage added per roll.")]
    [SerializeField] private Vector2Int damageFlatAddRange = new Vector2Int(3, 12);

    [Tooltip("Attack speed = reduce WeaponTick.interval by this FRACTION per roll.")]
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

    // Accuracy: fraction to reduce spread by (we store a delta for clean undo)
    [SerializeField] private Vector2 shooterSpreadReduceFracRange = new Vector2(0.10f, 0.35f);

    // =========================
    // TIER SYSTEM (1 = strongest, 10 = weakest)
    // Each tier multiplies that stat's effective roll range.
    // Default mapping if curve is not used: tier 10 -> 0.5x ... tier 1 -> 2.0x
    // =========================
    [Header("Tier Settings (1 = strongest, 10 = weakest)")]
    [SerializeField, Range(1, 10)] private int tierDamagePercent = 5;
    [SerializeField, Range(1, 10)] private int tierDamageFlat = 5;
    [SerializeField, Range(1, 10)] private int tierAttackSpeed = 5;
    [SerializeField, Range(1, 10)] private int tierCritChance = 5;
    [SerializeField, Range(1, 10)] private int tierCritMultiplier = 5;

    [Header("Tier Settings • Knife")]
    [SerializeField, Range(1, 10)] private int tierKnifeRadius = 5;
    [SerializeField, Range(1, 10)] private int tierKnifeSplashRadius = 5;
    [SerializeField, Range(1, 10)] private int tierKnifeLifesteal = 5;
    [SerializeField, Range(1, 10)] private int tierKnifeMaxTargets = 5;

    [Header("Tier Settings • Shooter")]
    [SerializeField, Range(1, 10)] private int tierShooterLifetime = 5;
    [SerializeField, Range(1, 10)] private int tierShooterForce = 5;
    [SerializeField, Range(1, 10)] private int tierShooterProjectiles = 5;
    [SerializeField, Range(1, 10)] private int tierShooterAccuracy = 5;

    [Header("Tier Curve (optional override)")]
    [Tooltip("Optional mapping from tier (1..10) to multiplier. X expects normalized (0=Tier10,1=Tier1). Y = multiplier.")]
    [SerializeField] private AnimationCurve tierMultiplierCurve;
    [Tooltip("If true and curve has keys, the curve overrides the default 0.5x..2.0x mapping.")]
    [SerializeField] private bool useTierCurve = false;
    [Tooltip("Default min/max multipliers when not using curve (Tier10=min, Tier1=max).")]
    [SerializeField] private Vector2 defaultTierMultiplierRange = new Vector2(0.5f, 2.0f);

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
        public int damageFlatAdded;
        public int damageFromPercentAdded;

        // Tick
        public float tickIntervalDelta;

        // Crits
        public float critChanceAdded;
        public float critMultAdded;

        // Knife
        public float knifeLifestealAdded;
        public float knifeRadiusDelta;
        public float knifeSplashRadiusDelta;
        public int knifeMaxTargetsAdded;

        // Shooter
        public float shooterLifetimeAdded;
        public float shooterForceAdded;
        public int shooterProjectilesAdded;
        public float shooterSpreadDelta;

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
        {
            RerollRarity();
            RollTiers();
        }

    }

    public void RerollRarity()
    {
        rarity = RollWeightedRarity();
        RerollStats();
    }

    public void RerollStats()
    {
        // ✅ Reroll tiers every time we reroll stats
        RollTiers();

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

        // Attack Speed (tick interval)
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
            shooter.spreadAngle += last.shooterSpreadDelta; // undo
        }

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

    // =========================
    // Tier helpers
    // =========================
    private float TierMult(int tier)
    {
        tier = Mathf.Clamp(tier, 1, 10);

        if (useTierCurve && tierMultiplierCurve != null && tierMultiplierCurve.length > 0)
        {
            // Map Tier10..Tier1 -> 0..1
            float x = (10 - tier) / 9f; // tier=10 -> 0, tier=1 -> 1
            return Mathf.Max(0f, tierMultiplierCurve.Evaluate(x));
        }

        // Default linear mapping: Tier10=min .. Tier1=max
        float t = (10 - tier) / 9f; // 0..1
        return Mathf.Lerp(Mathf.Max(0f, defaultTierMultiplierRange.x),
                          Mathf.Max(0f, defaultTierMultiplierRange.y), t);
    }

    // Multiply a normal additive range by tier multiplier
    private Vector2 ScaleRange(Vector2 baseRange, int tier)
    {
        float m = TierMult(tier);
        return new Vector2(baseRange.x * m, baseRange.y * m);
    }

    private Vector2Int ScaleRange(Vector2Int baseRange, int tier, int minClamp = int.MinValue)
    {
        float m = TierMult(tier);
        int x = Mathf.RoundToInt(baseRange.x * m);
        int y = Mathf.RoundToInt(baseRange.y * m);
        if (x > y) (x, y) = (y, x);
        x = Mathf.Max(minClamp, x);
        y = Mathf.Max(minClamp, y);
        return new Vector2Int(x, y);
    }

    // For multiplier-like ranges around 1 (e.g., 1.10..1.30): scale only the (mult - 1) delta
    private Vector2 ScaleMultiplierLike(Vector2 baseRange, int tier)
    {
        float m = TierMult(tier);
        float a = 1f + (baseRange.x - 1f) * m;
        float b = 1f + (baseRange.y - 1f) * m;
        if (a > b) (a, b) = (b, a);
        return new Vector2(a, b);
    }

    // =========================
    // Upgrades (record applied deltas to enable clean undo)
    // =========================

    private void Upgrade_DamageFlat()
    {
        Vector2Int r = ScaleRange(damageFlatAddRange, tierDamageFlat, 0);
        int add = UnityEngine.Random.Range(r.x, r.y + 1);
        if (knife != null) knife.damage += add;
        if (shooter != null) shooter.damage += add;
        last.damageFlatAdded += add;
        notes.Add($"+{add} Damage (Tier {ToRoman(tierDamageFlat)})");
    }

    private void Upgrade_DamagePercentAsFlatDelta()
    {
        Vector2 r = ScaleMultiplierLike(damageMultRange, tierDamagePercent);
        float mult = UnityEngine.Random.Range(r.x, r.y);
        float pct = (mult - 1f) * 100f;

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

        notes.Add($"+{pct:F0}% Damage (Tier {ToRoman(tierDamagePercent)})");
    }

    private void Upgrade_FasterAttack_StoreDelta()
    {
        if (tick == null) return;

        Vector2 r = ScaleRange(atkSpeedFracRange, tierAttackSpeed);
        float frac = Mathf.Clamp01(UnityEngine.Random.Range(r.x, r.y));
        float before = tick.interval;
        float reduceBy = before * frac;
        float newInterval = Mathf.Max(0.05f, before - reduceBy);
        float actuallyReduced = before - newInterval;
        tick.interval = newInterval;

        last.tickIntervalDelta += actuallyReduced;
        notes.Add($"+{frac * 100f:F0}% Attack Speed (Tier {ToRoman(tierAttackSpeed)})");
    }

    private void Upgrade_Crit()
    {
        bool buffChance = UnityEngine.Random.value < 0.6f;
        if (buffChance)
        {
            Vector2 r = ScaleRange(critChanceAddRange, tierCritChance);
            float add = Mathf.Clamp01(UnityEngine.Random.Range(r.x, r.y));
            if (knife != null) knife.critChance = Mathf.Clamp01(knife.critChance + add);
            if (shooter != null) shooter.critChance = Mathf.Clamp01(shooter.critChance + add);
            last.critChanceAdded += add;
            notes.Add($"+{add * 100f:F0}% Crit Chance (Tier {ToRoman(tierCritChance)})");
        }
        else
        {
            Vector2 r = ScaleRange(critMultAddRange, tierCritMultiplier);
            float add = UnityEngine.Random.Range(r.x, r.y);
            if (knife != null) knife.critMultiplier += add;
            if (shooter != null) shooter.critMultiplier += add;
            last.critMultAdded += add;
            notes.Add($"+{add:F2} Crit Mult (Tier {ToRoman(tierCritMultiplier)})");
        }
    }

    private void Upgrade_KnifeLifesteal()
    {
        if (knife == null) return;
        Vector2 r = ScaleRange(knifeLifestealAddRange, tierKnifeLifesteal);
        float add = Mathf.Clamp01(UnityEngine.Random.Range(r.x, r.y));
        knife.lifestealPercent = Mathf.Clamp01(knife.lifestealPercent + add);
        last.knifeLifestealAdded += add;
        notes.Add($"+{add * 100f:F0}% Lifesteal (Tier {ToRoman(tierKnifeLifesteal)})");
    }

    private void Upgrade_KnifeMainRadius_AsDelta()
    {
        if (knife == null) return;
        Vector2 r = ScaleMultiplierLike(knifeRadiusMultRange, tierKnifeRadius);
        float mult = UnityEngine.Random.Range(r.x, r.y);
        float delta = knife.radius * (mult - 1f);
        knife.radius += delta;
        last.knifeRadiusDelta += delta;
        notes.Add($"+{(mult - 1f) * 100f:F0}% Range (Tier {ToRoman(tierKnifeRadius)})");
    }

    private void Upgrade_KnifeSplashRadius_AsDelta()
    {
        if (knife == null) return;
        Vector2 r = ScaleMultiplierLike(knifeSplashRadiusMultRange, tierKnifeSplashRadius);
        float mult = UnityEngine.Random.Range(r.x, r.y);
        float delta = knife.splashRadius * (mult - 1f);
        knife.splashRadius += delta;
        last.knifeSplashRadiusDelta += delta;
        notes.Add($"+{(mult - 1f) * 100f:F0}% AOE (Tier {ToRoman(tierKnifeSplashRadius)})");
    }

    private void Upgrade_KnifeMaxTargets()
    {
        if (knife == null) return;
        Vector2Int r = ScaleRange(knifeMaxTargetsAddRange, tierKnifeMaxTargets, 1);
        int add = Mathf.Max(1, UnityEngine.Random.Range(r.x, r.y + 1));
        knife.maxTargetsPerTick += add;
        last.knifeMaxTargetsAdded += add;
        notes.Add($"+{add} Max Targets (Tier {ToRoman(tierKnifeMaxTargets)})");
    }

    private void Upgrade_ShooterProjectiles()
    {
        if (shooter == null) return;
        Vector2Int r = ScaleRange(shooterProjectilesAddRange, tierShooterProjectiles, 1);
        int add = Mathf.Max(1, UnityEngine.Random.Range(r.x, r.y + 1));
        shooter.projectileCount += add;
        last.shooterProjectilesAdded += add;
        notes.Add($"+{add} Projectiles (Tier {ToRoman(tierShooterProjectiles)})");
    }

    private void Upgrade_ShooterRange()
    {
        if (shooter == null) return;

        bool buffLifetime = UnityEngine.Random.value < 0.5f;
        if (buffLifetime)
        {
            Vector2 r = ScaleRange(shooterLifetimeAddRange, tierShooterLifetime);
            float add = Mathf.Max(0f, UnityEngine.Random.Range(r.x, r.y));
            shooter.bulletLifetime += add;
            last.shooterLifetimeAdded += add;
            notes.Add($"+{add:F1}s Projectile Lifetime (Tier {ToRoman(tierShooterLifetime)})");
        }
        else
        {
            Vector2 r = ScaleRange(shooterForceAddRange, tierShooterForce);
            float add = Mathf.Max(0f, UnityEngine.Random.Range(r.x, r.y));
            shooter.shootForce += add;
            last.shooterForceAdded += add;
            notes.Add($"+{add:F1} Projectile Speed (Tier {ToRoman(tierShooterForce)})");
        }
    }

    private void Upgrade_ShooterAccuracy_AsDelta()
    {
        if (shooter == null) return;

        Vector2 r = ScaleRange(shooterSpreadReduceFracRange, tierShooterAccuracy);
        float frac = Mathf.Clamp01(UnityEngine.Random.Range(r.x, r.y));
        float before = shooter.spreadAngle;
        float delta = before * frac;
        float newSpread = Mathf.Max(0f, before - delta);
        float actuallyReduced = before - newSpread;
        shooter.spreadAngle = newSpread;

        last.shooterSpreadDelta += actuallyReduced;
        notes.Add($"+{frac * 100f:F0}% Accuracy (Tier {ToRoman(tierShooterAccuracy)})");
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

        // Find start of rarity block by looking for our blue wrapper
        int idx = s.IndexOf("<color=#00AEEF>", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            idx = s.IndexOf("<b>Rarity:</b>", StringComparison.OrdinalIgnoreCase); // fallback

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

    // 1..10 -> Roman numerals
    private static string ToRoman(int n)
    {
        n = Mathf.Clamp(n, 1, 10);
        return n switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            6 => "VI",
            7 => "VII",
            8 => "VIII",
            9 => "IX",
            _ => "X"
        };
    }
    private void RollTiers()
    {
        // Rolls 1–10 for each tier variable
        tierDamagePercent = UnityEngine.Random.Range(1, 11);
        tierDamageFlat = UnityEngine.Random.Range(1, 11);
        tierAttackSpeed = UnityEngine.Random.Range(1, 11);
        tierCritChance = UnityEngine.Random.Range(1, 11);
        tierCritMultiplier = UnityEngine.Random.Range(1, 11);

        tierKnifeRadius = UnityEngine.Random.Range(1, 11);
        tierKnifeSplashRadius = UnityEngine.Random.Range(1, 11);
        tierKnifeLifesteal = UnityEngine.Random.Range(1, 11);
        tierKnifeMaxTargets = UnityEngine.Random.Range(1, 11);

        tierShooterLifetime = UnityEngine.Random.Range(1, 11);
        tierShooterForce = UnityEngine.Random.Range(1, 11);
        tierShooterProjectiles = UnityEngine.Random.Range(1, 11);
        tierShooterAccuracy = UnityEngine.Random.Range(1, 11);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
