using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public class PlayerStatUpgradeDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional explicit reference. If left empty, will try to find it on the Player (tag).")]
    public PlayerStatUpgrades upgrades;

    [Tooltip("If not set, will use GetComponent<TextMeshProUGUI>().")]
    public TextMeshProUGUI textUI;

    private void Awake()
    {
        if (textUI == null) textUI = GetComponent<TextMeshProUGUI>();

        if (upgrades == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                upgrades = player.GetComponent<PlayerStatUpgrades>();
        }
    }

    private void Update()
    {
        if (textUI == null)
            textUI = GetComponent<TextMeshProUGUI>();

        if (upgrades == null || upgrades.modifiers == null || upgrades.modifiers.Count == 0)
        {
            textUI.text = "<color=grey>No upgrades</color>";
            return;
        }

        // --- Merge duplicates by (Target, Stat) for DISPLAY ONLY ---
        var sums = new Dictionary<(PlayerStatUpgrades.TargetGroup tg, PlayerStatUpgrades.Stat st), (float add, float pct)>();
        var order = new List<(PlayerStatUpgrades.TargetGroup tg, PlayerStatUpgrades.Stat st)>(); // preserve first-seen order

        foreach (var m in upgrades.modifiers)
        {
            var key = (m.target, m.stat);
            if (!sums.TryGetValue(key, out var acc))
            {
                acc = (0f, 0f);
                sums[key] = acc;
                order.Add(key);
            }
            acc.add += m.add;
            acc.pct += m.percent;
            sums[key] = acc;
        }

        // Build UI text
        var sb = new StringBuilder();
        sb.AppendLine("<b>Upgrades</b>");

        foreach (var key in order)
        {
            var acc = sums[key];
            string friendlyTarget = GetFriendlyTarget(key.tg.ToString());
            string friendlyStat = GetFriendlyStat(key.st.ToString());

            // Header in gold
            sb.Append("<color=#FFD700>");
            sb.Append(friendlyTarget).Append(" - ").Append(friendlyStat);
            sb.Append("</color>: ");

            // Values (merged totals)
            if (Mathf.Abs(acc.add) > 0.001f)
            {
                string col = acc.add >= 0 ? "green" : "red";
                sb.Append($"<color={col}>{(acc.add >= 0 ? "+" : "")}{acc.add:0.##}</color> ");
            }

            if (Mathf.Abs(acc.pct) > 0.0001f)
            {
                string col = acc.pct >= 0 ? "green" : "red";
                sb.Append($"<color={col}>{(acc.pct >= 0 ? "+" : "")}{acc.pct * 100f:0.#}%</color>");
            }

            sb.AppendLine();
        }

        textUI.text = sb.ToString();
    }

    private string GetFriendlyTarget(string raw) => raw switch
    {
        "PlayerHealth" => "Player",
        "AllWeapons" => "Global",
        "WeaponTick" => "Attack Speed",
        "SimpleShooter" => "Ranged",
        "Knife" => "Melee",
        _ => InsertSpaces(raw)
    };

    private string GetFriendlyStat(string raw) => raw switch
    {
        // Player stats
        "MaxHealth" => "Max HP",
        "RegenPerSecond" => "HP Regen",
        "Armor" => "Armor",

        // Shared weapon stats
        "Damage" => "Damage",
        "CritChance" => "Crit Chance",
        "CritMultiplier" => "Crit Damage",
        "StatusChance" => "Status Chance",

        // Knife
        "KnifeRadius" => "Radius",
        "KnifeSplashRadius" => "Splash Radius",
        "KnifeSplashPercent" => "Splash Damage %",
        "KnifeLifesteal" => "Lifesteal",
        "KnifeMaxTargets" => "Max Targets",

        // Shooter
        "ShooterProjectileCount" => "Projectile Count",
        "ShooterSpreadAngle" => "Spread Angle",
        "ShooterForce" => "Bullet Force",
        "ShooterBulletLifetime" => "Bullet Lifetime",

        // Tick
        "TickInterval" => "Interval",
        "TickBurstCount" => "Burst Count",
        "TickBurstSpacing" => "Burst Spacing",
        "AttackSpeed" => "Attack Speed",

        _ => InsertSpaces(raw)
    };

    /// <summary> Inserts spaces before capital letters. </summary>
    private string InsertSpaces(string s) => Regex.Replace(s, "(\\B[A-Z])", " $1");
}
