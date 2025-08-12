using System;
using System.Collections.Generic;
using System.Reflection;
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

    [Header("Knife Tweaks")]
    [Tooltip("Allow reflection to modify Knife private fields (radius, lifesteal, etc.).")]
    [SerializeField] private bool allowKnifeReflection = true;

    [Header("Roll Ranges (per picked upgrade)")]
    [Tooltip("Percent damage per roll, applied multiplicatively: 1.10 = +10%.")]
    [SerializeField] private Vector2 damageMultRange = new Vector2(1.10f, 1.30f);   // +10..30%
    [Tooltip("NEW: Flat damage added per roll.")]
    [SerializeField] private Vector2Int damageFlatAddRange = new Vector2Int(3, 12); // +3..12 flat

    [Tooltip("Attack speed = reduce WeaponTick.interval by this fraction per roll.")]
    [SerializeField] private Vector2 atkSpeedFracRange = new Vector2(0.10f, 0.25f); // -10..25% interval
    [SerializeField] private Vector2 critChanceAddRange = new Vector2(0.05f, 0.20f);// +5..20%
    [SerializeField] private Vector2 critMultAddRange = new Vector2(0.25f, 1.00f);  // +0.25..+1.0

    // Knife-only via reflection
    [SerializeField] private Vector2 knifeRadiusMultRange = new Vector2(1.10f, 1.30f);
    [SerializeField] private Vector2 knifeSplashRadiusMultRange = new Vector2(1.10f, 1.30f);
    [SerializeField] private Vector2 knifeLifestealAddRange = new Vector2(0.05f, 0.20f);
    [SerializeField] private Vector2Int knifeMaxTargetsAddRange = new Vector2Int(1, 3);

    // Shooter-only
    [SerializeField] private Vector2 shooterLifetimeAddRange = new Vector2(0.5f, 2.0f);
    [SerializeField] private Vector2 shooterForceAddRange = new Vector2(1.0f, 4.0f);
    [SerializeField] private Vector2Int shooterProjectilesAddRange = new Vector2Int(1, 2);
    [SerializeField] private Vector2 shooterSpreadReduceFracRange = new Vector2(0.10f, 0.35f);

    // cached
    private Knife knife;           // public: damage, critChance, critMultiplier; privates via reflection
    private SimpleShooter shooter; // public: damage, shootForce, bulletLifetime, projectileCount, spreadAngle, crit...
    private WeaponTick tick;       // public: interval; ResetAndStart()

    private readonly List<string> notes = new();

    private void Awake()
    {
        knife = GetComponent<Knife>();
        shooter = GetComponent<SimpleShooter>();
        tick = GetComponent<WeaponTick>();

        if (rollOnAwake)
        {
            RerollRarity(); // rolls rarity (weighted) and applies stats
        }
    }

    // ---------- Public API ----------
    public void RerollRarity()
    {
        rarity = RollWeightedRarity();
        RerollStats();
    }

    public void RerollStats()
    {
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

        // Compact UI: show only "+ {buff}" lines (plus a small rarity tag on top)
        var sb = new StringBuilder();
        sb.AppendLine($"<b>Rarity:</b> {FormatRarity(rarity)}");
        for (int i = 0; i < notes.Count; i++)
            sb.AppendLine(notes[i]);

        WriteExtra(BlueWrap($"<size=80%>{sb}</size>"));

        if (tick != null && Application.isPlaying) tick.ResetAndStart();
        if (knife != null && allowKnifeReflection) CallPrivateMethod(knife, "UpdateRangeVisual");
    }

    [ContextMenu("Rarity/Reroll Rarity + Stats")]
    private void Ctx_RerollAll() => RerollRarity();

    [ContextMenu("Rarity/Reroll Stats Only")]
    private void Ctx_RerollStats() => RerollStats();

    // ---------- Internals ----------
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

        // NEW: flat damage
        if (knife != null || shooter != null) list.Add(Upgrade_DamageFlat);

        // Percent damage
        if (knife != null || shooter != null) list.Add(Upgrade_DamagePercent);

        // Faster attack (WeaponTick.interval ↓)
        if (tick != null) list.Add(Upgrade_FasterAttack);

        // Crit
        if (knife != null || shooter != null) list.Add(Upgrade_Crit);

        // Lifesteal (Knife)
        if (knife != null && allowKnifeReflection) list.Add(Upgrade_KnifeLifesteal);

        // AOE
        if (knife != null && allowKnifeReflection) list.Add(Upgrade_KnifeSplashRadius);
        if (shooter != null) list.Add(Upgrade_ShooterProjectiles);

        // Range
        if (knife != null && allowKnifeReflection) list.Add(Upgrade_KnifeMainRadius);
        if (shooter != null) list.Add(Upgrade_ShooterRange);

        // Other stats
        if (knife != null && allowKnifeReflection) list.Add(Upgrade_KnifeMaxTargets);
        if (shooter != null) list.Add(Upgrade_ShooterAccuracy);

        return list;
    }

    // ---------- Individual upgrades (produce "+ ..." notes) ----------
    private void Upgrade_DamageFlat()
    {
        int add = UnityEngine.Random.Range(damageFlatAddRange.x, damageFlatAddRange.y + 1);

        if (knife != null)
        {
            knife.damage = Mathf.Max(0, knife.damage + add);
        }
        if (shooter != null)
        {
            shooter.damage = Mathf.Max(0, shooter.damage + add);
        }
        notes.Add($"+{add} Damage");
    }

    private void Upgrade_DamagePercent()
    {
        float mult = UnityEngine.Random.Range(damageMultRange.x, damageMultRange.y);
        float pct = (mult - 1f) * 100f;

        if (knife != null)
            knife.damage = Mathf.RoundToInt(knife.damage * mult);
        if (shooter != null)
            shooter.damage = Mathf.RoundToInt(shooter.damage * mult);

        notes.Add($"+{pct:F0}% Damage");
    }

    private void Upgrade_FasterAttack()
    {
        if (tick == null) return;
        float frac = UnityEngine.Random.Range(atkSpeedFracRange.x, atkSpeedFracRange.y);
        tick.interval = Mathf.Max(0.05f, tick.interval * (1f - frac));
        notes.Add($"+{frac * 100f:F0}% Attack Speed");
    }

    private void Upgrade_Crit()
    {
        bool buffChance = UnityEngine.Random.value < 0.6f;

        if (knife != null || shooter != null)
        {
            if (buffChance)
            {
                float add = UnityEngine.Random.Range(critChanceAddRange.x, critChanceAddRange.y);
                if (knife != null) knife.critChance = Mathf.Clamp01(knife.critChance + add);
                if (shooter != null) shooter.critChance = Mathf.Clamp01(shooter.critChance + add);
                notes.Add($"+{add * 100f:F0}% Crit Chance");
            }
            else
            {
                float add = UnityEngine.Random.Range(critMultAddRange.x, critMultAddRange.y);
                if (knife != null) knife.critMultiplier = Mathf.Max(1f, knife.critMultiplier + add);
                if (shooter != null) shooter.critMultiplier = Mathf.Max(1f, shooter.critMultiplier + add);
                notes.Add($"+{add:F2} Crit Mult");
            }
        }
    }

    private void Upgrade_KnifeLifesteal()
    {
        if (knife == null) return;
        if (!TryGetPrivate(knife, "lifestealPercent", out float before)) return;
        float add = UnityEngine.Random.Range(knifeLifestealAddRange.x, knifeLifestealAddRange.y);
        float after = Mathf.Clamp01(before + add);
        if (TrySetPrivate(knife, "lifestealPercent", after))
            notes.Add($"+{add * 100f:F0}% Lifesteal");
    }

    private void Upgrade_KnifeMainRadius()
    {
        if (knife == null) return;
        if (!TryGetPrivate(knife, "radius", out float r0)) return;
        float mult = UnityEngine.Random.Range(knifeRadiusMultRange.x, knifeRadiusMultRange.y);
        float r1 = Mathf.Max(0f, r0 * mult);
        if (TrySetPrivate(knife, "radius", r1))
            notes.Add($"+{(mult - 1f) * 100f:F0}% Range");
    }

    private void Upgrade_KnifeSplashRadius()
    {
        if (knife == null) return;
        if (!TryGetPrivate(knife, "splashRadius", out float r0)) return;
        float mult = UnityEngine.Random.Range(knifeSplashRadiusMultRange.x, knifeSplashRadiusMultRange.y);
        float r1 = Mathf.Max(0f, r0 * mult);
        if (TrySetPrivate(knife, "splashRadius", r1))
            notes.Add($"+{(mult - 1f) * 100f:F0}% AOE");
    }

    private void Upgrade_KnifeMaxTargets()
    {
        if (knife == null) return;
        if (!TryGetPrivate(knife, "maxTargetsPerTick", out int before)) return;
        int add = UnityEngine.Random.Range(knifeMaxTargetsAddRange.x, knifeMaxTargetsAddRange.y + 1);
        int after = Mathf.Max(0, before + add);
        if (TrySetPrivate(knife, "maxTargetsPerTick", after))
            notes.Add($"+{add} Max Targets");
    }

    private void Upgrade_ShooterProjectiles()
    {
        if (shooter == null) return;
        int add = UnityEngine.Random.Range(shooterProjectilesAddRange.x, shooterProjectilesAddRange.y + 1);
        shooter.projectileCount = Mathf.Max(1, shooter.projectileCount + add);
        notes.Add($"+{add} Projectiles");
    }

    private void Upgrade_ShooterRange()
    {
        if (shooter == null) return;
        bool buffLifetime = UnityEngine.Random.value < 0.5f;
        if (buffLifetime)
        {
            float add = UnityEngine.Random.Range(shooterLifetimeAddRange.x, shooterLifetimeAddRange.y);
            shooter.bulletLifetime = Mathf.Max(0f, shooter.bulletLifetime + add);
            notes.Add($"+{add:F1}s Projectile Lifetime");
        }
        else
        {
            float add = UnityEngine.Random.Range(shooterForceAddRange.x, shooterForceAddRange.y);
            shooter.shootForce = Mathf.Max(0f, shooter.shootForce + add);
            notes.Add($"+{add:F1} Projectile Speed");
        }
    }

    private void Upgrade_ShooterAccuracy()
    {
        if (shooter == null) return;
        float frac = UnityEngine.Random.Range(shooterSpreadReduceFracRange.x, shooterSpreadReduceFracRange.y);
        shooter.spreadAngle = Mathf.Max(0f, shooter.spreadAngle * (1f - frac));
        notes.Add($"+{frac * 100f:F0}% Accuracy");
    }

    // ---------- Write concise blue text into each weapon's existing extraTextField ----------
    private void WriteExtra(string block)
    {
        if (knife != null) AppendIntoExtra(knife, block);
        if (shooter != null) AppendIntoExtra(shooter, block);
    }

    private void AppendIntoExtra(object component, string block)
    {
        if (!TryGetPrivate(component, "extraTextField", out string cur)) cur = "";
        // Remove any previous rarity section to avoid stacking duplicates
        string cleaned = RemoveRaritySection(cur);
        string combined = string.IsNullOrWhiteSpace(cleaned) ? block : $"{cleaned}\n{block}";
        TrySetPrivate(component, "extraTextField", combined);
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

    // ---------- Helpers ----------
    private static bool TryGetPrivate<T>(object obj, string field, out T value)
    {
        value = default;
        if (obj == null) return false;
        var fi = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi == null) return false;
        object v = fi.GetValue(obj);
        if (v is T cast) { value = cast; return true; }
        return false;
    }

    private static bool TrySetPrivate<T>(object obj, string field, T newValue)
    {
        if (obj == null) return false;
        var fi = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi == null || !fi.FieldType.IsAssignableFrom(typeof(T))) return false;
        fi.SetValue(obj, newValue);
        return true;
    }

    private static void CallPrivateMethod(object obj, string method)
    {
        if (obj == null) return;
        var mi = obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        mi?.Invoke(obj, null);
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
