using UnityEngine;
using UnityEngine.Events;

public class AwakeEventTrigger : MonoBehaviour
{
    [Header("Event to trigger on Awake")]
    public UnityEvent onAwake;

    [Header("Used for upgrades in accesories")]
    [Header("Upgrades")]
    public AccessoriesUpgrades nextUpgrade;
    private PowerUpChooser powerUpChooser;
    private void Awake()
    {
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();
        if (nextUpgrade != null && powerUpChooser != null)
        {
            powerUpChooser.powerUps.Add(nextUpgrade.Upgrade);
        }

        onAwake?.Invoke();
    }
}
