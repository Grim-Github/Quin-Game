using System;
using System.Reflection;
using TMPro;
using UnityEngine;

public class ChatterStats : MonoBehaviour
{
    [Header("UI (optional)")]
    public TextMeshProUGUI nameGUI;

    [Header("Chatter Power")]
    [Tooltip("Higher power makes the monster's rolled stats and rarity stronger.")]
    public int power = 0;

    [Header("Scaling Controls")]
    [Tooltip("How much we bias rarity towards higher tiers per power point. 0.02 = +2% weight scaling step.")]
    [Min(0f)] public float rarityBiasPerPower = 0.02f;

    [Tooltip("Multiplies stat roll ranges per power point. 0.015 = +1.5% per power.")]
    [Min(0f)] public float statRangeMultPerPower = 0.015f;

    [Tooltip("Extra scaling applied specifically to global attack cadence (WeaponTick).")]
    [Min(0f)] public float cadenceBoostPerPower = 0.01f;

    [Tooltip("Hard cap for the total multiplier applied to ranges (1 = no growth).")]
    [Min(1f)] public float maxRangeMultiplier = 3.0f;

    [Tooltip("Hard cap for rarity bias factor (1 = no bias).")]
    [Min(1f)] public float maxRarityBiasFactor = 4.0f;

    private void Start()
    {
        // NOTE: Removed any previous health manipulation here as requested. :contentReference[oaicite:2]{index=2}

        var mr = GetComponent<MonsterRarity>();
        if (mr != null)
            ApplyPowerScaling(mr);
    }

    private void ApplyPowerScaling(object mr)
    {
        int p = Mathf.Max(0, power);

        // ===== 1) Rarity bias (favor higher rarities as power grows) =====
        float bias = 1f + p * rarityBiasPerPower;
        bias = Mathf.Min(bias, maxRarityBiasFactor);

        // Pull existing weights
        float wCom = GetPrivate<float>(mr, "weightCommon", 60f);
        float wUnc = GetPrivate<float>(mr, "weightUncommon", 25f);
        float wRare = GetPrivate<float>(mr, "weightRare", 12f);
        float wLeg = GetPrivate<float>(mr, "weightLegendary", 3f);

        // Shift distribution: reduce Common, increase Uncommon/Rare/Legendary
        float comMul = 1f / bias;
        float uncMul = Mathf.Lerp(1f, bias, 0.33f);
        float rareMul = Mathf.Lerp(1f, bias, 0.66f);
        float legMul = bias;

        wCom *= comMul;
        wUnc *= uncMul;
        wRare *= rareMul;
        wLeg *= legMul;

        TrySetPrivate(mr, "weightCommon", wCom);
        TrySetPrivate(mr, "weightUncommon", wUnc);
        TrySetPrivate(mr, "weightRare", wRare);
        TrySetPrivate(mr, "weightLegendary", wLeg);

        // ===== 2) Range scaling (make all roll ranges stronger with power) =====
        float rangeMul = 1f + p * statRangeMultPerPower;
        rangeMul = Mathf.Clamp(rangeMul, 1f, maxRangeMultiplier);

        // Helper local funcs
        static Vector2Int ScaleV2I(Vector2Int v, float mul)
        {
            int a = Mathf.RoundToInt(v.x * mul);
            int b = Mathf.RoundToInt(v.y * mul);
            if (a > b) (a, b) = (b, a);
            return new Vector2Int(Mathf.Max(0, a), Mathf.Max(0, b));
        }

        static Vector2 ScaleV2(Vector2 v, float mul)
        {
            float a = v.x * mul;
            float b = v.y * mul;
            if (a > b) (a, b) = (b, a);
            return new Vector2(a, b);
        }

        // --- Enemy rolls (we scale ranges only; MonsterRarity will apply them) :contentReference[oaicite:3]{index=3}
        TryScaleV2I(mr, "hpFlatAdd", rangeMul, ScaleV2I);
        TryScaleV2(mr, "hpMult", rangeMul, ScaleV2);
        TryScaleV2(mr, "regenAdd", rangeMul, ScaleV2);
        TryScaleV2(mr, "armorAdd", rangeMul, ScaleV2);

        // --- Movement
        TryScaleV2(mr, "moveSpeedAdd", rangeMul, ScaleV2);

        // --- Global cadence (give it extra oomph if desired)
        float cadenceMul = Mathf.Clamp(rangeMul * (1f + p * cadenceBoostPerPower), 1f, maxRangeMultiplier);
        TryScaleV2(mr, "atkSpeedFracAll", cadenceMul, ScaleV2);

        // --- Knife (note: maxTargets was removed in MonsterRarity)
        TryScaleV2I(mr, "KnifeDamageFlat", rangeMul, ScaleV2I);
        TryScaleV2(mr, "KnifeDamageMult", rangeMul, ScaleV2);
        TryScaleV2(mr, "KnifeLifestealAdd", rangeMul, ScaleV2);
        // removed: TryScaleV2I for "KnifeMaxTargetsAdd" (field no longer exists) :contentReference[oaicite:4]{index=4}
        TryScaleV2(mr, "KnifeCritChanceAdd", rangeMul, ScaleV2);
        TryScaleV2(mr, "KnifeCritMultAdd", rangeMul, ScaleV2);

        // --- Shooter
        TryScaleV2I(mr, "shooterDamageFlat", rangeMul, ScaleV2I);
        TryScaleV2(mr, "shooterDamageMult", rangeMul, ScaleV2);
        TryScaleV2I(mr, "shooterProjectilesAdd", rangeMul, ScaleV2I);
    }

    // =========================
    // Reflection helper methods
    // =========================
    private static T GetPrivate<T>(object obj, string field, T fallback = default)
    {
        if (obj == null) return fallback;
        var f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null) return fallback;
        object val = f.GetValue(obj);
        if (val is T t) return t;
        try { return (T)Convert.ChangeType(val, typeof(T)); } catch { return fallback; }
    }

    private static bool TrySetPrivate<T>(object obj, string field, T value)
    {
        if (obj == null) return false;
        var f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null || !f.FieldType.IsAssignableFrom(typeof(T))) return false;
        f.SetValue(obj, value);
        return true;
    }

    private static void TryScaleV2(object obj, string field, float mul, Func<Vector2, float, Vector2> scaler)
    {
        var f = obj?.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null || f.FieldType != typeof(Vector2)) return;
        Vector2 current = (Vector2)f.GetValue(obj);
        f.SetValue(obj, scaler(current, mul));
    }

    private static void TryScaleV2I(object obj, string field, float mul, Func<Vector2Int, float, Vector2Int> scaler)
    {
        var f = obj?.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null || f.FieldType != typeof(Vector2Int)) return;
        Vector2Int current = (Vector2Int)f.GetValue(obj);
        f.SetValue(obj, scaler(current, mul));
    }
}
