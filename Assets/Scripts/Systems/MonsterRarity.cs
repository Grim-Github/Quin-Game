using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterRarity : MonoBehaviour
{
    public enum Rarity { Common, Uncommon, Rare, Legendary }

    [Header("Auto Roll")]
    [SerializeField] private bool rollOnStart = true;

    [Header("Current")]
    [SerializeField] private Rarity rarity = Rarity.Common;

    [Header("Rarity Weights")]
    [SerializeField] private float weightCommon = 60f;
    [SerializeField] private float weightUncommon = 25f;
    [SerializeField] private float weightRare = 12f;
    [SerializeField] private float weightLegendary = 3f;

    // === Enemy (SimpleHealth) roll ranges ===
    [Header("Enemy Health & Defense Rolls")]
    [SerializeField] private Vector2Int hpFlatAdd = new Vector2Int(15, 60);
    [SerializeField] private Vector2 hpMult = new Vector2(1.10f, 1.35f);
    [SerializeField] private Vector2 regenAdd = new Vector2(0.2f, 2.0f);
    [SerializeField] private Vector2 armorAdd = new Vector2(1f, 6f);

    // === Movement (EnemyChaser) ===
    [Header("Chase / Movement Rolls")]
    [SerializeField] private Vector2 moveSpeedAdd = new Vector2(0.5f, 2.5f);

    // === Global cadence (WeaponTick) ===
    [Header("Global Attack Cadence (WeaponTick)")]
    [SerializeField] private Vector2 atkSpeedFracAll = new Vector2(0.08f, 0.25f);

    // === Knife rolls (public fields) ===
    [Header("Knife Rolls")]
    [SerializeField] private Vector2Int KnifeDamageFlat = new Vector2Int(2, 12);
    [SerializeField] private Vector2 KnifeDamageMult = new Vector2(1.08f, 1.30f);
    [SerializeField] private Vector2 KnifeLifestealAdd = new Vector2(0.03f, 0.15f);
    // NOTE: removed KnifeMaxTargetsAdd (per request)
    [SerializeField] private Vector2 KnifeCritChanceAdd = new Vector2(0.05f, 0.20f);
    [SerializeField] private Vector2 KnifeCritMultAdd = new Vector2(0.20f, 0.80f);

    // === Shooter rolls (public fields) ===
    [Header("Shooter Rolls")]
    [SerializeField] private Vector2Int shooterDamageFlat = new Vector2Int(2, 12);
    [SerializeField] private Vector2 shooterDamageMult = new Vector2(1.08f, 1.30f);
    [SerializeField] private Vector2Int shooterProjectilesAdd = new Vector2Int(1, 2);

    // Cached refs
    private SimpleHealth health;        // needs public: int maxHealth, int currentHealth, float regenRate, float armor;
                                        // public UnityEngine.UI.Slider healthSlider; public TMPro.TextMeshProUGUI healthText;
                                        // public string extraTextField; public void Heal(int amt); public void UpdateStatsText();
    private EnemyChaser chaser;         // needs public: float moveSpeed;
    private Knife[] knives;             // must have public fields used below
    private SimpleShooter[] shooters;   // must have public fields used below
    private WeaponTick[] ticks;         // needs public: float interval; public void ResetAndStart();

    // Visible notes (pre-styled lines)
    private readonly List<string> notesEnemy = new();
    private readonly List<string> notesWeapons = new();

    // ===== UI Colors (TMP rich text) =====
    private const string C_HEADER = "#8BD3FF";   // headers
    private const string C_LABEL = "#EAEAEA";   // label text
    private const string C_VALUE = "#FFD24D";   // numbers
    private const string C_TEXT = "#D8E6F2";   // base text
    private const string C_RARE = "#3AA0FF";
    private const string C_UNC = "#3EC46D";
    private const string C_COM = "#B0B0B0";
    private const string C_LEG = "#FFB347";

    private void Awake() => RefreshCachedRefs();

    private void Start()
    {
        if (rollOnStart)
            RerollRarity();
    }

    private void OnTransformChildrenChanged() => RefreshCachedRefs();

    private void RefreshCachedRefs()
    {
        health = GetComponent<SimpleHealth>();
        chaser = GetComponent<EnemyChaser>();
        knives = GetComponentsInChildren<Knife>(true);
        shooters = GetComponentsInChildren<SimpleShooter>(true);
        ticks = GetComponentsInChildren<WeaponTick>(true);
    }

    // ===== Public / Context API =====
    [ContextMenu("Monster Rarity / Refresh & Reroll")]
    private void RefreshAndReroll()
    {
        RefreshCachedRefs();
        RerollRarity();
    }

    [ContextMenu("Monster Rarity / Reroll Rarity + Stats")]
    public void RerollRarity()
    {
        RefreshCachedRefs();
        rarity = RollWeightedRarity();
        RerollStats();
    }

    [ContextMenu("Monster Rarity / Reroll Stats Only")]
    public void RerollStats()
    {
        RefreshCachedRefs();
        notesEnemy.Clear();
        notesWeapons.Clear();

        int rolls = rarity switch
        {
            Rarity.Common => 1,
            Rarity.Uncommon => 2,
            Rarity.Rare => 4,
            Rarity.Legendary => 5,
            _ => 1
        };

        var candidates = BuildCandidates();
        Shuffle(candidates);
        for (int i = 0; i < Mathf.Min(rolls, candidates.Count); i++)
            candidates[i].Invoke();

        // Optional extra: cadence tweak across all weapons (60% chance)
        if (ticks != null && ticks.Length > 0 && UnityEngine.Random.value < 0.6f)
            Upgrade_AllWeaponTickSpeed();

        // Restart WeaponTick safely
        foreach (var t in ticks)
            if (t && t.isActiveAndEnabled)
                t.ResetAndStart();

        // === Write EVERYTHING to parent entity’s UI ===
        WriteIntoParentUI();
    }

    // ===== Candidate pool =====
    private List<Action> BuildCandidates()
    {
        var list = new List<Action>();

        if (health)
        {
            list.Add(Up_HP_Flat);
            list.Add(Up_HP_Mult);
            list.Add(Up_Regen_Add);
            list.Add(Up_Armor_Add);
        }

        if (chaser)
        {
            list.Add(Up_MoveSpeed_Add);
        }

        if (knives != null && knives.Length > 0)
        {
            list.Add(Up_Knife_Dmg_Flat);
            list.Add(Up_Knife_Dmg_Mult);
            list.Add(Up_Knife_Lifesteal_Add);
            list.Add(Up_Knife_Crit_Both);
        }

        if (shooters != null && shooters.Length > 0)
        {
            list.Add(Up_Shooter_Dmg_Flat);
            list.Add(Up_Shooter_Dmg_Mult);
            list.Add(Up_Shooter_Projectiles_Add);
        }

        return list;
    }

    // ===== Enemy (SimpleHealth) upgrades =====
    private void Up_HP_Flat()
    {
        if (!health) return;

        int add = UnityEngine.Random.Range(hpFlatAdd.x, hpFlatAdd.y + 1);
        health.maxHealth += add;
        health.currentHealth = health.maxHealth; // full heal

        EN("Max Health", $"+{add}");
        health.UpdateStatsText();
    }

    private void Up_HP_Mult()
    {
        if (!health) return;

        float mult = UnityEngine.Random.Range(hpMult.x, hpMult.y);
        health.maxHealth = Mathf.RoundToInt(health.maxHealth * mult);
        health.currentHealth = health.maxHealth; // full heal

        EN("Max Health", $"×{mult:F2}");
        health.UpdateStatsText();
    }

    private void Up_Regen_Add()
    {
        if (!health) return;
        float add = UnityEngine.Random.Range(regenAdd.x, regenAdd.y);
        health.regenRate = Mathf.Max(0f, health.regenRate + add);
        EN("Regen", $"+{add:F2}/s");
        health.UpdateStatsText();
    }

    private void Up_Armor_Add()
    {
        if (!health) return;
        float add = UnityEngine.Random.Range(armorAdd.x, armorAdd.y);
        health.armor = Mathf.Max(0f, health.armor + add);
        EN("Armor", $"+{add:F1}");
    }

    // ===== Movement (EnemyChaser) =====
    private void Up_MoveSpeed_Add()
    {
        if (!chaser) return;
        float add = UnityEngine.Random.Range(moveSpeedAdd.x, moveSpeedAdd.y);
        chaser.moveSpeed = Mathf.Max(0f, chaser.moveSpeed + add);
        EN("Move Speed", $"+{add:F1}");
    }

    // ===== Global cadence =====
    private void Upgrade_AllWeaponTickSpeed()
    {
        float frac = UnityEngine.Random.Range(atkSpeedFracAll.x, atkSpeedFracAll.y);
        if (ticks == null) return;

        foreach (var t in ticks)
        {
            if (!t) continue;
            float before = t.interval;
            float after = Mathf.Max(0.01f, before * (1f - frac));
            t.interval = after;
            if (t.isActiveAndEnabled) t.ResetAndStart();
        }
        WN("Attack Interval (All)", $"-{frac * 100f:F0}%");
    }

    // ===== Knife (public fields) =====
    private void Up_Knife_Dmg_Flat()
    {
        int add = UnityEngine.Random.Range(KnifeDamageFlat.x, KnifeDamageFlat.y + 1);
        foreach (var k in knives) if (k) k.damage = Mathf.Max(0, k.damage + add);
        WN("Knife Damage", $"+{add}");
    }

    private void Up_Knife_Dmg_Mult()
    {
        float m = UnityEngine.Random.Range(KnifeDamageMult.x, KnifeDamageMult.y);
        foreach (var k in knives) if (k) k.damage = Mathf.RoundToInt(k.damage * m);
        WN("Knife Damage", $"×{m:F2}");
    }

    private void Up_Knife_Lifesteal_Add()
    {
        float add = UnityEngine.Random.Range(KnifeLifestealAdd.x, KnifeLifestealAdd.y);
        foreach (var k in knives) if (k) k.lifestealPercent = Mathf.Clamp01(k.lifestealPercent + add);
        WN("Knife Lifesteal", $"+{add * 100f:F0}%");
    }

    private void Up_Knife_Crit_Both()
    {
        float addChance = UnityEngine.Random.Range(KnifeCritChanceAdd.x, KnifeCritChanceAdd.y);
        float addMult = UnityEngine.Random.Range(KnifeCritMultAdd.x, KnifeCritMultAdd.y);
        foreach (var k in knives)
        {
            if (!k) continue;
            k.critChance = Mathf.Clamp01(k.critChance + addChance);
            k.critMultiplier = Mathf.Max(1f, k.critMultiplier + addMult);
        }
        WN("Knife Crit (Chance & Mult)", $"+{addChance * 100f:F0}% / +{addMult:F2}x");
    }

    // ===== Shooter (public fields) =====
    private void Up_Shooter_Dmg_Flat()
    {
        int add = UnityEngine.Random.Range(shooterDamageFlat.x, shooterDamageFlat.y + 1);
        foreach (var s in shooters) if (s) s.damage = Mathf.Max(0, s.damage + add);
        WN("Shooter Damage", $"+{add}");
    }

    private void Up_Shooter_Dmg_Mult()
    {
        float m = UnityEngine.Random.Range(shooterDamageMult.x, shooterDamageMult.y);
        foreach (var s in shooters) if (s) s.damage = Mathf.RoundToInt(s.damage * m);
        WN("Shooter Damage", $"×{m:F2}");
    }

    private void Up_Shooter_Projectiles_Add()
    {
        int add = UnityEngine.Random.Range(shooterProjectilesAdd.x, shooterProjectilesAdd.y + 1);
        foreach (var s in shooters) if (s) s.projectileCount = Mathf.Max(1, s.projectileCount + add);
        WN("Shooter Projectiles", $"+{add}");
    }

    // ===== Rarity & UI =====
    private Rarity RollWeightedRarity()
    {
        float total = weightCommon + weightUncommon + weightRare + weightLegendary;
        float r = UnityEngine.Random.value * Mathf.Max(0.0001f, total);
        if ((r -= weightCommon) < 0) return Rarity.Common;
        if ((r -= weightUncommon) < 0) return Rarity.Uncommon;
        if ((r -= weightRare) < 0) return Rarity.Rare;
        return Rarity.Legendary;
    }

    private void WriteIntoParentUI()
    {
        if (!health) return;

        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"<b>{C(C_LABEL, "Rarity:")} {FormatRarity(rarity)}</b>");

        // Enemy section
        if (notesEnemy.Count > 0)
        {
            sb.AppendLine(C(C_HEADER, "<b>Enemy Mods</b>"));
            foreach (var line in notesEnemy) sb.AppendLine(line);
        }

        // Weapon section
        if (notesWeapons.Count > 0)
        {
            sb.AppendLine(C(C_HEADER, "<b>Weapon Mods</b>"));
            foreach (var line in notesWeapons) sb.AppendLine(line);
        }

        // Wrap with base text color and slightly smaller size for compactness
        string block = $"{C(C_TEXT, $"<size=85%>{sb}</size>")}";

        // Replace previous rarity block in SimpleHealth.extraTextField
        string cur = health.extraTextField ?? "";
        string cleaned = RemoveRaritySection(cur);
        string combined = string.IsNullOrWhiteSpace(cleaned) ? block : $"{cleaned}\n{block}";
        health.extraTextField = combined;

        // Update UI (method assumed public)
        try { health.UpdateStatsText(); } catch { /* ignore if not present */ }
    }



    // ===== Formatting helpers =====
    private static string C(string hex, string text) => $"<color={hex}>{text}</color>";
    private static string Bullet(string label, string value)
        => $"• {C(C_LABEL, label)}: {C(C_VALUE, value)}";

    private void EN(string label, string value) => notesEnemy.Add(Bullet(label, value));
    private void WN(string label, string value) => notesWeapons.Add(Bullet(label, value));

    private static string RemoveRaritySection(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int idx = s.IndexOf("Rarity:", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? s[..idx].TrimEnd() : s;
    }

    private static string FormatRarity(Rarity r) => r switch
    {
        Rarity.Common => C(C_COM, "Common"),
        Rarity.Uncommon => C(C_UNC, "Uncommon"),
        Rarity.Rare => C(C_RARE, "Rare"),
        Rarity.Legendary => C(C_LEG, "Legendary"),
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
