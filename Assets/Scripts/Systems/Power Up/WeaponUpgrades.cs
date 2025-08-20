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
        KnifeRadiusFlat,
        KnifeRadiusPercent,
        KnifeMaxTargetsFlat,
        KnifeLifestealFlat,
        KnifeLifestealPercent,
        KnifeCritChanceFlat,
        KnifeCritMultiplierFlat,

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
        ShooterCritChanceFlat,
        ShooterCritMultiplierFlat,

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

    [Header("Icon (optional)")]
    [Tooltip("If assigned, this sprite will be written into Upgrade.powerUpIcon.")]
    public Sprite icon;

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

        // Ensure icon if provided
        if (Upgrade != null && icon != null)
            Upgrade.powerUpIcon = icon;

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

        // Only set icon from our explicit field (no reflection)
        if (icon != null)
            Upgrade.powerUpIcon = icon;

        // Custom leaves name/description alone
        if (upgradeType == UpgradeType.Custom) return;

        switch (upgradeType)
        {
            // ------------- Knife (displayed as Melee) -------------
            case UpgradeType.KnifeDamageFlat:
                Upgrade.powerUpName = $"Melee Damage +{Mathf.RoundToInt(value)}";
                Upgrade.powerUpDescription = $"Increase Melee damage by {Mathf.RoundToInt(value)}.";
                break;
            case UpgradeType.KnifeDamagePercent:
                Upgrade.powerUpName = $"Melee Damage +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase Melee damage by {value * 100f:F0}%.";
                break;
            case UpgradeType.KnifeRadiusFlat:
                Upgrade.powerUpName = $"Melee Range +{value:F2}";
                Upgrade.powerUpDescription = $"Increase Melee attack radius by {value:F2} units.";
                break;
            case UpgradeType.KnifeRadiusPercent:
                Upgrade.powerUpName = $"Melee Range +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase Melee attack radius by {value * 100f:F0}%.";
                break;
            case UpgradeType.KnifeMaxTargetsFlat:
                Upgrade.powerUpName = $"Multi-Target +{Mathf.RoundToInt(value)}";
                Upgrade.powerUpDescription = $"Melee can hit {Mathf.RoundToInt(value)} additional target(s) per tick.";
                break;
            case UpgradeType.KnifeLifestealFlat:
                Upgrade.powerUpName = $"Melee Lifesteal +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Adds {value * 100f:F0}% lifesteal to Melee attacks.";
                break;
            case UpgradeType.KnifeLifestealPercent:
                Upgrade.powerUpName = $"Lifesteal Boost +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase current Melee lifesteal by {value * 100f:F0}%.";
                break;
            case UpgradeType.KnifeCritChanceFlat:
                Upgrade.powerUpName = $"Melee Crit Chance +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase Melee critical hit chance by {value * 100f:F0}%.";
                break;
            case UpgradeType.KnifeCritMultiplierFlat:
                Upgrade.powerUpName = $"Melee Crit Multiplier +{value:F2}x";
                Upgrade.powerUpDescription = $"Increase Melee critical damage multiplier by {value:F2}x.";
                break;

            // ------------- Shooter -------------
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
            case UpgradeType.ShooterCritChanceFlat:
                Upgrade.powerUpName = $"Shooter Crit Chance +{value * 100f:F0}%";
                Upgrade.powerUpDescription = $"Increase Shooter critical hit chance by {value * 100f:F0}%.";
                break;
            case UpgradeType.ShooterCritMultiplierFlat:
                Upgrade.powerUpName = $"Shooter Crit Multiplier +{value:F2}x";
                Upgrade.powerUpDescription = $"Increase Shooter critical damage multiplier by {value:F2}x.";
                break;

            // ------------- Tick -------------
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

        // Append parent name unless Custom (handled above) or no parent
        if (transform.parent != null && !string.IsNullOrEmpty(Upgrade.powerUpName))
        {
            Upgrade.powerUpName = $"{transform.parent.name} - {Upgrade.powerUpName}";
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

            UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("icon"));

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
}
