using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[AddComponentMenu("UI/Accessory Stats Manager")]
public class AccessoryStatsManager : MonoBehaviour
{
    [Header("Target UI")]
    [Tooltip("If left empty, auto-finds a TextMeshProUGUI named 'Accessory Stats'.")]
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("Formatting")]
    [SerializeField] private bool showHeader = true;
    [SerializeField] private string headerText = "<b>Accessories</b>";
    [SerializeField] private string bullet = "• ";
    [SerializeField] private bool showName = true;  // prints 'Name: Description'

    [Header("Refresh")]
    [Tooltip("Auto-refresh every short interval (unscaled, works while paused/slow-mo).")]
    [SerializeField] private bool autoRefresh = true;
    [SerializeField] private float refreshInterval = 0.5f;

    private float _nextRefreshTime;

    private void Awake()
    {
        EnsureTextRef();
        Refresh();
    }

    private void OnEnable()
    {
        EnsureTextRef();
        Refresh();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureTextRef();
        if (!Application.isPlaying) Refresh();
    }
#endif

    private void Update()
    {
        if (!autoRefresh) return;
        if (Time.unscaledTime >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            Refresh();
        }
    }

    public void Refresh()
    {
        if (statsText == null) return;

        var lines = new List<string>();
        var seen = new HashSet<string>();

        // 1) AccessoriesUpgrades (uses PowerUp data)
        var upgrades = FindObjectsOfType<AccessoriesUpgrades>(includeInactive: false);
        foreach (var acc in upgrades)
        {
            if (acc == null || !acc.isActiveAndEnabled) continue;
            if (acc.Upgrade == null) continue;

            string name = acc.Upgrade.powerUpName;
            string desc = acc.Upgrade.powerUpDescription;
            if (string.IsNullOrWhiteSpace(desc)) continue;

            string line = showName && !string.IsNullOrWhiteSpace(name)
                ? $"{bullet}{name}: {desc}"
                : $"{bullet}{desc}";

            if (seen.Add(line)) lines.Add(line);
        }

        // 2) Accessory (uses AccesoryDescription from the component)
        var plainAccessories = FindObjectsOfType<Accessory>(includeInactive: false);
        foreach (var a in plainAccessories)
        {
            if (a == null || !a.isActiveAndEnabled) continue;

            // NOTE: Using the field name exactly as defined: AccesoryDescription
            string desc = a.AccesoryDescription;
            if (string.IsNullOrWhiteSpace(desc)) continue;

            string name = showName ? a.gameObject.name : null;
            string line = showName && !string.IsNullOrWhiteSpace(name)
                ? $"{bullet}{name}: {desc}"
                : $"{bullet}{desc}";

            if (seen.Add(line)) lines.Add(line);
        }

        var sb = new StringBuilder();
        if (showHeader) sb.AppendLine(headerText);
        for (int i = 0; i < lines.Count; i++)
            sb.AppendLine(lines[i]);

        statsText.text = sb.ToString().TrimEnd();
    }


    private void EnsureTextRef()
    {
        if (statsText != null) return;

        // Try direct name lookup first (active only)
        var go = GameObject.Find("Accessory Stats");
        if (go) statsText = go.GetComponent<TextMeshProUGUI>();
    }
}
