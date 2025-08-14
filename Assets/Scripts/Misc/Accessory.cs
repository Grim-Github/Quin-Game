using UnityEngine;
using UnityEngine.Events;

public class Accessory : MonoBehaviour
{

    [Header("Power-Up")]
    public string AccesoryName;
    public string AccesoryDescription;
    public Sprite icon;

    [Header("Event to trigger on Awake")]
    public UnityEvent onAwake;

    [Header("Used for upgrades in accesories")]
    [Header("Upgrades")]
    [HideInInspector] public AccessoriesUpgrades nextUpgrade;
    private PowerUpChooser powerUpChooser;
    private void Awake()
    {
        // Auto-find the first child with AccessoriesUpgrades if not already assigned
        if (nextUpgrade == null)
        {
            nextUpgrade = GetComponentInChildren<AccessoriesUpgrades>();
        }

        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();
        if (nextUpgrade != null && powerUpChooser != null)
        {
            powerUpChooser.powerUps.Add(nextUpgrade.Upgrade);
        }

        onAwake?.Invoke();
    }

    public void UpProjectileCount()
    {
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null)
        {
            Debug.LogWarning("[Accessory] Player not found!");
            return;
        }

        // Get all SimpleShooter2D components in the player and its children
        var shooters = player.GetComponentsInChildren<SimpleShooter>(true);

        foreach (var shooter in shooters)
        {
            shooter.projectileCount += 1; // Example: increase projectile count
            Debug.Log($"[Accessory] Increased projectile count for {shooter.name} to {shooter.projectileCount}");
        }
    }


}
