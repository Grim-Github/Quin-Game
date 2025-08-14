using UnityEngine;

[System.Serializable]
public class TierSystem
{
    [Header("Tier (1 = strongest, 10 = weakest)")]
    [Range(1, 10)] public int damagePercent = 5;
    [Range(1, 10)] public int damageFlat = 5;
    [Range(1, 10)] public int attackSpeed = 5;
    [Range(1, 10)] public int critChance = 5;
    [Range(1, 10)] public int critMultiplier = 5;
    [Range(1, 10)] public int statusDuration = 5;


    [Header("Knife")]
    [Range(1, 10)] public int knifeRadius = 5;
    [Range(1, 10)] public int knifeSplashRadius = 5;
    [Range(1, 10)] public int knifeLifesteal = 5;
    [Range(1, 10)] public int knifeMaxTargets = 5;

    [Header("Shooter")]
    [Range(1, 10)] public int shooterLifetime = 5;
    [Range(1, 10)] public int shooterForce = 5;
    [Range(1, 10)] public int shooterProjectiles = 5;
    [Range(1, 10)] public int shooterAccuracy = 5;

    [Header("Curve (optional)")]
    public AnimationCurve multiplierCurve; // X: 0=Tier10..1=Tier1, Y: mult
    public bool useCurve = false;
    public Vector2 defaultMinMax = new Vector2(0.5f, 2.0f);

    public void RollAll(System.Random rng)
    {
        damagePercent = Roll(rng);
        damageFlat = Roll(rng);
        attackSpeed = Roll(rng);
        critChance = Roll(rng);
        critMultiplier = Roll(rng);
        statusDuration = Roll(rng);

        knifeRadius = Roll(rng);
        knifeSplashRadius = Roll(rng);
        knifeLifesteal = Roll(rng);
        knifeMaxTargets = Roll(rng);

        shooterLifetime = Roll(rng);
        shooterForce = Roll(rng);
        shooterProjectiles = Roll(rng);
        shooterAccuracy = Roll(rng);
    }

    public float Mult(int tier)
    {
        tier = Mathf.Clamp(tier, 1, 10);
        if (useCurve && multiplierCurve != null && multiplierCurve.length > 0)
        {
            float x = (10 - tier) / 9f; // 0..1 (10->0, 1->1)
            return Mathf.Max(0f, multiplierCurve.Evaluate(x));
        }
        float t = (10 - tier) / 9f;
        return Mathf.Lerp(Mathf.Max(0f, defaultMinMax.x), Mathf.Max(0f, defaultMinMax.y), t);
    }

    public Vector2 Scale(Vector2 baseRange, int tier)
    {
        float m = Mult(tier);
        return new Vector2(baseRange.x * m, baseRange.y * m);
    }

    public Vector2Int Scale(Vector2Int baseRange, int tier, int minClamp = int.MinValue)
    {
        float m = Mult(tier);
        int x = Mathf.RoundToInt(baseRange.x * m);
        int y = Mathf.RoundToInt(baseRange.y * m);
        if (x > y) (x, y) = (y, x);
        x = Mathf.Max(minClamp, x); y = Mathf.Max(minClamp, y);
        return new Vector2Int(x, y);
    }

    public Vector2 ScaleMultiplierLike(Vector2 baseRange, int tier)
    {
        float m = Mult(tier);
        float a = 1f + (baseRange.x - 1f) * m;
        float b = 1f + (baseRange.y - 1f) * m;
        if (a > b) (a, b) = (b, a);
        return new Vector2(a, b);
    }

    static int Roll(System.Random rng) => rng.Next(1, 11);
}
