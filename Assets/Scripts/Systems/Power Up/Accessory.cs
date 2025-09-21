﻿using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Accessory : MonoBehaviour
{
    [Header("Power-Up")]
    public string AccesoryName;
    [TextArea] public string AccesoryDescription;
    public Sprite icon;

    [Header("Event to trigger on Awake")]
    public UnityEvent onAwake;

    private PowerUpChooser powerUpChooser;

    // --- UI (mirrors Knife) ---
    [Header("UI")]
    [Tooltip("Prefab root GameObject that contains a TextMeshProUGUI somewhere in its children.")]
    [SerializeField] public GameObject statsTextPrefab;
    [SerializeField] private Transform uiParent;

    private string extraTextField; // combined description block (self + active children; auto-combined)
    [HideInInspector] public TextMeshProUGUI statsTextInstance;
    private Image iconImage;

    private void Awake()
    {
        // Queue accessory upgrade into PowerUpChooser
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();

        // Instantiate UI like Knife
        if (statsTextPrefab != null && uiParent != null)
        {
            var go = Instantiate(statsTextPrefab, uiParent);
            statsTextInstance = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (statsTextInstance != null) statsTextInstance.text = "";

            var iconObj = go.transform.Find("Icon");
            if (iconObj != null) iconImage = iconObj.GetComponent<Image>();
            if (iconImage != null) iconImage.sprite = icon;
        }

        onAwake?.Invoke();

        // Initial content from active children only
        RebuildDescriptionsFromActiveChildren();
        RefreshUI();
    }

    private void OnEnable() { NotifyRootToRefresh(); }
    private void OnDisable() { NotifyRootToRefresh(); }
    private void OnTransformChildrenChanged() { NotifyRootToRefresh(); }

    // Public API — call this from upgrades after you activate/deactivate child accessories or edit descriptions
    public void NotifyRootToRefresh()
    {
        var root = GetRootAccessory();
        root.RebuildDescriptionsFromActiveChildren();
        root.RefreshUI();
    }

    // Finds the topmost Accessory (the one that should own the UI)
    private Accessory GetRootAccessory()
    {
        Accessory root = this;
        var p = transform.parent;
        while (p != null)
        {
            var acc = p.GetComponent<Accessory>();
            if (acc != null) root = acc;
            p = p.parent;
        }
        return root;
    }

    // Combines this description + ONLY active child Accessory descriptions, then merges similar stat lines.
    public void RebuildDescriptionsFromActiveChildren()
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(AccesoryDescription))
            sb.AppendLine(AccesoryDescription);

        var childAccessories = GetComponentsInChildren<Accessory>(true);
        foreach (var acc in childAccessories)
        {
            if (acc == this) continue;                             // skip self
            if (!acc.gameObject.activeInHierarchy) continue;       // ONLY active children
            if (!string.IsNullOrWhiteSpace(acc.AccesoryDescription))
                sb.AppendLine(acc.AccesoryDescription);
        }

        // Merge similar lines like "+5 armor, +10 armor, 20 armor" -> "+35 armor"
        extraTextField = CombineStatLines(sb.ToString().TrimEnd());
    }

    private void RefreshUI()
    {
        if (statsTextInstance == null) return;

        var sb = new StringBuilder();
        string title = string.IsNullOrWhiteSpace(AccesoryName) ? name : AccesoryName;
        sb.AppendLine($"<b>{title}</b>");

        if (!string.IsNullOrWhiteSpace(extraTextField))
            sb.AppendLine(extraTextField);



        statsTextInstance.text = sb.ToString();
    }


    public void RemoveStatsText()
    {
        if (statsTextInstance != null)
        {
            Destroy(statsTextInstance.gameObject.transform.root.gameObject);
            statsTextInstance = null;
        }
    }

    // Optional convenience if you edit descriptions at runtime
    public void SetDescription(string desc)
    {
        AccesoryDescription = desc;
        NotifyRootToRefresh();
    }

    // Example action you previously had; left intact
    public void UpProjectileCount()
    {
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) return;

        var shooters = player.GetComponentsInChildren<SimpleShooter>(true);
        foreach (var shooter in shooters)
            shooter.projectileCount += 1;
    }

    // --- Stat line combiner ---
    // Supported formats (case-insensitive):
    // "+5 armor", "10 armor", "-2.5 armor", "+5% crit", "12.3% attack speed"
    // Anything not matching is preserved verbatim (line-by-line).
    private static readonly Regex statLineRegex = new Regex(
        @"^\s*([+\-]?\d+(?:\.\d+)?)\s*(%?)\s+([A-Za-z][A-Za-z\s/_\-\.]*)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Call with keepMarkersInOutput=true when saving back to AccesoryDescription (preserve markers).
    // Call with keepMarkersInOutput=false when building UI text (hide markers).
    private string CombineStatLines(string raw, bool keepMarkersInOutput = true)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Split on newlines and commas
        var pieces = new List<string>();
        foreach (var ln in raw.Split('\n'))
            foreach (var p in ln.Split(','))
            {
                var s = p.Trim();
                if (s.Length > 0) pieces.Add(s);
            }

        var combinedOrder = new List<string>();
        var combinedValues = new Dictionary<string, float>();
        var percentFlags = new Dictionary<string, bool>();
        var rawUnparsed = new List<string>();

        foreach (var piece in pieces)
        {
            // Keep markers as-is, but don't parse them as stats
            bool isStart = piece == "===AccessoryBonuses===";
            bool isEnd = piece == "===/AccessoryBonuses===";

            if (isStart || isEnd)
            {
                if (keepMarkersInOutput) rawUnparsed.Add(piece);
                // If not keeping markers in output, we still skip parsing (so stats are recognized elsewhere)
                continue;
            }

            var m = statLineRegex.Match(piece);
            if (!m.Success)
            {
                rawUnparsed.Add(piece);
                continue;
            }

            if (!float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            {
                rawUnparsed.Add(piece);
                continue;
            }

            bool isPercent = m.Groups[2].Value == "%";
            string nameRaw = m.Groups[3].Value.Trim();

            string normName = NormalizeStatName(nameRaw);
            string key = (isPercent ? "%" : "") + normName;

            if (!combinedValues.ContainsKey(key))
            {
                combinedValues[key] = 0f;
                percentFlags[key] = isPercent;
                combinedOrder.Add(key);
            }
            combinedValues[key] += val;
        }

        var outLines = new List<string>();

        foreach (var key in combinedOrder)
        {
            float sum = combinedValues[key];
            bool isPercent = percentFlags[key];
            if (Mathf.Approximately(sum, 0f)) continue;

            string displayName = DenormalizeStatName(key, isPercent);
            string num = FormatNumber(sum);
            string sign = sum > 0 ? "+" : "";
            string pct = isPercent ? "%" : "";
            outLines.Add($"{sign}{num}{pct} {displayName}");
        }

        // Append any non-stat lines (and, depending on flag, the markers)
        foreach (var line in rawUnparsed)
            outLines.Add(line);

        return string.Join("\n", outLines);
    }


    private static string NormalizeStatName(string s)
    {
        // Lowercase, collapse multiple spaces to single, trim
        var t = s.ToLowerInvariant().Trim();
        var sb = new StringBuilder(t.Length);
        bool prevSpace = false;
        foreach (char c in t)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!prevSpace) { sb.Append(' '); prevSpace = true; }
            }
            else
            {
                sb.Append(c);
                prevSpace = false;
            }
        }
        return sb.ToString();
    }



    private static string DenormalizeStatName(string key, bool isPercent)
    {
        // Remove our leading '%' marker if present
        if (isPercent && key.StartsWith("%")) key = key.Substring(1);

        // Title Case the first letter of each word (simple approach)
        var words = key.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            var w = words[i];
            if (w.Length == 0) continue;
            if (w.Length == 1) words[i] = char.ToUpperInvariant(w[0]).ToString();
            else words[i] = char.ToUpperInvariant(w[0]) + w.Substring(1);
        }
        return string.Join(" ", words);
    }

    private static string FormatNumber(float x)
    {
        // If effectively integer, show as int; else 1 decimal
        if (Mathf.Abs(x - Mathf.Round(x)) < 0.0001f)
            return Mathf.RoundToInt(x).ToString(CultureInfo.InvariantCulture);

        return x.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
