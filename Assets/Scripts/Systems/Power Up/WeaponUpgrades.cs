using UnityEngine;
using UnityEngine.Events; // <-- ADD THIS
//
// Handles a single weapon upgrade entry and (optionally) wires the *next* upgrade
// to the next sibling WeaponUpgrades component under the same parent.
// Also pushes that next upgrade's PowerUp into PowerUpChooser (expects a List<PowerUp>).
//
public class WeaponUpgrades : MonoBehaviour
{
    public enum UpgradeType
    {
        None,

        // --- Knife ---
        KnifeDamageFlat,
        KnifeDamagePercent,
        KnifeDamageTypeIndex,
        KnifeRadiusFlat,
        KnifeRadiusPercent,
        KnifeMaxTargetsFlat,
        KnifeLifestealFlat,
        KnifeLifestealPercent,
        KnifeCritChanceFlat,
        KnifeCritMultiplierFlat,
        KnifeSplashRadiusFlat,
        KnifeSplashRadiusPercent,
        KnifeSplashDamagePercentFlat,
        KnifeSplashDamagePercentPercent,
        KnifeStatusApplyChanceFlat,
        KnifeStatusApplyChancePercent,
        KnifeStatusDurationFlat,
        KnifeStatusDurationPercent,
        KnifeEnableStatusEffect,
        KnifeStatusEffectIndex,

        // --- Shooter ---
        ShooterDamageFlat,
        ShooterDamagePercent,
        ShooterDamageTypeIndex,
        ShooterProjectileCount,
        ShooterSpreadAngleFlat,
        ShooterSpreadAnglePercent,
        ShooterProjectileSpeedFlat,
        ShooterProjectileSpeedPercent,
        ShooterLifetimeFlat,
        ShooterLifetimePercent,
        ShooterCritChanceFlat,
        ShooterCritMultiplierFlat,
        ShooterStatusApplyChanceFlat,
        ShooterStatusApplyChancePercent,
        ShooterStatusDurationFlat,
        ShooterStatusDurationPercent,
        ShooterEnableStatusEffect,
        ShooterStatusEffectIndex,

        // --- WeaponTick ---
        TickRateFlat,
        TickRatePercent,
        BurstCountFlat,
        BurstCountPercent,
        BurstSpacingFlat,
        BurstSpacingPercent,

        Custom
    }

    private PowerUpChooser powerUpChooser;

    [Header("Power-Up")]
    public PowerUp Upgrade;

    [Tooltip("Autofilled: next sibling with WeaponUpgrades in the same parent.")]
    public WeaponUpgrades nextUpgrade;

    // Icon now auto-inferred from parent weapon (Knife/SimpleShooter).

    [Header("Upgrade Settings")]
    public UpgradeType upgradeType = UpgradeType.None;

    [Header("Custom Hooks")]
    [Tooltip("Invoked in Awake if UpgradeType.Custom is selected.")]
    public UnityEvent onCustomAwake; // <-- ADD THIS


    [Tooltip("Acts as integer for flat amounts (rounded), or as a percent when the upgrade type is % based (e.g., 0.25 = 25%).")]
    public float value = 0f;

    // ---------------------- Lifecycle ----------------------

    private void Awake()
    {
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();

        AutoAssignNextUpgrade();      // make sure nextUpgrade is always the next sibling with WeaponUpgrades
        EnqueueNextUpgradeOnce();     // push next Upgrade asset into chooser (expects List<PowerUp>)

        // Ensure icon: inherit from parent weapon if available
        TryAssignIconFromParent();

        // Normalize type if not allowed for the current parent
        if (!IsTypeAllowedForParent(upgradeType))
            upgradeType = UpgradeType.None;

        SetUpgradeInfo();

        // --- ADD THIS: fire the custom event on Awake if Custom is selected ---
        if (upgradeType == UpgradeType.Custom)
        {
            onCustomAwake?.Invoke();
        }
        // ---------------------------------------------------------------------

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

        TryAssignIconFromParent();

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
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// Adds the nextUpgrade's PowerUp asset to the chooser list once.
    /// Requires PowerUpChooser.powerUps to be a List<PowerUp>.
    /// Safely avoids duplicates and nulls.
    /// </summary>
    private void EnqueueNextUpgradeOnce()
    {
        if (powerUpChooser == null) return;
        if (nextUpgrade == null || nextUpgrade.Upgrade == null) return;

        var list = powerUpChooser.powerUps;
        if (list != null && !list.Contains(nextUpgrade.Upgrade))
        {
            list.Add(nextUpgrade.Upgrade);
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
        // None/Custom always allowed
        if (type == UpgradeType.None || type == UpgradeType.Custom) return true;

        bool hasKnife = HasParent<Knife>();
        bool hasShooter = HasParent<SimpleShooter>();
        bool hasTick = HasParent<WeaponTick>();

        switch (type)
        {
            // Knife-only
            case UpgradeType.KnifeDamageFlat:
            case UpgradeType.KnifeDamagePercent:
            case UpgradeType.KnifeDamageTypeIndex:
            case UpgradeType.KnifeRadiusFlat:
            case UpgradeType.KnifeRadiusPercent:
            case UpgradeType.KnifeMaxTargetsFlat:
            case UpgradeType.KnifeLifestealFlat:
            case UpgradeType.KnifeLifestealPercent:
            case UpgradeType.KnifeCritChanceFlat:
            case UpgradeType.KnifeCritMultiplierFlat:
            case UpgradeType.KnifeSplashRadiusFlat:
            case UpgradeType.KnifeSplashRadiusPercent:
            case UpgradeType.KnifeSplashDamagePercentFlat:
            case UpgradeType.KnifeSplashDamagePercentPercent:
            case UpgradeType.KnifeStatusApplyChanceFlat:
            case UpgradeType.KnifeStatusApplyChancePercent:
            case UpgradeType.KnifeStatusDurationFlat:
            case UpgradeType.KnifeStatusDurationPercent:
            case UpgradeType.KnifeEnableStatusEffect:
            case UpgradeType.KnifeStatusEffectIndex:
                return hasKnife;

            // Shooter-only
            case UpgradeType.ShooterDamageFlat:
            case UpgradeType.ShooterDamagePercent:
            case UpgradeType.ShooterDamageTypeIndex:
            case UpgradeType.ShooterProjectileCount:
            case UpgradeType.ShooterSpreadAngleFlat:
            case UpgradeType.ShooterSpreadAnglePercent:
            case UpgradeType.ShooterProjectileSpeedFlat:
            case UpgradeType.ShooterProjectileSpeedPercent:
            case UpgradeType.ShooterLifetimeFlat:
            case UpgradeType.ShooterLifetimePercent:
            case UpgradeType.ShooterCritChanceFlat:
            case UpgradeType.ShooterCritMultiplierFlat:
            case UpgradeType.ShooterStatusApplyChanceFlat:
            case UpgradeType.ShooterStatusApplyChancePercent:
            case UpgradeType.ShooterStatusDurationFlat:
            case UpgradeType.ShooterStatusDurationPercent:
            case UpgradeType.ShooterEnableStatusEffect:
            case UpgradeType.ShooterStatusEffectIndex:
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

        // Custom leaves name/description alone
        if (upgradeType == UpgradeType.Custom) return;

        const string numColor = "#8888FF";
        string C(string s) => $"<color={numColor}>{s}</color>";

        switch (upgradeType)
        {
            // ------------- Knife (displayed as Melee) -------------
            case UpgradeType.KnifeDamageFlat:
                Upgrade.powerUpName = $"Damage Up +{C(Mathf.RoundToInt(value).ToString())}";
                Upgrade.powerUpDescription = $"Increases weapon damage by {C(Mathf.RoundToInt(value).ToString())}.";
                break;
            case UpgradeType.KnifeDamagePercent:
                Upgrade.powerUpName = $"Damage Up +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Increases weapon damage by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.KnifeDamageTypeIndex:
                {
                    var dt = ClampToDamageType(Mathf.RoundToInt(value));
                    Upgrade.powerUpName = $"Damage Type: {dt}";
                    Upgrade.powerUpDescription = $"Changes this weapon's damage type to {dt}.";
                    break;
                }
            case UpgradeType.KnifeRadiusFlat:
                Upgrade.powerUpName = $"Range Up +{C(value.ToString("F2"))}";
                Upgrade.powerUpDescription = $"Increases attack reach by {C(value.ToString("F2"))}.";
                break;
            case UpgradeType.KnifeRadiusPercent:
                Upgrade.powerUpName = $"Range Up +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Increases attack reach by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.KnifeMaxTargetsFlat:
                Upgrade.powerUpName = $"Additional Targets +{C(Mathf.RoundToInt(value).ToString())}";
                Upgrade.powerUpDescription = $"Allows strikes to affect {C(Mathf.RoundToInt(value).ToString())} additional target(s).";
                break;
            case UpgradeType.KnifeLifestealFlat:
                Upgrade.powerUpName = $"Lifesteal Up +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Increases lifesteal by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.KnifeLifestealPercent:
                Upgrade.powerUpName = $"Lifesteal Up +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Further empowers lifesteal by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.KnifeCritChanceFlat:
                Upgrade.powerUpName = $"Critical Chance +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Raises chance to critically strike by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.KnifeCritMultiplierFlat:
                Upgrade.powerUpName = $"Critical Damage +{C(value.ToString("F2"))}x";
                Upgrade.powerUpDescription = $"Increases critical strike damage by {C(value.ToString("F2"))}x.";
                break;
            case UpgradeType.KnifeSplashRadiusFlat:
                Upgrade.powerUpName = $"Splash Radius +{C(value.ToString("F2"))}";
                Upgrade.powerUpDescription = $"Widens splash reach by {C(value.ToString("F2"))}.";
                break;
            case UpgradeType.KnifeSplashRadiusPercent:
                Upgrade.powerUpName = $"Splash Radius +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Widens splash reach by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.KnifeSplashDamagePercentFlat:
                Upgrade.powerUpName = $"Splash Potency +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Increases splash damage by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.KnifeSplashDamagePercentPercent:
                Upgrade.powerUpName = $"Splash Potency +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Further empowers splash damage by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.KnifeStatusApplyChanceFlat:
                Upgrade.powerUpName = $"Status Chance +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Increases chance to inflict effects by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.KnifeStatusApplyChancePercent:
                Upgrade.powerUpName = $"Status Chance +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Further improves effect application by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.KnifeStatusDurationFlat:
                Upgrade.powerUpName = $"Status Duration +{C(value.ToString("F1"))}s";
                Upgrade.powerUpDescription = $"Effects linger longer by {C(value.ToString("F1"))}s.";
                break;
            case UpgradeType.KnifeStatusDurationPercent:
                Upgrade.powerUpName = $"Status Duration +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Effects linger longer by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.KnifeEnableStatusEffect:
                Upgrade.powerUpName = $"Enable Status On Hit";
                Upgrade.powerUpDescription = $"Enables inflicting status effects on hit.";
                break;
            case UpgradeType.KnifeStatusEffectIndex:
                Upgrade.powerUpName = $"Set Status Type";
                Upgrade.powerUpDescription = $"Sets the status effect type (index {C(Mathf.RoundToInt(value).ToString())}).";
                break;

            // ------------- Shooter -------------
            case UpgradeType.ShooterDamageFlat:
                Upgrade.powerUpName = $"Damage Up +{C(Mathf.RoundToInt(value).ToString())}";
                Upgrade.powerUpDescription = $"Increases weapon damage by {C(Mathf.RoundToInt(value).ToString())}.";
                break;
            case UpgradeType.ShooterDamagePercent:
                Upgrade.powerUpName = $"Damage Up +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Increases weapon damage by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.ShooterDamageTypeIndex:
                {
                    var dt = ClampToDamageType(Mathf.RoundToInt(value));
                    Upgrade.powerUpName = $"Damage Type: {dt}";
                    Upgrade.powerUpDescription = $"Changes this weapon's damage type to {dt}.";
                    break;
                }
            case UpgradeType.ShooterProjectileCount:
                Upgrade.powerUpName = $"Projectile Count +{C(Mathf.RoundToInt(value).ToString())}";
                Upgrade.powerUpDescription = $"Fires {C(Mathf.RoundToInt(value).ToString())} additional projectile(s).";
                break;
            case UpgradeType.ShooterSpreadAngleFlat:
                Upgrade.powerUpName = $"Spread +{C(value.ToString("F1"))}°";
                Upgrade.powerUpDescription = $"Narrows firing spread by {C(value.ToString("F1"))}°.";
                break;
            case UpgradeType.ShooterSpreadAnglePercent:
                Upgrade.powerUpName = $"Spread +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Narrows firing spread by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.ShooterProjectileSpeedFlat:
                Upgrade.powerUpName = $"Projectile Speed +{C(value.ToString("F1"))}";
                Upgrade.powerUpDescription = $"Increases projectile speed by {C(value.ToString("F1"))}.";
                break;
            case UpgradeType.ShooterProjectileSpeedPercent:
                Upgrade.powerUpName = $"Projectile Speed +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Increases projectile speed by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.ShooterLifetimeFlat:
                Upgrade.powerUpName = $"Projectile Lifetime +{C(value.ToString("F1"))}s";
                Upgrade.powerUpDescription = $"Extends projectile lifetime by {C(value.ToString("F1"))}s.";
                break;
            case UpgradeType.ShooterLifetimePercent:
                Upgrade.powerUpName = $"Projectile Lifetime +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Extends projectile lifetime by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.ShooterCritChanceFlat:
                Upgrade.powerUpName = $"Critical Chance +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Raises chance to critically strike by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.ShooterCritMultiplierFlat:
                Upgrade.powerUpName = $"Critical Damage +{C(value.ToString("F2"))}x";
                Upgrade.powerUpDescription = $"Increases critical strike damage by {C(value.ToString("F2"))}x.";
                break;
            case UpgradeType.ShooterStatusApplyChanceFlat:
                Upgrade.powerUpName = $"Status Chance +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Increases chance to inflict effects by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.ShooterStatusApplyChancePercent:
                Upgrade.powerUpName = $"Status Chance +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Further improves effect application by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.ShooterStatusDurationFlat:
                Upgrade.powerUpName = $"Status Duration +{C(value.ToString("F1"))}s";
                Upgrade.powerUpDescription = $"Effects linger longer by {C(value.ToString("F1"))}s.";
                break;
            case UpgradeType.ShooterStatusDurationPercent:
                Upgrade.powerUpName = $"Status Duration +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Effects linger longer by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.ShooterEnableStatusEffect:
                Upgrade.powerUpName = $"Enable Status On Hit";
                Upgrade.powerUpDescription = $"Enables inflicting status effects on hit.";
                break;
            case UpgradeType.ShooterStatusEffectIndex:
                Upgrade.powerUpName = $"Set Status Type";
                Upgrade.powerUpDescription = $"Sets the status effect type (index {C(Mathf.RoundToInt(value).ToString())}).";
                break;

            // ------------- Tick -------------
            case UpgradeType.TickRateFlat:
                Upgrade.powerUpName = $"Attack Interval −{C(value.ToString("F2"))}s";
                Upgrade.powerUpDescription = $"Reduces delay between attacks by {C(value.ToString("F2"))}s.";
                break;
            case UpgradeType.TickRatePercent:
                Upgrade.powerUpName = $"Attack Interval −{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Reduces delay between attacks by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.BurstCountFlat:
                Upgrade.powerUpName = $"Burst Count +{C(Mathf.RoundToInt(value).ToString())}";
                Upgrade.powerUpDescription = $"Increases shots per burst by {C(Mathf.RoundToInt(value).ToString())}.";
                break;
            case UpgradeType.BurstCountPercent:
                Upgrade.powerUpName = $"Burst Count +{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Increases shots per burst by {C((value * 100f).ToString("F0"))}%.";
                break;
            case UpgradeType.BurstSpacingFlat:
                Upgrade.powerUpName = $"Burst Delay −{C(value.ToString("F2"))}s";
                Upgrade.powerUpDescription = $"Reduces delay between burst shots by {C(value.ToString("F2"))}s.";
                break;
            case UpgradeType.BurstSpacingPercent:
                Upgrade.powerUpName = $"Burst Delay −{C((value * 100f).ToString("F0"))}%";
                Upgrade.powerUpDescription = $"Reduces delay between burst shots by {C((value * 100f).ToString("F0"))}%.";
                break;

            default:
                Upgrade.powerUpName = "No Upgrade";
                Upgrade.powerUpDescription = "This upgrade slot is empty.";
                break;
        }

        // Append parent name unless Custom (handled above) or no parent
        if (transform.parent != null && !string.IsNullOrEmpty(Upgrade.powerUpName))
        {
            Upgrade.powerUpName = $"{transform.parent.name} - {Upgrade.powerUpName}";
        }
    }

    private void TryAssignIconFromParent()
    {
        if (Upgrade == null) return;
        // Read weapon sprite from parent weapon behaviours
        if (transform.parent != null)
        {
            if (transform.parent.TryGetComponent(out Knife knife))
            {
                if (knife.weaponSprite != null)
                {
                    Upgrade.powerUpIcon = knife.weaponSprite;
                    return;
                }
            }
            if (transform.parent.TryGetComponent(out SimpleShooter shooter))
            {
                if (shooter.weaponSprite != null)
                {
                    Upgrade.powerUpIcon = shooter.weaponSprite;
                    return;
                }
            }
        }
    }

    public void ApplyUpgrade()
    {
        // CUSTOM: intentionally does nothing
        if (upgradeType == UpgradeType.Custom || upgradeType == UpgradeType.None)
            return;

        // ---------------- KNIFE ----------------
        if (transform.parent != null && transform.parent.TryGetComponent(out Knife knife))
        {
            switch (upgradeType)
            {
                case UpgradeType.KnifeDamageFlat:
                    knife.damage += Mathf.RoundToInt(value);
                    break;

                case UpgradeType.KnifeDamagePercent:
                    knife.damage = Mathf.RoundToInt(knife.damage * (1f + value));
                    break;

                case UpgradeType.KnifeRadiusFlat:
                    knife.radius += value;
                    break;

                case UpgradeType.KnifeRadiusPercent:
                    knife.radius *= (1f + value);
                    break;

                case UpgradeType.KnifeMaxTargetsFlat:
                    knife.maxTargetsPerTick += Mathf.RoundToInt(value);
                    break;

                case UpgradeType.KnifeLifestealFlat:
                    knife.lifestealPercent += value;
                    break;

                case UpgradeType.KnifeLifestealPercent:
                    knife.lifestealPercent *= (1f + value);
                    break;

                case UpgradeType.KnifeCritChanceFlat:
                    knife.critChance += value;
                    break;

                case UpgradeType.KnifeCritMultiplierFlat:
                    knife.critMultiplier += value;
                    break;
                case UpgradeType.KnifeSplashRadiusFlat:
                    knife.splashRadius += value;
                    break;

                case UpgradeType.KnifeSplashRadiusPercent:
                    knife.splashRadius *= (1f + value);
                    break;

                case UpgradeType.KnifeSplashDamagePercentFlat:
                    knife.splashDamagePercent += value;
                    break;

                case UpgradeType.KnifeSplashDamagePercentPercent:
                    knife.splashDamagePercent *= (1f + value);
                    break;

                case UpgradeType.KnifeStatusApplyChanceFlat:
                    knife.statusApplyChance = Mathf.Clamp01(knife.statusApplyChance + value);
                    break;

                case UpgradeType.KnifeStatusApplyChancePercent:
                    knife.statusApplyChance = Mathf.Clamp01(knife.statusApplyChance * (1f + value));
                    break;

                case UpgradeType.KnifeStatusDurationFlat:
                    knife.statusEffectDuration += value;
                    break;

                case UpgradeType.KnifeStatusDurationPercent:
                    knife.statusEffectDuration *= (1f + value);
                    break;

                case UpgradeType.KnifeEnableStatusEffect:
                    knife.applyStatusEffectOnHit = true;
                    break;

                case UpgradeType.KnifeStatusEffectIndex:
                    knife.EnableOnHitEffectByIndex(Mathf.RoundToInt(value));
                    break;

                case UpgradeType.KnifeDamageTypeIndex:
                    knife.damageType = ClampToDamageType(Mathf.RoundToInt(value));
                    break;
            }
        }

        // ---------------- SHOOTER ----------------
        if (transform.parent != null && transform.parent.TryGetComponent(out SimpleShooter shooter))
        {
            switch (upgradeType)
            {
                case UpgradeType.ShooterDamageFlat:
                    shooter.damage += Mathf.RoundToInt(value);
                    break;

                case UpgradeType.ShooterDamagePercent:
                    shooter.damage = Mathf.RoundToInt(shooter.damage * (1f + value));
                    break;

                case UpgradeType.ShooterProjectileCount:
                    shooter.projectileCount += Mathf.RoundToInt(value);
                    break;

                case UpgradeType.ShooterSpreadAngleFlat:
                    shooter.spreadAngle += value;
                    break;

                case UpgradeType.ShooterSpreadAnglePercent:
                    shooter.spreadAngle *= (1f + value);
                    break;

                case UpgradeType.ShooterProjectileSpeedFlat:
                    shooter.shootForce += value;
                    break;

                case UpgradeType.ShooterProjectileSpeedPercent:
                    shooter.shootForce *= (1f + value);
                    break;

                case UpgradeType.ShooterLifetimeFlat:
                    shooter.bulletLifetime += value;
                    break;

                case UpgradeType.ShooterLifetimePercent:
                    shooter.bulletLifetime *= (1f + value);
                    break;

                case UpgradeType.ShooterCritChanceFlat:
                    shooter.critChance += value;
                    break;

                case UpgradeType.ShooterCritMultiplierFlat:
                    shooter.critMultiplier += value;
                    break;

                case UpgradeType.ShooterStatusApplyChanceFlat:
                    shooter.statusApplyChance = Mathf.Clamp01(shooter.statusApplyChance + value);
                    break;

                case UpgradeType.ShooterStatusApplyChancePercent:
                    shooter.statusApplyChance = Mathf.Clamp01(shooter.statusApplyChance * (1f + value));
                    break;

                case UpgradeType.ShooterStatusDurationFlat:
                    shooter.statusEffectDuration += value;
                    break;

                case UpgradeType.ShooterStatusDurationPercent:
                    shooter.statusEffectDuration *= (1f + value);
                    break;

                case UpgradeType.ShooterEnableStatusEffect:
                    shooter.applyStatusEffectOnHit = true;
                    break;

                case UpgradeType.ShooterStatusEffectIndex:
                    shooter.EnableOnHitEffectByIndex(Mathf.RoundToInt(value));
                    break;

                case UpgradeType.ShooterDamageTypeIndex:
                    shooter.damageType = ClampToDamageType(Mathf.RoundToInt(value));
                    break;
            }
        }

        // ---------------- TICK ----------------
        if (transform.parent != null && transform.parent.TryGetComponent(out WeaponTick tick))
        {
            switch (upgradeType)
            {
                case UpgradeType.TickRateFlat:
                    tick.interval = Mathf.Max(0.05f, tick.interval - value);
                    break;

                case UpgradeType.TickRatePercent:
                    tick.interval = Mathf.Max(0.05f, tick.interval * (1f - value));
                    break;

                case UpgradeType.BurstCountFlat:
                    tick.burstCount += Mathf.RoundToInt(value);
                    break;

                case UpgradeType.BurstCountPercent:
                    tick.burstCount = Mathf.RoundToInt(tick.burstCount * (1f + value));
                    break;

                case UpgradeType.BurstSpacingFlat:
                    tick.burstSpacing = Mathf.Max(0.01f, tick.burstSpacing - value);
                    break;

                case UpgradeType.BurstSpacingPercent:
                    tick.burstSpacing = Mathf.Max(0.01f, tick.burstSpacing * (1f - value));
                    break;
            }
        }
    }

    private static SimpleHealth.DamageType ClampToDamageType(int rawIndex)
    {
        // Ensure value maps into enum range 0..4
        if (rawIndex < 0) rawIndex = 0;
        if (rawIndex > 4) rawIndex = 4;
        return (SimpleHealth.DamageType)rawIndex;
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

            // Filter choices based on parent components
            var all = (WeaponUpgrades.UpgradeType[])System.Enum.GetValues(typeof(WeaponUpgrades.UpgradeType));
            System.Collections.Generic.List<WeaponUpgrades.UpgradeType> allowed = new();

            foreach (var t in all)
            {
                if (wu.IsTypeAllowedForParent(t))
                    allowed.Add(t);
            }
            // After: value/other fields + so.ApplyModifiedProperties() if you prefer
            // Find the serialized property for the UnityEvent
            var customEventProp = so.FindProperty("onCustomAwake");

            // Only show the event hook when the selected type is Custom
            if (wu.upgradeType == WeaponUpgrades.UpgradeType.Custom)
            {
                UnityEditor.EditorGUILayout.Space();
                UnityEditor.EditorGUILayout.LabelField("Custom Events", UnityEditor.EditorStyles.boldLabel);
                UnityEditor.EditorGUILayout.PropertyField(customEventProp);
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

    // ---------------------- Tools ----------------------
    [ContextMenu("Generate Random Upgrade")]
    private void GenerateRandomUpgrade()
    {
        // Build allowed list excluding None/Custom
        var all = (UpgradeType[])System.Enum.GetValues(typeof(UpgradeType));
        System.Collections.Generic.List<UpgradeType> allowed = new();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == UpgradeType.None || t == UpgradeType.Custom) continue;
            if (IsTypeAllowedForParent(t)) allowed.Add(t);
        }
        if (allowed.Count == 0)
        {
            Debug.LogWarning("[WeaponUpgrades] No valid upgrade types for this parent.");
            return;
        }

        // Pick a type and value
        int pick = Random.Range(0, allowed.Count);
        var chosen = allowed[pick];

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Generate Random Upgrade");
#endif
        upgradeType = chosen;
        value = GetRandomValueForType(chosen);
        SetUpgradeInfo();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private float GetRandomValueForType(UpgradeType t)
    {
        switch (t)
        {
            // Knife
            case UpgradeType.KnifeDamageFlat: return Random.Range(2f, 12f);
            case UpgradeType.KnifeDamagePercent: return Random.Range(0.05f, 0.25f);
            case UpgradeType.KnifeRadiusFlat: return Random.Range(0.1f, 1.0f);
            case UpgradeType.KnifeRadiusPercent: return Random.Range(0.05f, 0.30f);
            case UpgradeType.KnifeMaxTargetsFlat: return Mathf.Round(Random.Range(1f, 3.99f));
            case UpgradeType.KnifeLifestealFlat: return Random.Range(0.02f, 0.15f);
            case UpgradeType.KnifeLifestealPercent: return Random.Range(0.10f, 0.40f);
            case UpgradeType.KnifeCritChanceFlat: return Random.Range(0.03f, 0.20f);
            case UpgradeType.KnifeCritMultiplierFlat: return Random.Range(0.10f, 0.60f);
            case UpgradeType.KnifeSplashRadiusFlat: return Random.Range(0.20f, 1.50f);
            case UpgradeType.KnifeSplashRadiusPercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.KnifeSplashDamagePercentFlat: return Random.Range(0.05f, 0.30f);
            case UpgradeType.KnifeSplashDamagePercentPercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.KnifeStatusApplyChanceFlat: return Random.Range(0.05f, 0.30f);
            case UpgradeType.KnifeStatusApplyChancePercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.KnifeStatusDurationFlat: return Random.Range(0.5f, 3.0f);
            case UpgradeType.KnifeStatusDurationPercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.KnifeEnableStatusEffect: return 0f; // toggle only
            case UpgradeType.KnifeStatusEffectIndex:
                return Mathf.Round(Random.Range(0f, (float)System.Enum.GetValues(typeof(StatusEffectSystem.StatusType)).Length - 1f));

            // Shooter
            case UpgradeType.ShooterDamageFlat: return Random.Range(2f, 12f);
            case UpgradeType.ShooterDamagePercent: return Random.Range(0.05f, 0.25f);
            case UpgradeType.ShooterProjectileCount: return Mathf.Round(Random.Range(1f, 3.99f));
            case UpgradeType.ShooterSpreadAngleFlat: return Random.Range(2f, 20f);
            case UpgradeType.ShooterSpreadAnglePercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.ShooterProjectileSpeedFlat: return Random.Range(0.5f, 5f);
            case UpgradeType.ShooterProjectileSpeedPercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.ShooterLifetimeFlat: return Random.Range(0.3f, 2f);
            case UpgradeType.ShooterLifetimePercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.ShooterCritChanceFlat: return Random.Range(0.03f, 0.20f);
            case UpgradeType.ShooterCritMultiplierFlat: return Random.Range(0.10f, 0.60f);
            case UpgradeType.ShooterStatusApplyChanceFlat: return Random.Range(0.05f, 0.30f);
            case UpgradeType.ShooterStatusApplyChancePercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.ShooterStatusDurationFlat: return Random.Range(0.5f, 3.0f);
            case UpgradeType.ShooterStatusDurationPercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.ShooterEnableStatusEffect: return 0f; // toggle only
            case UpgradeType.ShooterStatusEffectIndex:
                return Mathf.Round(Random.Range(0f, (float)System.Enum.GetValues(typeof(StatusEffectSystem.StatusType)).Length - 1f));

            // Tick
            case UpgradeType.TickRateFlat: return Random.Range(0.05f, 0.50f);
            case UpgradeType.TickRatePercent: return Random.Range(0.05f, 0.30f);
            case UpgradeType.BurstCountFlat: return Mathf.Round(Random.Range(1f, 3.99f));
            case UpgradeType.BurstCountPercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.BurstSpacingFlat: return Random.Range(0.02f, 0.30f);
            case UpgradeType.BurstSpacingPercent: return Random.Range(0.10f, 0.50f);
        }
        return 0f;
    }
}
