using UnityEngine;

public interface IDamageModule { int Damage { get; set; } }
public interface ICritModule { float CritChance { get; set; } float CritMultiplier { get; set; } }
public interface IAttackSpeedModule { float Interval { get; set; } }
public interface IKnifeModule
{
    float LifestealPercent { get; set; }
    float Radius { get; set; }
    float SplashRadius { get; set; }
    int MaxTargetsPerTick { get; set; }
}
public interface IShooterModule
{
    float BulletLifetime { get; set; }
    float ShootForce { get; set; }
    int ProjectileCount { get; set; }
    float SpreadAngle { get; set; }
}
public interface IUITextSink
{
    string Text { get; set; }          // current full text
    void SetText(string s);
}

// ===== Adapters (no reflection) =====
public sealed class KnifeAdapter : IDamageModule, ICritModule, IKnifeModule, IUITextSink
{
    private readonly Knife k;
    public KnifeAdapter(Knife k) { this.k = k; }
    public int Damage { get => k.damage; set => k.damage = value; }
    public float CritChance { get => k.critChance; set => k.critChance = Mathf.Clamp01(value); }
    public float CritMultiplier { get => k.critMultiplier; set => k.critMultiplier = value; }
    public float LifestealPercent { get => k.lifestealPercent; set => k.lifestealPercent = Mathf.Clamp01(value); }
    public float Radius { get => k.radius; set => k.radius = value; }
    public float SplashRadius { get => k.splashRadius; set => k.splashRadius = value; }
    public int MaxTargetsPerTick { get => k.maxTargetsPerTick; set => k.maxTargetsPerTick = value; }
    public string Text { get => k.extraTextField ?? ""; set => k.extraTextField = value; }
    public void SetText(string s) => k.extraTextField = s;
}

public sealed class ShooterAdapter : IDamageModule, ICritModule, IShooterModule, IUITextSink
{
    private readonly SimpleShooter s;
    public ShooterAdapter(SimpleShooter s) { this.s = s; }
    public int Damage { get => s.damage; set => s.damage = value; }
    public float CritChance { get => s.critChance; set => s.critChance = Mathf.Clamp01(value); }
    public float CritMultiplier { get => s.critMultiplier; set => s.critMultiplier = value; }
    public float BulletLifetime { get => s.bulletLifetime; set => s.bulletLifetime = value; }
    public float ShootForce { get => s.shootForce; set => s.shootForce = value; }
    public int ProjectileCount { get => s.projectileCount; set => s.projectileCount = value; }
    public float SpreadAngle { get => s.spreadAngle; set => s.spreadAngle = Mathf.Max(0f, value); }
    public string Text { get => s.extraTextField ?? ""; set => s.extraTextField = value; }
    public void SetText(string t) => s.extraTextField = t;
}

public sealed class TickAdapter : IAttackSpeedModule
{
    private readonly WeaponTick t;
    public TickAdapter(WeaponTick t) { this.t = t; }
    public float Interval { get => t.interval; set => t.interval = value; }
    public void ResetAndStartIfPlaying()
    {
        if (Application.isPlaying) t.ResetAndStart();
    }
}
