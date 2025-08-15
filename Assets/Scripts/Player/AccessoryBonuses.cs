using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public class AccessoryBonuses : MonoBehaviour
{
    public enum BonusType
    {
        MaxHealthFlat,   // +N to SimpleHealth.maxHealth (also heals by same amount)
        RegenPerSecond,  // +f to SimpleHealth.regenRate
        ArmorFlat        // +f to SimpleHealth.armor
    }

    [Serializable]
    public class BonusOption
    {
        public BonusType type = BonusType.MaxHealthFlat;
        [Tooltip("Min..Max roll range. Integers are rounded for MaxHealth.")]
        public Vector2 range = new Vector2(5, 25);
        [Tooltip("Enable/disable this bonus from the pool.")]
        public bool enabled = true;
    }

    [Tooltip("If left empty, will try Player tag, then first SimpleHealth in scene, then in parents.")]
    public SimpleHealth target;

    [Tooltip("Possible bonuses to roll from (without replacement).")]
    public List<BonusOption> bonusPool = new List<BonusOption>
    {
        new BonusOption{ type = BonusType.MaxHealthFlat, range = new Vector2(10,30), enabled = true },
        new BonusOption{ type = BonusType.RegenPerSecond, range = new Vector2(0.2f,1.5f), enabled = true },
        new BonusOption{ type = BonusType.ArmorFlat, range = new Vector2(2f,12f), enabled = true },
    };

    [Min(1)] public int bonusesToRoll = 2;
    [Tooltip("Use a fixed seed for deterministic rolls (0 = random).")]
    public int seed = 0;

    [Tooltip("Apply bonuses automatically on Awake.")]
    public bool applyOnAwake = true;

    [Tooltip("Revert applied bonuses on OnDisable/OnDestroy.")]
    public bool revertOnDisable = true;

    // --- internal state ---
    private struct Applied
    {
        public BonusType type;
        public float value;   // store as float; for MaxHealth we also track the exact int we applied
        public int intValue;  // used only by MaxHealthFlat for precise revert/heal bookkeeping
    }
    private readonly List<Applied> _applied = new();

    // We’ll store the exact lines we injected (so we can remove them later without markers)
    private readonly List<string> _lastInjectedLines = new();

    // Cached Accessory on the same GameObject (where we will write description lines)
    private Accessory _accessory;

    private void Awake()
    {
        _accessory = GetComponent<Accessory>();
        if (applyOnAwake) ApplyBonuses();
    }

    private void OnDisable()
    {
        if (revertOnDisable) RevertBonuses();
    }

    private void OnDestroy()
    {
        if (revertOnDisable) RevertBonuses();
    }

    public void ApplyBonuses()
    {
        if (_applied.Count > 0) return; // already applied

        EnsureTarget();
        if (target == null)
        {
            Debug.LogWarning($"[AccessoryBonuses] No SimpleHealth target found for {name}.");
            return;
        }

        // build working pool of enabled options
        List<BonusOption> pool = new();
        foreach (var opt in bonusPool)
            if (opt.enabled) pool.Add(opt);

        if (pool.Count == 0)
        {
            Debug.LogWarning("[AccessoryBonuses] No enabled bonus options.");
            return;
        }

        // seed rng if requested
        if (seed != 0) Random.InitState(seed);

        int rolls = Mathf.Min(bonusesToRoll, pool.Count);
        for (int i = 0; i < rolls; i++)
        {
            int pick = Random.Range(0, pool.Count);
            var chosen = pool[pick];
            pool.RemoveAt(pick);

            float rolled = Random.Range(chosen.range.x, chosen.range.y);

            switch (chosen.type)
            {
                case BonusType.MaxHealthFlat:
                    {
                        int delta = Mathf.RoundToInt(rolled);
                        // increase cap and heal by same amount (generous behavior)
                        target.maxHealth += delta;
                        target.currentHealth = Mathf.Clamp(target.currentHealth + delta, 0, target.maxHealth);
                        target.SyncSlider();

                        _applied.Add(new Applied { type = chosen.type, value = delta, intValue = delta });
                        break;
                    }
                case BonusType.RegenPerSecond:
                    {
                        target.regenRate += rolled;
                        _applied.Add(new Applied { type = chosen.type, value = rolled, intValue = 0 });
                        break;
                    }
                case BonusType.ArmorFlat:
                    {
                        target.armor = Mathf.Max(0f, target.armor + rolled);
                        _applied.Add(new Applied { type = chosen.type, value = rolled, intValue = 0 });
                        break;
                    }
            }
        }

        // Write stat lines into this Accessory’s description so they appear in the combined extra text
        UpdateAccessoryDescription();
    }

    public void RevertBonuses()
    {
        if (_applied.Count == 0 || target == null) return;

        foreach (var a in _applied)
        {
            switch (a.type)
            {
                case BonusType.MaxHealthFlat:
                    target.maxHealth = Mathf.Max(1, target.maxHealth - a.intValue);
                    target.currentHealth = Mathf.Min(target.currentHealth, target.maxHealth);
                    target.SyncSlider();
                    break;

                case BonusType.RegenPerSecond:
                    target.regenRate = Mathf.Max(0f, target.regenRate - a.value);
                    break;

                case BonusType.ArmorFlat:
                    target.armor = Mathf.Max(0f, target.armor - a.value);
                    break;
            }
        }

        _applied.Clear();

        // Remove only the lines we previously injected (no markers needed)
        if (_accessory != null && _lastInjectedLines.Count > 0)
        {
            string current = _accessory.AccesoryDescription ?? string.Empty;
            string cleaned = RemoveExactLines(current, _lastInjectedLines);
            _lastInjectedLines.Clear();
            _accessory.SetDescription(cleaned);
        }
    }

    private void UpdateAccessoryDescription()
    {
        if (_accessory == null) return;

        // Build neat stat lines formatted to match your combiner:
        // "+5 Armor", "+1.2 Regen/s", "+25 Max Health", etc.
        _lastInjectedLines.Clear();


        foreach (var a in _applied)
        {
            switch (a.type)
            {
                case BonusType.MaxHealthFlat:
                    _lastInjectedLines.Add($"+{a.intValue} Max Health");
                    break;
                case BonusType.RegenPerSecond:
                    _lastInjectedLines.Add($"+{FormatFloat(a.value)} Regen/s");
                    break;
                case BonusType.ArmorFlat:
                    _lastInjectedLines.Add($"+{FormatFloat(a.value)} Armor");
                    break;
            }
        }

        // Merge: append our lines to whatever description is already there
        // (No sentinel markers. On revert we’ll delete these exact lines.)
        string baseDesc = _accessory.AccesoryDescription ?? string.Empty;
        string merged = AppendUniqueLines(baseDesc, _lastInjectedLines);
        _accessory.SetDescription(merged);
    }

    // --- helpers ---
    private void EnsureTarget()
    {
        if (target != null) return;

        // 1) Player tag
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.GetComponentInChildren<SimpleHealth>(true);
        if (target != null) return;

        // 2) First in parents
        target = GetComponentInParent<SimpleHealth>(true);
        if (target != null) return;

        // 3) First in scene
        target = FindFirstObjectByType<SimpleHealth>();
    }

    private static string FormatFloat(float v)
    {
        // int-like -> no decimals; else one decimal
        return Mathf.Abs(v - Mathf.Round(v)) < 0.0001f ? Mathf.RoundToInt(v).ToString() : v.ToString("0.0");
    }

    private static string AppendUniqueLines(string text, List<string> linesToAdd)
    {
        if (linesToAdd == null || linesToAdd.Count == 0) return text ?? string.Empty;

        // Split existing into lines for duplicate check (preserve original newlines)
        var existing = new HashSet<string>(StringComparer.Ordinal);
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(text))
        {
            foreach (var ln in SplitLines(text))
            {
                existing.Add(ln);
                sb.AppendLine(ln);
            }
        }

        // Append only new/unique lines
        foreach (var ln in linesToAdd)
        {
            if (!existing.Contains(ln))
            {
                sb.AppendLine(ln);
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string RemoveExactLines(string text, List<string> linesToRemove)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (linesToRemove == null || linesToRemove.Count == 0) return text;

        var toRemove = new HashSet<string>(linesToRemove, StringComparer.Ordinal);
        var sb = new StringBuilder();
        foreach (var ln in SplitLines(text))
        {
            if (!toRemove.Contains(ln))
                sb.AppendLine(ln);
        }
        return sb.ToString().TrimEnd();
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        int idx = 0;
        while (idx < text.Length)
        {
            int next = text.IndexOf('\n', idx);
            if (next < 0)
            {
                yield return text.Substring(idx).TrimEnd('\r');
                yield break;
            }
            else
            {
                int len = next - idx;
                string line = text.Substring(idx, len).TrimEnd('\r');
                yield return line;
                idx = next + 1;
            }
        }
    }
}
