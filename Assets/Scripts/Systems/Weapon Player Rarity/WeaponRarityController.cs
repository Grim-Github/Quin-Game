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

    private System.Random rng;

    // ===== Selected Upgrades Tracking =====
    private struct AppliedUpgrade
    {
        public IUpgrade upgrade;   // which upgrade (type/behavior)
        public Action undo;        // how to undo this single upgrade
        public string note;        // human-readable line(s) for UI

        public AppliedUpgrade(IUpgrade u, Action un, string n)
        {
            upgrade = u; undo = un; note = n;
        }
    }
    private readonly List<AppliedUpgrade> applied = new();

    /// <summary>Number of currently selected/applied stat upgrades.</summary>
    public int SelectedUpgradeCount => applied.Count;

    /// <summary>Return a snapshot of current applied upgrade notes (for UI/Debug).</summary>
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
        // Build adapters from present components (no reflection)
        var k = GetComponent<Knife>();
        var s = GetComponent<SimpleShooter>();
        var t = GetComponent<WeaponTick>();

        if (k) { knife = new KnifeAdapter(k); uiSink = knife; }
        if (s) { shooter = new ShooterAdapter(s); if (uiSink == null) uiSink = shooter; }
        if (t) { tick = new TickAdapter(t); }

        rng = rngSeed == 0 ? new System.Random() : new System.Random(rngSeed);

        if (rollOnAwake)
        {
            RerollRarityAndStats();
        }
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
        // Fresh tiers like original flow
        tiers.RollAll(rng);

        // Undo all previously applied upgrades
        UndoAllApplied();

        // Build context and pool
        var ctx = BuildContext();
        var pool = BuildUpgrades(ctx);
        if (pool.Count == 0)
        {
            applied.Clear();
            WriteRarityBlockOnly("<i>No applicable upgrades.</i>");
            return;
        }

        // Rolls per rarity
        int rolls = current switch { Rarity.Common => 1, Rarity.Uncommon => 2, Rarity.Rare => 4, Rarity.Legendary => 5, _ => 1 };

        // Shuffle and take first N
        Shuffle(pool);

        // Apply
        applied.Clear();
        for (int i = 0; i < Mathf.Min(rolls, pool.Count); i++)
            ApplyAndRecord(ctx, pool[i]);

        // Rebuild UI block from applied list
        RebuildUIFromApplied();

        // retrigger tick if present
        tick?.ResetAndStartIfPlaying();
    }

    /// <summary>Rerolls a single stat at <paramref name="index"/> within the currently selected upgrades (same upgrade type).</summary>
    /// <returns>true on success, false if index invalid or nothing to reroll.</returns>
    public bool RerollStatAt(int index)
    {
        if (index < 0 || index >= applied.Count) return false;

        // Fresh tiers for this single roll
        tiers.RollAll(rng);

        var ctx = BuildContext();

        // Undo the previous single upgrade
        applied[index].undo?.Invoke();

        // Re-apply the SAME upgrade type (keeps the "slot" but rerolls values)
        var up = applied[index].upgrade;
        var sb = new StringBuilder();
        var undo = up.Apply(ctx, sb);
        var note = sb.ToString().Trim();
        applied[index] = new AppliedUpgrade(up, undo, note);

        // Update UI
        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }

    /// <summary>Rerolls ONE random stat among the selected upgrades (same upgrade type).</summary>
    public bool RerollRandomStat()
    {
        if (applied.Count == 0) return false;
        int idx = UnityEngine.Random.Range(0, applied.Count);
        return RerollStatAt(idx);
    }

    [ContextMenu("Rarity/Reroll 1 Random Stat")]
    private void ContextRerollOneRandomStat()
    {
        if (!RerollRandomStat())
            Debug.LogWarning($"{name}: No stat to reroll (none applied).");
    }

    // ========== NEW #1: Reroll one stat INTO ANOTHER (switch upgrade type) ==========

    /// <summary>
    /// Rerolls the stat at <paramref name="index"/> by switching it to a DIFFERENT upgrade type.
    /// </summary>
    /// <returns>true on success, false if no alternative upgrade was available.</returns>
    public bool RerollStatIntoAnotherAt(int index)
    {
        if (index < 0 || index >= applied.Count) return false;

        // Fresh tiers for this single roll (so the new type uses current tiers)
        tiers.RollAll(rng);

        var ctx = BuildContext();

        // Build pool and exclude the current upgrade type
        var pool = BuildUpgrades(ctx);
        if (pool.Count == 0) return false;

        var currentType = applied[index].upgrade.GetType();
        var alternatives = new List<IUpgrade>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null && pool[i].GetType() != currentType && pool[i].IsApplicable(ctx))
                alternatives.Add(pool[i]);

        if (alternatives.Count == 0) return false;

        // Undo the previous single upgrade
        applied[index].undo?.Invoke();

        // Pick a new type and apply
        Shuffle(alternatives);
        var newUp = alternatives[0];
        var sb = new StringBuilder();
        var undo = newUp.Apply(ctx, sb);
        var note = sb.ToString().Trim();
        applied[index] = new AppliedUpgrade(newUp, undo, note);

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }

    /// <summary>Convenience: pick a random applied slot and switch it to another upgrade type.</summary>
    public bool RerollRandomStatIntoAnother()
    {
        if (applied.Count == 0) return false;
        int idx = UnityEngine.Random.Range(0, applied.Count);
        return RerollStatIntoAnotherAt(idx);
    }

    [ContextMenu("Rarity/Reroll 1 Stat Into Another Type")]
    private void ContextRerollIntoAnother()
    {
        if (!RerollRandomStatIntoAnother())
            Debug.LogWarning($"{name}: No alternative upgrade type available.");
    }

    // ========== NEW #2: Randomly upgrade a tier (towards Tier=1) ==========

    /// <summary>
    /// Randomly improves ONE tier field by <paramref name="steps"/> (toward 1 = strongest).
    /// Optionally rerolls ONE applied stat afterward to reflect the new tier immediately.
    /// </summary>
    /// <returns>true if a tier was changed.</returns>
    public bool UpgradeRandomTier(int steps = 1, bool rerollOneAppliedStat = true)
    {
        steps = Mathf.Max(1, steps);

        // We have 13 tier slots. Choose one at random and improve it.
        int slot = UnityEngine.Random.Range(0, 13);
        bool changed = ImproveTierSlot(slot, steps);

        if (!changed) return false;

        // Optionally show effect by rerolling one applied stat
        if (rerollOneAppliedStat && applied.Count > 0)
            RerollRandomStat();

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
        // Move chosen tier toward 1 by 'steps'
        switch (slotIndex)
        {
            case 0: return Lower(ref tiers.damagePercent, steps);
            case 1: return Lower(ref tiers.damageFlat, steps);
            case 2: return Lower(ref tiers.attackSpeed, steps);
            case 3: return Lower(ref tiers.critChance, steps);
            case 4: return Lower(ref tiers.critMultiplier, steps);
            case 5: return Lower(ref tiers.knifeRadius, steps);
            case 6: return Lower(ref tiers.knifeSplashRadius, steps);
            case 7: return Lower(ref tiers.knifeLifesteal, steps);
            case 8: return Lower(ref tiers.knifeMaxTargets, steps);
            case 9: return Lower(ref tiers.shooterLifetime, steps);
            case 10: return Lower(ref tiers.shooterForce, steps);
            case 11: return Lower(ref tiers.shooterProjectiles, steps);
            case 12: return Lower(ref tiers.shooterAccuracy, steps);
            default: return false;
        }
    }

    private static bool Lower(ref int tierField, int steps)
    {
        int before = tierField;
        tierField = Mathf.Clamp(tierField - steps, 1, 10);
        return tierField != before;
    }

    private void ApplyAndRecord(WeaponContext ctx, IUpgrade up)
    {
        var sb = new StringBuilder();
        var undo = up.Apply(ctx, sb);
        var note = sb.ToString().Trim();
        applied.Add(new AppliedUpgrade(up, undo, note));
    }

    private void UndoAllApplied()
    {
        for (int i = applied.Count - 1; i >= 0; i--)
            applied[i].undo?.Invoke();
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
            tickAdapter = tick
        };
    }

    private List<IUpgrade> BuildUpgrades(WeaponContext c)
    {
        var list = new List<IUpgrade>();

        if (c.damage != null) { list.Add(new DamageFlatUpgrade()); list.Add(new DamagePercentAsFlatUpgrade()); }
        if (c.attack != null) list.Add(new AttackSpeedUpgrade());
        if (c.crit != null) list.Add(new CritUpgrade());
        if (c.knife != null)
        {
            list.Add(new KnifeLifestealUpgrade());
            list.Add(new KnifeSplashUpgrade());
            list.Add(new KnifeRadiusUpgrade());
            list.Add(new KnifeMaxTargetsUpgrade());
        }
        if (c.shooter != null)
        {
            list.Add(new ShooterProjectilesUpgrade());
            list.Add(new ShooterRangeUpgrade());
            list.Add(new ShooterAccuracyUpgrade());
        }

        return list;
    }

    // ===================== UI Build & Sanitize (NO <size>) =====================

    private void RebuildUIFromApplied()
    {
        if (uiSink == null) return;

        var sb = new StringBuilder();
        sb.AppendLine($"<b>Rarity:</b> {WeaponContext.FormatRarity(current)}");
        for (int i = 0; i < applied.Count; i++)
        {
            var line = applied[i].note;
            if (!string.IsNullOrEmpty(line)) sb.AppendLine(line);
        }

        WriteRaritySection(sb.ToString().TrimEnd());
    }

    private void WriteRarityBlockOnly(string inner)
    {
        if (uiSink == null) return;

        var sb = new StringBuilder();
        sb.AppendLine($"<b>Rarity:</b> {WeaponContext.FormatRarity(current)}");
        sb.AppendLine(inner);
        WriteRaritySection(sb.ToString().TrimEnd());
    }

    /// <summary>
    /// Centralized writer: strips any old wrappers (<size> & <color>), wraps once with color only,
    /// removes the last rarity block from current UI, dedupes/normalizes, then sets text.
    /// </summary>
    private void WriteRaritySection(string inner)
    {
        if (uiSink == null) return;

        // 1) Ensure the INNER content has no wrapper tags
        string cleanInner = StripWrappers(inner);

        // 2) Wrap exactly once with color (NO size)
        string block = TAG_COLOR_OPEN + cleanInner + TAG_COLOR_CLOSE;

        // 3) Remove the last rarity section (wrappers included) from current text
        string cur = uiSink.Text ?? string.Empty;
        string withoutLast = RemoveLastRaritySection(cur);

        // 4) Merge, then dedupe & normalize
        string merged = string.IsNullOrWhiteSpace(withoutLast) ? block : (withoutLast.TrimEnd() + "\n" + block);
        merged = DedupeColorTags(merged);
        merged = NormalizeWhitespace(merged);

        uiSink.SetText(merged);
    }

    /// <summary>Remove any legacy <size> and our color tags from a string.</summary>
    private static string StripWrappers(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        // Remove any size tags that may still exist
        s = s.Replace("<size=80%>", string.Empty)
             .Replace("</size>", string.Empty);

        // Remove our color tags
        s = s.Replace(TAG_COLOR_OPEN, string.Empty)
             .Replace(TAG_COLOR_CLOSE, string.Empty);

        return s.Trim();
    }

    /// <summary>
    /// Remove the last rarity block by finding the last "<b>Rarity:</b>", or falling back to the last color open.
    /// </summary>
    private static string RemoveLastRaritySection(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        int rarityIdx = s.LastIndexOf("<b>Rarity:</b>", StringComparison.OrdinalIgnoreCase);
        if (rarityIdx < 0)
        {
            // Fallback: try last color opener
            int colorIdx = s.LastIndexOf(TAG_COLOR_OPEN, StringComparison.OrdinalIgnoreCase);
            return (colorIdx >= 0) ? s.Substring(0, colorIdx).TrimEnd() : s;
        }

        // Try to find an outer color start that begins before the rarity header
        int colorStart = s.LastIndexOf(TAG_COLOR_OPEN, rarityIdx, StringComparison.OrdinalIgnoreCase);
        int startIdx = (colorStart >= 0) ? colorStart : rarityIdx;

        return s.Substring(0, startIdx).TrimEnd();
    }

    /// <summary>Collapse accidental duplicate color opens/closes created by merges.</summary>
    private static string DedupeColorTags(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        // Collapse repeated opens
        while (s.Contains(TAG_COLOR_OPEN + TAG_COLOR_OPEN))
            s = s.Replace(TAG_COLOR_OPEN + TAG_COLOR_OPEN, TAG_COLOR_OPEN);

        // Collapse repeated closes
        while (s.Contains(TAG_COLOR_CLOSE + TAG_COLOR_CLOSE))
            s = s.Replace(TAG_COLOR_CLOSE + TAG_COLOR_CLOSE, TAG_COLOR_CLOSE);

        return s;
    }

    // Collapses runs of blank lines and trims line-start spaces
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

            // End of newline run
            nlRun = 0;

            // Drop leading spaces/tabs at line starts
            if (c == ' ' || c == '\t')
            {
                int len = sb.Length;
                if (len > 0 && sb[len - 1] == '\n') continue;
            }

            sb.Append(c);
        }

        return sb.ToString().TrimEnd();
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
