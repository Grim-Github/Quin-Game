using System;
using System.Collections.Generic;
using System.Reflection;
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
    [SerializeField] private bool mayTuneReachEvent = true;

    // === Global cadence (WeaponTick) ===
    [Header("Global Attack Cadence (WeaponTick)")]
    [SerializeField] private Vector2 atkSpeedFracAll = new Vector2(0.08f, 0.25f);

    // === Knife rolls (public fields) ===
    [Header("Knife Rolls")]
    [SerializeField] private Vector2Int knifeDamageFlat = new Vector2Int(2, 12);
    [SerializeField] private Vector2 knifeDamageMult = new Vector2(1.08f, 1.30f);
    [SerializeField] private Vector2 knifeLifestealAdd = new Vector2(0.03f, 0.15f);
    [SerializeField] private Vector2Int knifeMaxTargetsAdd = new Vector2Int(1, 3);
    [SerializeField] private Vector2 knifeCritChanceAdd = new Vector2(0.05f, 0.20f);
    [SerializeField] private Vector2 knifeCritMultAdd = new Vector2(0.20f, 0.80f);

    // === Shooter rolls (public fields) ===
    [Header("Shooter Rolls")]
    [SerializeField] private Vector2Int shooterDamageFlat = new Vector2Int(2, 12);
    [SerializeField] private Vector2 shooterDamageMult = new Vector2(1.08f, 1.30f);
    [SerializeField] private Vector2Int shooterProjectilesAdd = new Vector2Int(1, 2);

    // Cached refs
    private SimpleHealth health;
    private EnemyChaser chaser;
    private Knife[] knives;
    private SimpleShooter[] shooters;
    private WeaponTick[] ticks;

    // Visible notes (pre-styled lines)
    private readonly List<string> notesEnemy = new();
    private readonly List<string> notesWeapons = new();

    // ===== UI Colors (TMP rich text) =====
    // tweak if you want different palette
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
                CallPublicIfExists(t, "ResetAndStart");

        // === Write EVERYTHING to parent entity’s UI ===
        WriteIntoParentUI();
    }

    // ===== Candidate pool (REMOVED: 5,6,7,10,14,15,23,24) =====
    private List<Action> BuildCandidates()
    {
        var list = new List<Action>();

        if (health)
        {
            list.Add(Up_HP_Flat);          // +flat max HP
            list.Add(Up_HP_Mult);          // x% max HP
            list.Add(Up_Regen_Add);        // +regen
            list.Add(Up_Armor_Add);        // +armor
        }

        if (chaser)
        {
            list.Add(Up_MoveSpeed_Add);    // +move speed
            if (mayTuneReachEvent) list.Add(Up_ReachEvent_Tune); // reach cadence
        }

        if (knives != null && knives.Length > 0)
        {
            list.Add(Up_Knife_Dmg_Flat);
            list.Add(Up_Knife_Dmg_Mult);
            list.Add(Up_Knife_Lifesteal_Add);
            list.Add(Up_Knife_MaxTargets_Add);
            list.Add(Up_Knife_CritChance_Add);
            list.Add(Up_Knife_CritMult_Add);
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

        if (TryGetPrivate(health, "maxHealth", out int maxHP))
        {
            int newMax = Mathf.Max(1, maxHP + add);
            TrySetPrivate(health, "maxHealth", newMax);

            if (TryGetPrivate(health, "currentHealth", out int cur))
                TrySetPrivate(health, "currentHealth", Mathf.Clamp(cur + add, 0, newMax));

            EN("Max Health", $"+{add}");
            UpdateHealthUIImmediate();
        }
    }

    private void Up_HP_Mult()
    {
        if (!health) return;
        float m = UnityEngine.Random.Range(hpMult.x, hpMult.y);

        if (TryGetPrivate(health, "maxHealth", out int maxHP))
        {
            int newMax = Mathf.Max(1, Mathf.RoundToInt(maxHP * m));
            int newCur = 0;
            bool hasCur = TryGetPrivate(health, "currentHealth", out int cur);
            if (hasCur) newCur = Mathf.Clamp(Mathf.RoundToInt(cur * m), 0, newMax);

            TrySetPrivate(health, "maxHealth", newMax);
            if (hasCur) TrySetPrivate(health, "currentHealth", newCur);

            EN("Max Health", $"×{m:F2}");
            UpdateHealthUIImmediate();
        }
    }

    private void Up_Regen_Add()
    {
        if (!health) return;
        float add = UnityEngine.Random.Range(regenAdd.x, regenAdd.y);
        if (TryGetPrivate(health, "regenRate", out float before))
        {
            TrySetPrivate(health, "regenRate", Mathf.Max(0f, before + add));
            EN("Regen", $"+{add:F2}/s");
            UpdateHealthUIImmediate();
        }
    }

    private void Up_Armor_Add()
    {
        if (!health) return;
        if (TryGetPrivate(health, "armor", out float before))
        {
            float add = UnityEngine.Random.Range(armorAdd.x, armorAdd.y);
            TrySetPrivate(health, "armor", Mathf.Max(0f, before + add));
            EN("Armor", $"+{add:F1}");
        }
    }

    // ===== Movement (EnemyChaser) =====
    private void Up_MoveSpeed_Add()
    {
        if (!chaser) return;
        if (TryGetPrivate(chaser, "moveSpeed", out float before))
        {
            float add = UnityEngine.Random.Range(moveSpeedAdd.x, moveSpeedAdd.y);
            TrySetPrivate(chaser, "moveSpeed", Mathf.Max(0f, before + add));
            EN("Move Speed", $"+{add:F1}");
        }
    }

    private void Up_ReachEvent_Tune()
    {
        if (!chaser) return;
        TrySetPrivate(chaser, "repeatEvent", true);
        if (TryGetPrivate(chaser, "resetBuffer", out float buf))
            TrySetPrivate(chaser, "resetBuffer", Mathf.Max(0.05f, buf * 0.7f));
        EN("Reach Event", "More frequent");
    }

    // ===== Global cadence =====
    private void Upgrade_AllWeaponTickSpeed()
    {
        float frac = UnityEngine.Random.Range(atkSpeedFracAll.x, atkSpeedFracAll.y);
        foreach (var t in ticks)
        {
            if (!t) continue;
            if (!TryGetPrivate(t, "interval", out float before)) continue;
            float after = Mathf.Max(0.01f, before * (1f - frac));
            TrySetPrivate(t, "interval", after);
            if (t.isActiveAndEnabled) CallPublicIfExists(t, "ResetAndStart");
        }
        WN("Attack Interval (All)", $"-{frac * 100f:F0}%");
    }

    // ===== Knife (public fields) =====
    private void Up_Knife_Dmg_Flat()
    {
        int add = UnityEngine.Random.Range(knifeDamageFlat.x, knifeDamageFlat.y + 1);
        foreach (var k in knives) if (k) k.damage = Mathf.Max(0, k.damage + add);
        WN("Knife Damage", $"+{add}");
    }

    private void Up_Knife_Dmg_Mult()
    {
        float m = UnityEngine.Random.Range(knifeDamageMult.x, knifeDamageMult.y);
        foreach (var k in knives) if (k) k.damage = Mathf.RoundToInt(k.damage * m);
        WN("Knife Damage", $"×{m:F2}");
    }

    private void Up_Knife_Lifesteal_Add()
    {
        float add = UnityEngine.Random.Range(knifeLifestealAdd.x, knifeLifestealAdd.y);
        foreach (var k in knives) if (k) k.lifestealPercent = Mathf.Clamp01(k.lifestealPercent + add);
        WN("Knife Lifesteal", $"+{add * 100f:F0}%");
    }

    private void Up_Knife_MaxTargets_Add()
    {
        int add = UnityEngine.Random.Range(knifeMaxTargetsAdd.x, knifeMaxTargetsAdd.y + 1);
        foreach (var k in knives) if (k) k.maxTargetsPerTick = Mathf.Max(0, k.maxTargetsPerTick + add);
        WN("Knife Max Targets", $"+{add}");
    }

    private void Up_Knife_CritChance_Add()
    {
        float add = UnityEngine.Random.Range(knifeCritChanceAdd.x, knifeCritChanceAdd.y);
        foreach (var k in knives) if (k) k.critChance = Mathf.Clamp01(k.critChance + add);
        WN("Knife Crit Chance", $"+{add * 100f:F0}%");
    }

    private void Up_Knife_CritMult_Add()
    {
        float add = UnityEngine.Random.Range(knifeCritMultAdd.x, knifeCritMultAdd.y);
        foreach (var k in knives) if (k) k.critMultiplier = Mathf.Max(1f, k.critMultiplier + add);
        WN("Knife Crit Mult", $"+{add:F2}x");
    }

    // ===== Shooter (public fields already) =====
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
        if (!TryGetPrivate(health, "extraTextField", out string cur)) cur = "";
        string cleaned = RemoveRaritySection(cur);
        string combined = string.IsNullOrWhiteSpace(cleaned) ? block : $"{cleaned}\n{block}";
        TrySetPrivate(health, "extraTextField", combined);

        CallPrivateOrPublic(health, "UpdateStatsText");
        UpdateHealthUIImmediate();
    }

    private void UpdateHealthUIImmediate()
    {
        if (!health) return;

        if (TryGetPrivate(health, "maxHealth", out int maxHP))
        {
            int curHP = 0;
            bool hasCur = TryGetPrivate(health, "currentHealth", out int curField);
            if (hasCur) curHP = curField;
            else
            {
                var prop = health.GetType().GetProperty("CurrentHealth", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.PropertyType == typeof(int))
                    curHP = (int)prop.GetValue(health, null);
            }

            // Slider
            var fSlider = health.GetType().GetField("healthSlider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fSlider != null)
            {
                var slider = fSlider.GetValue(health) as UnityEngine.UI.Slider;
                if (slider != null)
                {
                    slider.maxValue = maxHP;
                    slider.value = Mathf.Clamp(curHP, 0, maxHP);
                }
            }

            // Text
            var fText = health.GetType().GetField("healthText", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fText != null)
            {
                var tmp = fText.GetValue(health) as TMPro.TextMeshProUGUI;
                if (tmp != null)
                    tmp.text = $"{curHP}/{maxHP}";
            }
        }
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

    // ===== tiny reflection helpers for enemy/chaser/WT only =====
    private static bool TryGetPrivate<T>(object obj, string fieldName, out T value)
    {
        value = default;
        if (obj == null) return false;
        var f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null || !typeof(T).IsAssignableFrom(f.FieldType)) return false;
        object val = f.GetValue(obj);
        if (val is T cast) { value = cast; return true; }
        return false;
    }

    private static bool TrySetPrivate<T>(object obj, string fieldName, T value)
    {
        if (obj == null) return false;
        var f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null || !f.FieldType.IsAssignableFrom(typeof(T))) return false;
        f.SetValue(obj, value);
        return true;
    }

    private static void CallPublicIfExists(object obj, string method)
    {
        if (obj == null) return;
        var mi = obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public);
        mi?.Invoke(obj, null);
    }

    private void CallPrivateOrPublic(object obj, string method)
    {
        if (obj == null) return;
        var miPriv = obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        var miPub = obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public);
        (miPriv ?? miPub)?.Invoke(obj, null);
    }
}
