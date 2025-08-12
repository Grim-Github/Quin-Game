
using UnityEngine;

/// <summary>
/// Handles a single weapon upgrade entry and (optionally) wires the *next* upgrade
/// to the next sibling WeaponUpgrades component under the same parent.
/// Also pushes that next upgrade's PowerUp into PowerUpChooser (expects a List&lt;PowerUp&gt;).
/// </summary>
[ExecuteAlways]
public class WeaponUpgrades : MonoBehaviour
{
    public enum UpgradeType
    {
        None,

        // --- Knife ---
        KnifeDamageFlat,
        KnifeDamagePercent,
        KnifeRadiusFlat,
        KnifeRadiusPercent,
        KnifeMaxTargetsFlat,
        KnifeLifestealFlat,
        KnifeLifestealPercent,
        KnifeCritChanceFlat,        // NEW
        KnifeCritMultiplierFlat,    // NEW

        // --- Shooter ---
        ShooterDamageFlat,
        ShooterDamagePercent,
        ShooterProjectileCount,
        ShooterSpreadAngleFlat,
        ShooterSpreadAnglePercent,
        ShooterProjectileSpeedFlat,
        ShooterProjectileSpeedPercent,
        ShooterLifetimeFlat,
        ShooterLifetimePercent,
        ShooterCritChanceFlat,      // NEW
        ShooterCritMultiplierFlat,  // NEW

        // --- WeaponTick ---
        TickRateFlat,
        TickRatePercent,
        BurstCountFlat,
        BurstCountPercent,
        BurstSpacingFlat,
        BurstSpacingPercent
    }

    private PowerUpChooser powerUpChooser;

    [Header("Power-Up")]
    public PowerUp Upgrade;

    [Tooltip("Autofilled: next sibling with WeaponUpgrades in the same parent.")]
    public WeaponUpgrades nextUpgrade;

    [Header("Icon (optional)")]
    [Tooltip("If assigned, this sprite will be written into Upgrade.powerUpIcon.")]
    public Sprite icon; // NEW

    [Header("Upgrade Settings")]
    public UpgradeType upgradeType = UpgradeType.None;

    [Tooltip("Acts as integer for flat amounts (rounded), or as a percent when the upgrade type is % based (e.g., 0.25 = 25%).")]
    public float value = 0f;

    // ---------------------- Lifecycle ----------------------

    private void Awake()
    {
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();

        AutoAssignNextUpgrade();      // <- make sure nextUpgrade is always the next sibling with WeaponUpgrades
        EnqueueNextUpgradeOnce();     // <- push next Upgrade asset into chooser (expects List<PowerUp>)

        // Ensure icon if provided
        if (Upgrade != null && icon != null)
            Upgrade.powerUpIcon = icon;

        // Normalize type if not allowed for the current parent
        if (!IsTypeAllowedForParent(upgradeType))
            upgradeType = UpgradeType.None;

        SetUpgradeInfo();
        ApplyUpgrade();
    }

    private void OnEnable()
    {
        // Editor recompile / domain reload safety
        AutoAssignNextUpgrade();
        EnqueueNextUpgradeOnce();
    }

    private void OnValidate()
    {
        // Keep next wired live in the editor
        AutoAssignNextUpgrade();

        // Keep name/description (and icon) up to date while editing
        if (!IsTypeAllowedForParent(upgradeType))
            upgradeType = UpgradeType.None;

        if (Upgrade != null && icon != null)
            Upgrade.powerUpIcon = icon;

        SetUpgradeInfo();
    }

    private void OnTransformParentChanged()
    {
        AutoAssignNextUpgrade();
    }

    private void OnTransformChildrenChanged()
    {
        AutoAssignNextUpgrade();
    }

    // ---------------------- Auto-wire NEXT ----------------------

    /// <summary>
    /// Automatically sets nextUpgrade to the next sibling under the same parent
    /// that has a WeaponUpgrades component. If immediate next child doesn't have it,
    /// scans forward until it finds one. Clears if none found.
    /// </summary>
    private void AutoAssignNextUpgrade()
    {
        var old = nextUpgrade;
        nextUpgrade = null;

        if (transform.parent == null) return;

        int myIndex = transform.GetSiblingIndex();
        var parent = transform.parent;

        for (int i = myIndex + 1; i < parent.childCount; i++)
        {
            var candidate = parent.GetChild(i).GetComponent<WeaponUpgrades>();
            if (candidate != null)
            {
                nextUpgrade = candidate;
                break;
            }
        }

#if UNITY_EDITOR
        if (old != nextUpgrade)
        {
            // Mark dirty so the inspector shows the refreshed reference
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// Adds the nextUpgrade's PowerUp asset to the chooser list once.
    /// Requires PowerUpChooser.powerUps to be a List&lt;PowerUp&gt;.
    /// Safely avoids duplicates and nulls.
    /// </summary>
    private void EnqueueNextUpgradeOnce()
    {
        if (powerUpChooser == null) return;
        if (nextUpgrade == null || nextUpgrade.Upgrade == null) return;

        // Use reflection-free guard if powerUps is a List<PowerUp>
        try
        {
            var list = powerUpChooser.powerUps;
            if (list != null && !list.Contains(nextUpgrade.Upgrade))
            {
                list.Add(nextUpgrade.Upgrade);
            }
        }
        catch (System.Exception)
        {
            // If user's PowerUpChooser isn't List-backed, ignore silently.
        }
    }

    // ---------------------- Helpers ----------------------

    private bool HasParent<T>() where T : Component
    {
        if (transform.parent == null) return false;
        return transform.parent.GetComponent<T>() != null;
    }

    private bool IsTypeAllowedForParent(UpgradeType type)
    {
        // None always allowed
        if (type == UpgradeType.None) return true;

        bool hasKnife = HasParent<Knife>();
        bool hasShooter = HasParent<SimpleShooter>();
        bool hasTick = HasParent<WeaponTick>();

        switch (type)
        {
            // Knife-only
            case UpgradeType.KnifeDamageFlat:
            case UpgradeType.KnifeDamagePercent:
            case UpgradeType.KnifeRadiusFlat:
            case UpgradeType.KnifeRadiusPercent:
            case UpgradeType.KnifeMaxTargetsFlat:
            case UpgradeType.KnifeLifestealFlat:
            case UpgradeType.KnifeLifestealPercent:
            case UpgradeType.KnifeCritChanceFlat:
            case UpgradeType.KnifeCritMultiplierFlat:
                return hasKnife;

            // Shooter-only
            case UpgradeType.ShooterDamageFlat:
            case UpgradeType.ShooterDamagePercent:
            case UpgradeType.ShooterProjectileCount:
            case UpgradeType.ShooterSpreadAngleFlat:
            case UpgradeType.ShooterSpreadAnglePercent:
            case UpgradeType.ShooterProjectileSpeedFlat:
            case UpgradeType.ShooterProjectileSpeedPercent:
            case UpgradeType.ShooterLifetimeFlat:
            case UpgradeType.ShooterLifetimePercent:
            case UpgradeType.ShooterCritChanceFlat:
            case UpgradeType.ShooterCritMultiplierFlat:
                return hasShooter;

            // Tick-only
            case UpgradeType.TickRateFlat:
            case UpgradeType.TickRatePercent:
            case UpgradeType.BurstCountFlat:
            case UpgradeType.BurstCountPercent:
            case UpgradeType.BurstSpacingFlat:
            case UpgradeType.BurstSpacingPercent:
                return hasTick;

            default:
                return false;
        }
    }

    private void SetUpgradeInfo()
    {
        if (Upgrade == null) return;


        // Auto-set icon from parent's weapon sprite if available
        if (transform.parent != null)
        {
            // Check SimpleShooter
            if (transform.parent.TryGetComponent(out SimpleShooter shooter))
            {
                var shooterSprite = shooter.GetType()
                    .GetField("weaponSprite", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                    ?.GetValue(shooter) as Sprite;
                if (shooterSprite != null)
                {
                    icon = shooterSprite;
                    Upgrade.powerUpIcon = shooterSprite;
                }
            }
            // Check Knife
            else if (transform.parent.TryGetComponent(out Knife Knife))
            {
                var KnifeSprite = Knife.GetType()
                    .GetField("weaponSprite", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                    ?.GetValue(Knife) as Sprite;
                if (KnifeSprite != null)
                {
                    icon = KnifeSprite;
                    Upgrade.powerUpIcon = KnifeSprite;
                }
            }
        }


        switch (upgradeType)
        {
            // ------------- Knife -------------
            case UpgradeType.KnifeDamageFlat:
                Upgrade.powerUpName = $"Knife Damage +{Mathf.RoundToInt(value)}";
                Upgrade.powerUpDescription = $"Increase Knife damage by {Mathf.RoundToInt(value)}.";
                break;
            case UpgradeType.KnifeDamagePercent:
                Upgrade.powerUpName = $"Knife Damage +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase Knife damage by {value * 100f:F0}%.";
                break;
            case UpgradeType.KnifeRadiusFlat:
                Upgrade.powerUpName = $"Knife Range +{value:F2}";
                Upgrade.powerUpDescription = $"Increase Knife attack radius by {value:F2} units.";
                break;
            case UpgradeType.KnifeRadiusPercent:
                Upgrade.powerUpName = $"Knife Range +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase Knife attack radius by {value * 100f:F0}%.";
                break;
            case UpgradeType.KnifeMaxTargetsFlat:
                Upgrade.powerUpName = $"Multi-Target +{Mathf.RoundToInt(value)}";
                Upgrade.powerUpDescription = $"Knife can hit {Mathf.RoundToInt(value)} additional target(s) per tick.";
                break;
            case UpgradeType.KnifeLifestealFlat:
                Upgrade.powerUpName = $"Knife Lifesteal +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Adds {value * 100f:F0}% lifesteal to Knife attacks.";
                break;
            case UpgradeType.KnifeLifestealPercent:
                Upgrade.powerUpName = $"Lifesteal Boost +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase current Knife lifesteal by {value * 100f:F0}%.";
                break;
            case UpgradeType.KnifeCritChanceFlat: // NEW
                Upgrade.powerUpName = $"Knife Crit Chance +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase Knife critical hit chance by {value * 100f:F0}%.";
                break;
            case UpgradeType.KnifeCritMultiplierFlat: // NEW
                Upgrade.powerUpName = $"Knife Crit Multiplier +{value:F2}x";
                Upgrade.powerUpDescription = $"Increase Knife critical damage multiplier by {value:F2}x.";
                break;

            // ------------- SHOOTER -------------
            case UpgradeType.ShooterDamageFlat:
                Upgrade.powerUpName = $"Shooter Damage +{Mathf.RoundToInt(value)}";
                Upgrade.powerUpDescription = $"Increase projectile damage by {Mathf.RoundToInt(value)}.";
                break;
            case UpgradeType.ShooterDamagePercent:
                Upgrade.powerUpName = $"Shooter Damage +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase projectile damage by {value * 100f:F0}%.";
                break;
            case UpgradeType.ShooterProjectileCount:
                Upgrade.powerUpName = $"Extra Projectiles +{Mathf.RoundToInt(value)}";
                Upgrade.powerUpDescription = $"Fire {Mathf.RoundToInt(value)} more projectile(s) per shot.";
                break;
            case UpgradeType.ShooterSpreadAngleFlat:
                Upgrade.powerUpName = $"Spread +{value:F1}°";
                Upgrade.powerUpDescription = $"Increase spread by {value:F1} degrees.";
                break;
            case UpgradeType.ShooterSpreadAnglePercent:
                Upgrade.powerUpName = $"Spread +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase spread by {value * 100f:F0}%.";
                break;
            case UpgradeType.ShooterProjectileSpeedFlat:
                Upgrade.powerUpName = $"Bullet Speed +{value:F1}";
                Upgrade.powerUpDescription = $"Increase projectile speed by {value:F1}.";
                break;
            case UpgradeType.ShooterProjectileSpeedPercent:
                Upgrade.powerUpName = $"Bullet Speed +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase projectile speed by {value * 100f:F0}%.";
                break;
            case UpgradeType.ShooterLifetimeFlat:
                Upgrade.powerUpName = $"Bullet Lifetime +{value:F1}s";
                Upgrade.powerUpDescription = $"Increase projectile lifetime by {value:F1} seconds.";
                break;
            case UpgradeType.ShooterLifetimePercent:
                Upgrade.powerUpName = $"Bullet Lifetime +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase projectile lifetime by {value * 100f:F0}%.";
                break;
            case UpgradeType.ShooterCritChanceFlat: // NEW
                Upgrade.powerUpName = $"Shooter Crit Chance +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase Shooter critical hit chance by {value * 100f:F0}%.";
                break;
            case UpgradeType.ShooterCritMultiplierFlat: // NEW
                Upgrade.powerUpName = $"Shooter Crit Multiplier +{value:F2}x";
                Upgrade.powerUpDescription = $"Increase Shooter critical damage multiplier by {value:F2}x.";
                break;

            // ------------- TICK -------------
            case UpgradeType.TickRateFlat:
                Upgrade.powerUpName = $"Attack Speed −{value:F2}s";
                Upgrade.powerUpDescription = $"Reduce interval between attacks by {value:F2} seconds.";
                break;
            case UpgradeType.TickRatePercent:
                Upgrade.powerUpName = $"Attack Speed +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Reduce attack interval by {value * 100f:F0}%.";
                break;
            case UpgradeType.BurstCountFlat:
                Upgrade.powerUpName = $"Burst +{Mathf.RoundToInt(value)}";
                Upgrade.powerUpDescription = $"Increase shots per burst by {Mathf.RoundToInt(value)}.";
                break;
            case UpgradeType.BurstCountPercent:
                Upgrade.powerUpName = $"Burst +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase burst shot count by {value * 100f:F0}%.";
                break;
            case UpgradeType.BurstSpacingFlat:
                Upgrade.powerUpName = $"Burst Spacing −{value:F2}s";
                Upgrade.powerUpDescription = $"Reduce delay between burst shots by {value:F2} seconds.";
                break;
            case UpgradeType.BurstSpacingPercent:
                Upgrade.powerUpName = $"Burst Spacing −{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Reduce delay between burst shots by {value * 100f:F0}%.";
                break;

            default:
                Upgrade.powerUpName = "No Upgrade";
                Upgrade.powerUpDescription = "This upgrade slot is empty.";
                break;
        }

        // Append parent transform name to the upgrade title
        if (!string.IsNullOrEmpty(Upgrade.powerUpName) && transform.parent != null)
        {
            Upgrade.powerUpName = $"{transform.parent.name} - {Upgrade.powerUpName}";
        }

    }

    public void ApplyUpgrade()
    {
        // ---------------- Knife ----------------
        switch (upgradeType)
        {
            case UpgradeType.KnifeDamageFlat:
                if (transform.parent.TryGetComponent(out Knife k1))
                    k1.damage += Mathf.RoundToInt(value);
                break;

            case UpgradeType.KnifeDamagePercent:
                if (transform.parent.TryGetComponent(out Knife k2))
                    k2.damage = Mathf.RoundToInt(k2.damage * (1f + value));
                break;

            case UpgradeType.KnifeRadiusFlat:
                if (transform.parent.TryGetComponent(out Knife k3))
                {
                    var f = typeof(Knife).GetField("radius", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(k3, (float)f.GetValue(k3) + value);
                }
                break;

            case UpgradeType.KnifeRadiusPercent:
                if (transform.parent.TryGetComponent(out Knife k4))
                {
                    var f = typeof(Knife).GetField("radius", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(k4, (float)f.GetValue(k4) * (1f + value));
                }
                break;

            case UpgradeType.KnifeMaxTargetsFlat:
                if (transform.parent.TryGetComponent(out Knife k5))
                {
                    var f = typeof(Knife).GetField("maxTargetsPerTick", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(k5, (int)f.GetValue(k5) + Mathf.RoundToInt(value));
                }
                break;

            case UpgradeType.KnifeLifestealFlat:
                if (transform.parent.TryGetComponent(out Knife k6))
                {
                    var f = typeof(Knife).GetField("lifestealPercent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(k6, (float)f.GetValue(k6) + value);
                }
                break;

            case UpgradeType.KnifeLifestealPercent:
                if (transform.parent.TryGetComponent(out Knife k7))
                {
                    var f = typeof(Knife).GetField("lifestealPercent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(k7, (float)f.GetValue(k7) * (1f + value));
                }
                break;

            case UpgradeType.KnifeCritChanceFlat: // NEW
                if (transform.parent.TryGetComponent(out Knife k8))
                {
                    var f = typeof(Knife).GetField("critChance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(k8, (float)f.GetValue(k8) + value);
                }
                break;

            case UpgradeType.KnifeCritMultiplierFlat: // NEW
                if (transform.parent.TryGetComponent(out Knife k9))
                {
                    var f = typeof(Knife).GetField("critMultiplier", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(k9, (float)f.GetValue(k9) + value);
                }
                break;
        }

        // ---------------- SHOOTER ----------------
        switch (upgradeType)
        {
            case UpgradeType.ShooterDamageFlat:
                if (transform.parent.TryGetComponent(out SimpleShooter s1))
                    s1.damage += Mathf.RoundToInt(value);
                break;

            case UpgradeType.ShooterDamagePercent:
                if (transform.parent.TryGetComponent(out SimpleShooter s2))
                    s2.damage = Mathf.RoundToInt(s2.damage * (1f + value));
                break;

            case UpgradeType.ShooterProjectileCount:
                if (transform.parent.TryGetComponent(out SimpleShooter s3))
                    s3.projectileCount += Mathf.RoundToInt(value);
                break;

            case UpgradeType.ShooterSpreadAngleFlat:
                if (transform.parent.TryGetComponent(out SimpleShooter s4))
                    s4.spreadAngle += value;
                break;

            case UpgradeType.ShooterSpreadAnglePercent:
                if (transform.parent.TryGetComponent(out SimpleShooter s5))
                    s5.spreadAngle *= (1f + value);
                break;

            case UpgradeType.ShooterProjectileSpeedFlat:
                if (transform.parent.TryGetComponent(out SimpleShooter s6))
                    s6.shootForce += value;
                break;

            case UpgradeType.ShooterProjectileSpeedPercent:
                if (transform.parent.TryGetComponent(out SimpleShooter s7))
                    s7.shootForce *= (1f + value);
                break;

            case UpgradeType.ShooterLifetimeFlat:
                if (transform.parent.TryGetComponent(out SimpleShooter s8))
                    s8.bulletLifetime += value;
                break;

            case UpgradeType.ShooterLifetimePercent:
                if (transform.parent.TryGetComponent(out SimpleShooter s9))
                    s9.bulletLifetime *= (1f + value);
                break;

            case UpgradeType.ShooterCritChanceFlat: // NEW
                if (transform.parent.TryGetComponent(out SimpleShooter s10))
                {
                    var f = typeof(SimpleShooter).GetField("critChance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(s10, (float)f.GetValue(s10) + value);
                }
                break;

            case UpgradeType.ShooterCritMultiplierFlat: // NEW
                if (transform.parent.TryGetComponent(out SimpleShooter s11))
                {
                    var f = typeof(SimpleShooter).GetField("critMultiplier", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(s11, (float)f.GetValue(s11) + value);
                }
                break;
        }

        // ---------------- TICK ----------------
        switch (upgradeType)
        {
            case UpgradeType.TickRateFlat:
                if (transform.parent.TryGetComponent(out WeaponTick t1))
                    t1.interval = Mathf.Max(0.05f, t1.interval - value);
                break;

            case UpgradeType.TickRatePercent:
                if (transform.parent.TryGetComponent(out WeaponTick t2))
                    t2.interval = Mathf.Max(0.05f, t2.interval * (1f - value));
                break;

            case UpgradeType.BurstCountFlat:
                if (transform.parent.TryGetComponent(out WeaponTick t3))
                {
                    var f = typeof(WeaponTick).GetField("burstCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(t3, (int)f.GetValue(t3) + Mathf.RoundToInt(value));
                }
                break;

            case UpgradeType.BurstCountPercent:
                if (transform.parent.TryGetComponent(out WeaponTick t4))
                {
                    var f = typeof(WeaponTick).GetField("burstCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(t4, Mathf.RoundToInt((int)f.GetValue(t4) * (1f + value)));
                }
                break;

            case UpgradeType.BurstSpacingFlat:
                if (transform.parent.TryGetComponent(out WeaponTick t5))
                {
                    var f = typeof(WeaponTick).GetField("burstSpacing", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(t5, Mathf.Max(0.01f, (float)f.GetValue(t5) - value));
                }
                break;

            case UpgradeType.BurstSpacingPercent:
                if (transform.parent.TryGetComponent(out WeaponTick t6))
                {
                    var f = typeof(WeaponTick).GetField("burstSpacing", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(t6, Mathf.Max(0.01f, (float)f.GetValue(t6) * (1f - value)));
                }
                break;
        }
    }

#if UNITY_EDITOR
    // ---------- Custom Inspector to Filter Enum + show read-only next ----------
    [UnityEditor.CustomEditor(typeof(WeaponUpgrades))]
    private class WeaponUpgradesEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var wu = (WeaponUpgrades)target;
            var so = serializedObject;

            so.Update();

            // Draw PowerUp first
            UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("Upgrade"));

            // Read-only nextUpgrade display with a refresh button
            using (new UnityEditor.EditorGUI.DisabledScope(true))
            {
                UnityEditor.EditorGUILayout.ObjectField("Next Upgrade (auto)", wu.nextUpgrade, typeof(WeaponUpgrades), true);
            }
            if (UnityEngine.GUILayout.Button("Refresh Next"))
            {
                wu.AutoAssignNextUpgrade();
                UnityEditor.EditorUtility.SetDirty(wu);
            }

            UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("icon")); // NEW

            // Filter choices based on parent components
            var all = (WeaponUpgrades.UpgradeType[])System.Enum.GetValues(typeof(WeaponUpgrades.UpgradeType));
            System.Collections.Generic.List<WeaponUpgrades.UpgradeType> allowed = new();

            foreach (var t in all)
            {
                if (wu.IsTypeAllowedForParent(t))
                    allowed.Add(t);
            }

            if (allowed.Count == 0)
            {
                UnityEditor.EditorGUILayout.HelpBox("No valid upgrades for this parent. Add Knife, SimpleShooter, or WeaponTick to the parent.", UnityEditor.MessageType.Warning);
            }

            // Current selection index within allowed list
            int currentIndex = Mathf.Max(0, allowed.IndexOf(wu.upgradeType));
            if (currentIndex < 0) currentIndex = 0;

            // Display names
            string[] names = new string[allowed.Count];
            for (int i = 0; i < allowed.Count; i++)
                names[i] = allowed[i].ToString();

            // Draw popup
            int newIndex = (allowed.Count > 0)
                ? UnityEditor.EditorGUILayout.Popup("Upgrade Type", currentIndex, names)
                : 0;

            // Apply selection
            if (allowed.Count > 0)
            {
                var newType = allowed[newIndex];
                if (newType != wu.upgradeType)
                {
                    UnityEditor.Undo.RecordObject(wu, "Change Upgrade Type");
                    wu.upgradeType = newType;
                    wu.SetUpgradeInfo();
                    UnityEditor.EditorUtility.SetDirty(wu);
                }
            }

            // Value field
            var valueProp = so.FindProperty("value");
            UnityEditor.EditorGUILayout.PropertyField(valueProp);

            // Warn if invalid (shouldn’t happen thanks to filtering, but still safe)
            if (!wu.IsTypeAllowedForParent(wu.upgradeType))
            {
                UnityEditor.EditorGUILayout.HelpBox("Selected UpgradeType is not valid for this parent. It will be treated as 'None'.", UnityEditor.MessageType.Warning);
            }

            so.ApplyModifiedProperties();

            // Show live preview of title/description if possible
            if (wu.Upgrade != null)
            {
                UnityEditor.EditorGUILayout.Space();
                UnityEditor.EditorGUILayout.LabelField("Preview", UnityEditor.EditorStyles.boldLabel);
                UnityEditor.EditorGUILayout.LabelField("Title", wu.Upgrade.powerUpName);
                UnityEditor.EditorGUILayout.LabelField("Description", wu.Upgrade.powerUpDescription, UnityEditor.EditorStyles.wordWrappedLabel);
            }
        }
    }
#endif
}
