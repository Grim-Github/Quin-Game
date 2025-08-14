using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponRarityController : MonoBehaviour
{
    [Header("Lifecycle")]
    [SerializeField] private bool rollOnAwake = true;
    [SerializeField] private int rngSeed = 0; // 0 = random

    [SerializeField] private UpgradeWeightProvider upgradeWeights;  // optional; if null, fallback to old behavior

    [Header("Rarity")]
    [SerializeField] private Rarity current = Rarity.Common;
    [SerializeField] private RarityWeights weights = new RarityWeights { common = 60, uncommon = 25, rare = 12, legendary = 3 };

    [Header("Ranges")]
    [SerializeField] private UpgradeRanges ranges = new UpgradeRanges();

    [Header("Tiers")]
    [SerializeField] private TierSystem tiers = new TierSystem();

    // Cached adapters
    private KnifeAdapter knife;
    private ShooterAdapter shooter;
    private TickAdapter tick;
    private IUITextSink uiSink; // whichever (knife or shooter) we found

    // Single RNG source (deterministic with seed)
    private System.Random rng;

    // ===== Selected Upgrades Tracking =====
    private sealed class AppliedUpgrade
    {
        public readonly IUpgrade upgrade;   // upgrade behavior
        public readonly Action undo;        // undo action
        public readonly string note;        // human-readable line(s) for UI
        public AppliedUpgrade(IUpgrade u, Action un, string n) { upgrade = u; undo = un; note = n; }
    }
    private readonly List<AppliedUpgrade> applied = new();

    public int SelectedUpgradeCount => applied.Count;

    public IReadOnlyList<string> SelectedUpgradeNotes
    {
        get
        {
            var list = new List<string>(applied.Count);
            for (int i = 0; i < applied.Count; i++) list.Add(applied[i].note);
            return list;
        }
    }

    // Wrapper tag constants (NO size tags)
    private const string TAG_COLOR_OPEN = "<color=#00AEEF>";
    private const string TAG_COLOR_CLOSE = "</color>";

    private void Awake()
    {
        // Build adapters (no reflection)
        var k = GetComponent<Knife>();
        var s = GetComponent<SimpleShooter>();
        var t = GetComponent<WeaponTick>();

        if (k) { knife = new KnifeAdapter(k); uiSink = knife; }
        if (s) { shooter = new ShooterAdapter(s); if (uiSink == null) uiSink = shooter; }
        if (t) { tick = new TickAdapter(t); }

        rng = rngSeed == 0 ? new System.Random() : new System.Random(rngSeed);

        if (rollOnAwake) RerollRarityAndStats();
    }

    // ===================== Public API =====================

    [ContextMenu("Rarity/Reroll Rarity + All Stats")]
    public void RerollRarityAndStats()
    {
        current = weights.Roll(rng);
        RerollStats();
    }

    [ContextMenu("Rarity/Reroll All Stats (keep current rarity)")]
    public void RerollStats()
    {
        tiers.RollAll(rng);
        UndoAllApplied();

        var ctx = BuildContext();

        // NEW: build typed candidates instead of bare IUpgrade list
        var candidates = BuildCandidates(ctx);
        if (candidates.Count == 0)
        {
            applied.Clear();
            WriteUIBlock(new[] { $"<b>Rarity:</b> {WeaponContext.FormatRarity(current)}", "<i>No applicable upgrades.</i>" });
            return;
        }

        // Instead of fixed max rolls per rarity, roll between 1 and that rarity's max
        int maxRolls = current switch
        {
            Rarity.Common => 1,
            Rarity.Uncommon => 2,
            Rarity.Rare => 4,
            Rarity.Legendary => 5,
            _ => 1
        };

        // Randomly choose how many upgrades to apply (at least 1)
        int rolls = rng.Next(1, maxRolls + 1);


        applied.Clear();

        if (upgradeWeights != null)
        {
            // Weighted selection
            var picked = upgradeWeights.PickWeighted(candidates, rolls, rng);
            for (int i = 0; i < picked.Count; i++)
                ApplyAndRecord(ctx, picked[i]);
        }
        else
        {
            // Fallback: old behavior (shuffle + take N)
            Shuffle(candidates, rng);
            for (int i = 0; i < Math.Min(rolls, candidates.Count); i++)
                ApplyAndRecord(ctx, candidates[i].upgrade);
        }

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
    }


    /// <summary>Rerolls a single stat at <paramref name="index"/>. 
    /// If <paramref name="rerollTiers"/> is true, tiers are re-rolled first.</summary>
    public bool RerollStatAt(int index, bool rerollTiers = true)
    {
        if (!IsValidIndex(index)) return false;

        if (rerollTiers)
            tiers.RollAll(rng);

        var ctx = BuildContext();

        var prev = applied[index];
        prev.undo?.Invoke();

        var sb = new StringBuilder();
        var undo = prev.upgrade.Apply(ctx, sb);
        applied[index] = new AppliedUpgrade(prev.upgrade, undo, sb.ToString().Trim());

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }


    /// <summary>Rerolls ONE random stat without changing tiers (keeps current tiers).</summary>
    public bool RerollRandomStat()
    {
        if (applied.Count == 0) return false;
        int idx = NextInt(rng, 0, applied.Count);
        return RerollStatAt(idx, false); // <-- no tier reroll for index 2 button
    }


    /// <summary>
    /// Removes one random applied upgrade (undoing its effect).
    /// Allows removal down to 1 remaining upgrade, regardless of rarity.
    /// </summary>
    public bool RemoveRandomUpgrade()
    {
        // Allow going down to exactly 1 upgrade
        if (applied.Count <= 0) return false;

        int idx = NextInt(rng, 0, applied.Count);
        applied[idx].undo?.Invoke();
        applied.RemoveAt(idx);

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }


    // === Add exactly one UNIQUE upgrade; never duplicate; respect rarity cap ===
    public bool AddRandomUpgrade(bool preferUnique = true)
    {
        int maxAllowed = RollsFor(current);
        if (applied.Count >= maxAllowed) return false; // already full

        var ctx = BuildContext();
        var cands = BuildCandidates(ctx);
        if (cands == null || cands.Count == 0) return false;

        // Types already taken
        var existing = new HashSet<System.Type>();
        for (int i = 0; i < applied.Count; i++)
            if (applied[i].upgrade != null)
                existing.Add(applied[i].upgrade.GetType());

        // Only consider types not already present
        var uniqueBag = new List<UpgradeWeightProvider.Candidate>(cands.Count);
        for (int i = 0; i < cands.Count; i++)
        {
            var up = cands[i].upgrade;
            if (up == null) continue;
            if (!up.IsApplicable(ctx)) continue;
            if (existing.Contains(up.GetType())) continue; // enforce uniqueness
            uniqueBag.Add(cands[i]);
        }

        if (uniqueBag.Count == 0) return false; // nothing unique left to add

        // Pick one
        IUpgrade picked = null;
        if (upgradeWeights != null)
        {
            var list = upgradeWeights.PickWeighted(uniqueBag, 1, rng);
            if (list == null || list.Count == 0 || list[0] == null) return false;
            picked = list[0];
        }
        else
        {
            Shuffle(uniqueBag, rng);
            picked = uniqueBag[0].upgrade;
        }

        // Apply & record
        var sb = new StringBuilder();
        var undo = picked.Apply(ctx, sb);
        applied.Add(new AppliedUpgrade(picked, undo, sb.ToString().Trim()));

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }





    [ContextMenu("Rarity/Reroll 1 Random Stat")]
    private void ContextRerollOneRandomStat()
    {
        if (!RerollRandomStat())
            Debug.LogWarning($"{name}: No stat to reroll (none applied).");
    }

    // ========== NEW #1: Reroll one stat INTO ANOTHER (switch upgrade type) ==========

    // === Reroll one slot into another UNIQUE upgrade type (no collisions) ===
    public bool RerollStatIntoAnotherAt(int index)
    {
        if (!IsValidIndex(index)) return false;

        tiers.RollAll(rng);
        var ctx = BuildContext();

        var cands = BuildCandidates(ctx);
        if (cands == null || cands.Count == 0) return false;

        var currentType = applied[index].upgrade?.GetType();
        if (currentType == null) return false;

        // Types present in other slots (must avoid)
        var occupied = new HashSet<System.Type>();
        for (int i = 0; i < applied.Count; i++)
        {
            if (i == index) continue;
            var up = applied[i].upgrade;
            if (up != null) occupied.Add(up.GetType());
        }

        // Build alternatives: different type, not occupied, applicable
        var altBag = new List<UpgradeWeightProvider.Candidate>();
        for (int i = 0; i < cands.Count; i++)
        {
            var up = cands[i].upgrade;
            if (up == null) continue;
            var t = up.GetType();
            if (t == currentType) continue;
            if (occupied.Contains(t)) continue;          // enforce uniqueness
            if (!up.IsApplicable(ctx)) continue;
            altBag.Add(cands[i]);
        }

        if (altBag.Count == 0) return false;

        // Choose replacement
        IUpgrade replacement = null;
        if (upgradeWeights != null)
        {
            var list = upgradeWeights.PickWeighted(altBag, 1, rng);
            if (list == null || list.Count == 0 || list[0] == null) return false;
            replacement = list[0];
        }
        else
        {
            Shuffle(altBag, rng);
            replacement = altBag[0].upgrade;
        }

        // Swap
        applied[index].undo?.Invoke();

        var sb = new StringBuilder();
        var undo = replacement.Apply(ctx, sb);
        applied[index] = new AppliedUpgrade(replacement, undo, sb.ToString().Trim());

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }


    /// <summary>Pick a random applied slot and switch it to another upgrade type.</summary>
    public bool RerollRandomStatIntoAnother()
    {
        if (applied.Count == 0) return false;
        int idx = NextInt(rng, 0, applied.Count);
        return RerollStatIntoAnotherAt(idx);
    }

    [ContextMenu("Rarity/Reroll 1 Stat Into Another Type")]
    private void ContextRerollIntoAnother()
    {
        if (!RerollRandomStatIntoAnother())
            Debug.LogWarning($"{name}: No alternative upgrade type available.");
    }

    // ========== NEW #2: Randomly upgrade a tier (towards Tier=1) ==========

    /// <summary>Improves ONE random tier field by steps (toward 1 = strongest). Optionally rerolls one selected stat.</summary>
    public bool UpgradeRandomTier(int steps = 1, bool rerollOneAppliedStat = true)
    {
        steps = Mathf.Max(1, steps);

        // 13 tier slots indexed 0..12
        int slot = NextInt(rng, 0, 13);
        bool changed = ImproveTierSlot(slot, steps);

        if (!changed) return false;

        if (rerollOneAppliedStat && applied.Count > 0)
            RerollRandomStat();

        return true;
    }

    public bool RandomizeRandomTier(bool rerollOneAppliedStat = true)
    {
        // 13 tier slots indexed 0..12
        int slot = NextInt(rng, 0, 13);
        bool changed = RandomizeTierSlot(slot);

        if (!changed) return false;

        if (rerollOneAppliedStat && applied.Count > 0)
            RerollStatAt(NextInt(rng, 0, applied.Count), true); // reroll stat range using new tier

        return true;
    }

    /// <summary>
    /// Upgrades rarity by one step (max Legendary) without adding any new upgrades.
    /// Keeps all current applied upgrades exactly as they are.
    /// </summary>
    public bool UpgradeRarityKeepStats()
    {
        var prev = current;
        var next = NextRarity(prev);
        if (next == prev) return false; // already Legendary

        current = next;

        // Optionally roll tiers so future rolls (not current stats) use fresh tiers.
        // This does NOT change already-applied values.
        tiers.RollAll(rng);

        // Refresh UI & restart tick so the new rarity shows.
        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }



    [ContextMenu("Rarity/Upgrade 1 Random Tier (then reroll 1 stat)")]
    private void ContextUpgradeOneTier()
    {
        if (!UpgradeRandomTier(1, true))
            Debug.LogWarning($"{name}: Could not upgrade a tier (already at best?).");
    }

    // ===================== Internal helpers =====================


    private bool ImproveTierSlot(int slotIndex, int steps)
    {
        switch (slotIndex)
        {
            case 0: return ClampTier(ref tiers.damagePercent, steps);
            case 1: return ClampTier(ref tiers.damageFlat, steps);
            case 2: return ClampTier(ref tiers.attackSpeed, steps);
            case 3: return ClampTier(ref tiers.critChance, steps);
            case 4: return ClampTier(ref tiers.critMultiplier, steps);
            case 5: return ClampTier(ref tiers.knifeRadius, steps);
            case 6: return ClampTier(ref tiers.knifeSplashRadius, steps);
            case 7: return ClampTier(ref tiers.knifeLifesteal, steps);
            case 8: return ClampTier(ref tiers.knifeMaxTargets, steps);
            case 9: return ClampTier(ref tiers.shooterLifetime, steps);
            case 10: return ClampTier(ref tiers.shooterForce, steps);
            case 11: return ClampTier(ref tiers.shooterProjectiles, steps);
            case 12: return ClampTier(ref tiers.shooterAccuracy, steps);
            default: return false;
        }
    }

    private bool RandomizeTierSlot(int slotIndex)
    {
        int newTier = rng.Next(1, 11); // inclusive 1..10
        switch (slotIndex)
        {
            case 0: return SetTier(ref tiers.damagePercent, newTier);
            case 1: return SetTier(ref tiers.damageFlat, newTier);
            case 2: return SetTier(ref tiers.attackSpeed, newTier);
            case 3: return SetTier(ref tiers.critChance, newTier);
            case 4: return SetTier(ref tiers.critMultiplier, newTier);
            case 5: return SetTier(ref tiers.knifeRadius, newTier);
            case 6: return SetTier(ref tiers.knifeSplashRadius, newTier);
            case 7: return SetTier(ref tiers.knifeLifesteal, newTier);
            case 8: return SetTier(ref tiers.knifeMaxTargets, newTier);
            case 9: return SetTier(ref tiers.shooterLifetime, newTier);
            case 10: return SetTier(ref tiers.shooterForce, newTier);
            case 11: return SetTier(ref tiers.shooterProjectiles, newTier);
            case 12: return SetTier(ref tiers.shooterAccuracy, newTier);
            default: return false;
        }
    }

    private static bool SetTier(ref int tierField, int newValue)
    {
        int before = tierField;
        tierField = Mathf.Clamp(newValue, 1, 10);
        return tierField != before;
    }

    private static bool ClampTier(ref int tierField, int steps)
    {
        int before = tierField;
        tierField = Mathf.Clamp(tierField - steps, 1, 10);
        return tierField != before;
    }

    private void ApplyAndRecord(WeaponContext ctx, IUpgrade up)
    {
        var sb = new StringBuilder();
        var undo = up.Apply(ctx, sb);
        applied.Add(new AppliedUpgrade(up, undo, sb.ToString().Trim()));
    }

    private void UndoAllApplied()
    {
        for (int i = applied.Count - 1; i >= 0; i--) applied[i].undo?.Invoke();
        applied.Clear();
    }

    private WeaponContext BuildContext()
    {
        return new WeaponContext
        {
            rng = rng,
            rarity = current,
            tiers = tiers,
            ranges = ranges,
            damage = (IDamageModule)(object)(knife ?? (object)shooter ?? null),
            crit = (ICritModule)(object)(knife ?? (object)shooter ?? null),
            attack = (IAttackSpeedModule)tick,
            knife = knife,
            shooter = shooter,
            ui = uiSink,
            tickAdapter = tick,
            duration = (IDurationModule)(object)(knife ?? (object)shooter ?? null) // NEW
        };
    }


    private List<UpgradeWeightProvider.Candidate> BuildCandidates(WeaponContext c)
    {
        var list = new List<UpgradeWeightProvider.Candidate>(12);
        void Add(bool cond, IUpgrade up, UpgradeType t) { if (cond && up != null) list.Add(new UpgradeWeightProvider.Candidate(up, t)); }

        if (c.damage != null)
        {
            Add(true, new DamageFlatUpgrade(), UpgradeType.DamageFlat);
            Add(true, new DamagePercentAsFlatUpgrade(), UpgradeType.DamagePercentAsFlat);
        }

        Add(c.attack != null, new AttackSpeedUpgrade(), UpgradeType.AttackSpeed);
        Add(c.crit != null, new CritUpgrade(), UpgradeType.Crit);

        if (c.knife != null)
        {
            Add(true, new KnifeLifestealUpgrade(), UpgradeType.KnifeLifesteal);
            Add(true, new KnifeSplashUpgrade(), UpgradeType.KnifeSplash);
            Add(true, new KnifeRadiusUpgrade(), UpgradeType.KnifeRadius);
            Add(true, new KnifeMaxTargetsUpgrade(), UpgradeType.KnifeMaxTargets);

            var knifeRef = GetComponent<Knife>();
            if (knifeRef != null && knifeRef.applyStatusEffectOnHit)
                Add(true, new StatusEffectDurationUpgrade(), UpgradeType.StatusEffectDuration);
        }

        if (c.shooter != null)
        {
            Add(true, new ShooterProjectilesUpgrade(), UpgradeType.ShooterProjectiles);
            Add(true, new ShooterRangeUpgrade(), UpgradeType.ShooterRange);
            Add(true, new ShooterAccuracyUpgrade(), UpgradeType.ShooterAccuracy);

            var shooterRef = GetComponent<SimpleShooter>();
            if (shooterRef != null && shooterRef.applyStatusEffectOnHit)
                Add(true, new StatusEffectDurationUpgrade(), UpgradeType.StatusEffectDuration);
        }

        return list;
    }


    // ===================== UI (single writer, no size tags) =====================

    private void RebuildUIFromApplied()
    {
        if (uiSink == null) return;

        var lines = new List<string>(1 + applied.Count)
        {
            $"<b>Rarity:</b> {WeaponContext.FormatRarity(current)}"
        };
        for (int i = 0; i < applied.Count; i++)
        {
            var line = applied[i].note;
            if (!string.IsNullOrEmpty(line)) lines.Add(line);
        }

        WriteUIBlock(lines);
    }

    private void WriteUIBlock(IReadOnlyList<string> lines)
    {
        if (uiSink == null) return;

        // Build block text
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            var clean = StripWrappers(lines[i]);
            if (!string.IsNullOrWhiteSpace(clean))
            {
                sb.AppendLine(clean);
            }
        }
        string inner = sb.ToString().TrimEnd();

        // Wrap exactly once with color
        string block = TAG_COLOR_OPEN + inner + TAG_COLOR_CLOSE;

        // Remove last rarity section (if any), then append
        string cur = uiSink.Text ?? string.Empty;
        string withoutLast = RemoveLastRaritySection(cur);

        string merged = string.IsNullOrWhiteSpace(withoutLast)
                        ? block
                        : (withoutLast.TrimEnd() + "\n" + block);

        merged = DedupeColorTags(NormalizeWhitespace(merged));

        uiSink.SetText(merged);
    }

    // ===================== Text utils =====================

    private static string StripWrappers(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace("<size=80%>", string.Empty).Replace("</size>", string.Empty);
        s = s.Replace(TAG_COLOR_OPEN, string.Empty).Replace(TAG_COLOR_CLOSE, string.Empty);
        return s.Trim();
    }

    private static string RemoveLastRaritySection(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        int rarityIdx = s.LastIndexOf("<b>Rarity:</b>", StringComparison.OrdinalIgnoreCase);
        if (rarityIdx < 0)
        {
            int colorIdx = s.LastIndexOf(TAG_COLOR_OPEN, StringComparison.OrdinalIgnoreCase);
            return (colorIdx >= 0) ? s[..colorIdx].TrimEnd() : s;
        }

        int colorStart = s.LastIndexOf(TAG_COLOR_OPEN, rarityIdx, StringComparison.OrdinalIgnoreCase);
        int startIdx = (colorStart >= 0) ? colorStart : rarityIdx;
        return s[..startIdx].TrimEnd();
    }

    private static string DedupeColorTags(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // Two quick passes are plenty for our merges
        s = s.Replace(TAG_COLOR_OPEN + TAG_COLOR_OPEN, TAG_COLOR_OPEN);
        s = s.Replace(TAG_COLOR_CLOSE + TAG_COLOR_CLOSE, TAG_COLOR_CLOSE);
        return s;
    }

    private static string NormalizeWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        var sb = new StringBuilder(s.Length);
        int nlRun = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            if (c == '\r') continue; // normalize CRLF -> LF

            if (c == '\n')
            {
                nlRun++;
                if (nlRun <= 2) sb.Append('\n');
                continue;
            }

            nlRun = 0;

            // Drop leading spaces/tabs at line starts
            if ((c == ' ' || c == '\t') && sb.Length > 0 && sb[^1] == '\n') continue;

            sb.Append(c);
        }

        return sb.ToString().TrimEnd();
    }

    // ===================== RNG helpers =====================

    private static Rarity NextRarity(Rarity r) => r switch
    {
        Rarity.Common => Rarity.Uncommon,
        Rarity.Uncommon => Rarity.Rare,
        Rarity.Rare => Rarity.Legendary,
        _ => Rarity.Legendary
    };

    private static int RollsFor(Rarity r) => r switch
    {
        Rarity.Common => 1,
        Rarity.Uncommon => 2,
        Rarity.Rare => 4,
        Rarity.Legendary => 5,
        _ => 1
    };


    private static int NextInt(System.Random r, int minInclusive, int maxExclusive)
        => r.Next(minInclusive, maxExclusive);

    private static void Shuffle<T>(IList<T> list, System.Random r)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = r.Next(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private bool IsValidIndex(int index) => (uint)index < (uint)applied.Count;
}
