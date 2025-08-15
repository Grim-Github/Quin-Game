using System.Collections.Generic;
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

    [Header("Upgrades")]
    [HideInInspector] public AccessoriesUpgrades nextUpgrade;
    private PowerUpChooser powerUpChooser;

    // --- UI (mirrors Knife) ---
    [Header("UI")]
    [Tooltip("Prefab root GameObject that contains a TextMeshProUGUI somewhere in its children.")]
    [SerializeField] public GameObject statsTextPrefab;
    [SerializeField] private Transform uiParent;

    [TextArea, SerializeField] private string extraTextField; // combined description block (self + active children; auto-combined)
    [HideInInspector] public TextMeshProUGUI statsTextInstance;
    private Image iconImage;

    private void Awake()
    {
        // Queue accessory upgrade into PowerUpChooser
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();
        if (nextUpgrade == null)
            nextUpgrade = GetComponentInChildren<AccessoriesUpgrades>(true);

        if (nextUpgrade != null && powerUpChooser != null)
            powerUpChooser.powerUps.Add(nextUpgrade.Upgrade);

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

    private string CombineStatLines(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Split on newlines and commas to be flexible with "a, b, c" or line-per-stat formats
        var pieces = new List<string>();
        var lines = raw.Split('\n');
        foreach (var ln in lines)
        {
            var parts = ln.Split(',');
            foreach (var p in parts)
            {
                var s = p.Trim();
                if (s.Length > 0) pieces.Add(s);
            }
        }

        // Aggregation structures
        var combinedOrder = new List<string>(); // preserves first-seen order of keys
        var combinedValues = new Dictionary<string, float>(); // key -> sum
        var percentFlags = new Dictionary<string, bool>(); // key -> isPercent
        var rawUnparsed = new List<string>(); // keep lines we couldn't parse

        foreach (var piece in pieces)
        {
            var m = statLineRegex.Match(piece);
            if (!m.Success)
            {
                rawUnparsed.Add(piece);
                continue;
            }

            // Parse numeric value
            if (!float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            {
                rawUnparsed.Add(piece);
                continue;
            }

            bool isPercent = m.Groups[2].Value == "%";
            string nameRaw = m.Groups[3].Value.Trim();

            // Normalize key (case-insensitive, compact spaces)
            string normName = NormalizeStatName(nameRaw);
            string key = (isPercent ? "%" : "") + normName; // differentiate  "crit" vs "%crit"

            if (!combinedValues.ContainsKey(key))
            {
                combinedValues[key] = 0f;
                percentFlags[key] = isPercent;
                combinedOrder.Add(key);
            }
            combinedValues[key] += val;
        }

        // Build output: combined stats + any unparsed lines
        var outLines = new List<string>();

        foreach (var key in combinedOrder)
        {
            float sum = combinedValues[key];
            bool isPercent = percentFlags[key];
            string displayName = DenormalizeStatName(key, isPercent);

            // Skip zero results (e.g., +5 and -5 cancel)
            if (Mathf.Approximately(sum, 0f)) continue;

            string num = FormatNumber(sum);
            string sign = sum > 0 ? "+" : ""; // minus already included by formatting for negatives
            string pct = isPercent ? "%" : "";
            outLines.Add($"{sign}{num}{pct} {displayName}");
        }

        // Preserve any non-matching lines (at the end, in original encounter order)
        outLines.AddRange(rawUnparsed);

        // Join with newline
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
