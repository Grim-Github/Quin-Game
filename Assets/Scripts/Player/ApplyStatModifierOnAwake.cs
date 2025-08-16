using UnityEngine;

/// <summary>
/// Put this on any GameObject (e.g. pickup, powerup).
/// On Awake, it applies a stat modifier to the PlayerStatUpgrades system.
/// </summary>
public class ApplyStatModifierOnAwake : MonoBehaviour
{
    [Header("Modifier To Apply")]
    public PlayerStatUpgrades.TargetGroup target = PlayerStatUpgrades.TargetGroup.AllWeapons;
    public PlayerStatUpgrades.Stat stat = PlayerStatUpgrades.Stat.Damage;

    [Tooltip("Flat amount to add (rounded for ints).")]
    public float add = 0f;

    [Tooltip("Percent modifier (0.20 = +20%).")]
    public float percent = 0f;

    [Tooltip("If true, applies only once and then destroys this GameObject.")]
    public bool destroyAfterApply = true;

    [Header("Finding Player")]
    [Tooltip("Optional explicit reference. If left empty, will auto-find by tag 'Player'.")]
    public PlayerStatUpgrades upgrades;

    private void Awake()
    {
        if (upgrades == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                upgrades = player.GetComponent<PlayerStatUpgrades>();
        }

        if (upgrades == null)
        {
            Debug.LogWarning($"{nameof(ApplyStatModifierOnAwake)}: No PlayerStatUpgrades found!");
            return;
        }

        // Build the modifier
        var mod = new PlayerStatUpgrades.StatModifier
        {
            target = target,
            stat = stat,
            add = add,
            percent = percent
        };

        // Add it
        upgrades.modifiers.Add(mod);

        // Immediately apply
        upgrades.RebuildAndApply();

        Debug.Log($"[Upgrades] Applied {stat} mod to {target}: +{add}, +{percent * 100f}%");

        if (destroyAfterApply)
            Destroy(gameObject);
    }
}
