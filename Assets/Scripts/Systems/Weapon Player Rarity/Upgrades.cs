using System;
using System.Text;
using UnityEngine;

public interface IUpgrade
{
    bool IsApplicable(WeaponContext ctx);
    /// <summary>Apply and return an undo action.</summary>
    Action Apply(WeaponContext ctx, StringBuilder notes);
}

public sealed class WeaponContext
{
    public System.Random rng;
    public Rarity rarity;
    public TierSystem tiers;
    public UpgradeRanges ranges;

    // adapters present = supported
    public IDamageModule damage;
    public ICritModule crit;
    public IAttackSpeedModule attack;
    public IKnifeModule knife;
    public IShooterModule shooter;
    public IUITextSink ui;                     // sink to write rarity block
    public TickAdapter tickAdapter;            // to reset tick cleanly

    public string Roman(int n)
    {
        n = Mathf.Clamp(n, 1, 10);
        return n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => "X" };
    }

    public static string BlueWrap(string inner) => $"<color=#00AEEF>{inner}</color>";
    public static string FormatRarity(Rarity r) => r switch
    {
        Rarity.Common => "<color=#B0B0B0>Common</color>",
        Rarity.Uncommon => "<color=#3EC46D>Uncommon</color>",
        Rarity.Rare => "<color=#3AA0FF>Rare</color>",
        Rarity.Legendary => "<color=#FFB347>Legendary</color>",
        _ => "Common"
    };
}

// ===== Concrete upgrades =====
public sealed class DamageFlatUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.damage != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.damageFlatAdd, c.tiers.damageFlat, 0);
        int add = UnityEngine.Random.Range(r.x, r.y + 1);
        int before = c.damage.Damage;
        c.damage.Damage = before + add;
        notes.AppendLine($"+{add} Damage (Tier {c.Roman(c.tiers.damageFlat)})");
        return () => c.damage.Damage -= add;
    }
}

public sealed class DamagePercentAsFlatUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.damage != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.ScaleMultiplierLike(c.ranges.damageMult, c.tiers.damagePercent);
        float mult = UnityEngine.Random.Range(r.x, r.y);
        int baseDmg = c.damage.Damage;
        int delta = Mathf.RoundToInt(baseDmg * (mult - 1f));
        c.damage.Damage = baseDmg + delta;
        notes.AppendLine($"+{(mult - 1f) * 100f:F0}% Damage (Tier {c.Roman(c.tiers.damagePercent)})");
        return () => c.damage.Damage -= delta;
    }
}

public sealed class AttackSpeedUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.attack != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.atkSpeedFrac, c.tiers.attackSpeed);
        float frac = Mathf.Clamp01(UnityEngine.Random.Range(r.x, r.y));
        float before = c.attack.Interval;
        float reduceBy = before * frac;
        float newInterval = Mathf.Max(0.05f, before - reduceBy);
        float actuallyReduced = before - newInterval;
        c.attack.Interval = newInterval;
        notes.AppendLine($"+{frac * 100f:F0}% Attack Speed (Tier {c.Roman(c.tiers.attackSpeed)})");
        return () => c.attack.Interval += actuallyReduced;
    }
}

public sealed class CritUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.crit != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        bool chance = UnityEngine.Random.value < 0.6f;
        if (chance)
        {
            var r = c.tiers.Scale(c.ranges.critChanceAdd, c.tiers.critChance);
            float add = Mathf.Clamp01(UnityEngine.Random.Range(r.x, r.y));
            c.crit.CritChance = Mathf.Clamp01(c.crit.CritChance + add);
            notes.AppendLine($"+{add * 100f:F0}% Crit Chance (Tier {c.Roman(c.tiers.critChance)})");
            return () => c.crit.CritChance = Mathf.Clamp01(c.crit.CritChance - add);
        }
        else
        {
            var r = c.tiers.Scale(c.ranges.critMultAdd, c.tiers.critMultiplier);
            float add = UnityEngine.Random.Range(r.x, r.y);
            c.crit.CritMultiplier += add;
            notes.AppendLine($"+{add:F2} Crit Mult (Tier {c.Roman(c.tiers.critMultiplier)})");
            return () => c.crit.CritMultiplier -= add;
        }
    }
}

public sealed class KnifeLifestealUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.knife != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.knifeLifestealAdd, c.tiers.knifeLifesteal);
        float add = Mathf.Clamp01(UnityEngine.Random.Range(r.x, r.y));
        c.knife.LifestealPercent = Mathf.Clamp01(c.knife.LifestealPercent + add);
        notes.AppendLine($"+{add * 100f:F0}% Lifesteal (Tier {c.Roman(c.tiers.knifeLifesteal)})");
        return () => c.knife.LifestealPercent = Mathf.Clamp01(c.knife.LifestealPercent - add);
    }
}

public sealed class KnifeRadiusUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.knife != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.ScaleMultiplierLike(c.ranges.knifeRadiusMult, c.tiers.knifeRadius);
        float mult = UnityEngine.Random.Range(r.x, r.y);
        float before = c.knife.Radius;
        float delta = before * (mult - 1f);
        c.knife.Radius = before + delta;
        notes.AppendLine($"+{(mult - 1f) * 100f:F0}% Range (Tier {c.Roman(c.tiers.knifeRadius)})");
        return () => c.knife.Radius -= delta;
    }
}

public sealed class KnifeSplashUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.knife != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.ScaleMultiplierLike(c.ranges.knifeSplashRadiusMult, c.tiers.knifeSplashRadius);
        float mult = UnityEngine.Random.Range(r.x, r.y);
        float before = c.knife.SplashRadius;
        float delta = before * (mult - 1f);
        c.knife.SplashRadius = before + delta;
        notes.AppendLine($"+{(mult - 1f) * 100f:F0}% AOE (Tier {c.Roman(c.tiers.knifeSplashRadius)})");
        return () => c.knife.SplashRadius -= delta;
    }
}

public sealed class KnifeMaxTargetsUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.knife != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.knifeMaxTargetsAdd, c.tiers.knifeMaxTargets, 1);
        int add = Mathf.Max(1, UnityEngine.Random.Range(r.x, r.y + 1));
        c.knife.MaxTargetsPerTick += add;
        notes.AppendLine($"+{add} Max Targets (Tier {c.Roman(c.tiers.knifeMaxTargets)})");
        return () => c.knife.MaxTargetsPerTick -= add;
    }
}

public sealed class ShooterProjectilesUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.shooter != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.shooterProjectilesAdd, c.tiers.shooterProjectiles, 1);
        int add = Mathf.Max(1, UnityEngine.Random.Range(r.x, r.y + 1));
        c.shooter.ProjectileCount += add;
        notes.AppendLine($"+{add} Projectiles (Tier {c.Roman(c.tiers.shooterProjectiles)})");
        return () => c.shooter.ProjectileCount -= add;
    }
}

public sealed class ShooterRangeUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.shooter != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        bool lifetime = UnityEngine.Random.value < 0.5f;
        if (lifetime)
        {
            var r = c.tiers.Scale(c.ranges.shooterLifetimeAdd, c.tiers.shooterLifetime);
            float add = Mathf.Max(0f, UnityEngine.Random.Range(r.x, r.y));
            c.shooter.BulletLifetime += add;
            notes.AppendLine($"+{add:F1}s Projectile Lifetime (Tier {c.Roman(c.tiers.shooterLifetime)})");
            return () => c.shooter.BulletLifetime -= add;
        }
        else
        {
            var r = c.tiers.Scale(c.ranges.shooterForceAdd, c.tiers.shooterForce);
            float add = Mathf.Max(0f, UnityEngine.Random.Range(r.x, r.y));
            c.shooter.ShootForce += add;
            notes.AppendLine($"+{add:F1} Projectile Speed (Tier {c.Roman(c.tiers.shooterForce)})");
            return () => c.shooter.ShootForce -= add;
        }
    }
}

public sealed class ShooterAccuracyUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.shooter != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.shooterSpreadReduceFrac, c.tiers.shooterAccuracy);
        float frac = Mathf.Clamp01(UnityEngine.Random.Range(r.x, r.y));
        float before = c.shooter.SpreadAngle;
        float delta = before * frac;
        float newSpread = Mathf.Max(0f, before - delta);
        float actuallyReduced = before - newSpread;
        c.shooter.SpreadAngle = newSpread;
        notes.AppendLine($"+{frac * 100f:F0}% Accuracy (Tier {c.Roman(c.tiers.shooterAccuracy)})");
        return () => c.shooter.SpreadAngle += actuallyReduced;
    }
}
