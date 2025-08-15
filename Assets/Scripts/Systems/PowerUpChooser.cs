using NaughtyAttributes;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[System.Serializable]
public class PowerUp
{
    public string powerUpName;
    [TextArea] public string powerUpDescription;

    [Header("Activation")]
    [Tooltip("Prefab or in-scene GameObject to spawn/enable when selected.")]
    public GameObject powerUpObject;

    [Header("Type")]
    [Tooltip("Treat this power-up as an Accessory (counts toward accessory cap).")]
    public bool IsAccessory;
    [Tooltip("Treat this power-up as a Weapon (counts toward weapon cap).")]
    public bool IsWeapon;

    [Header("Visuals")]
    [Tooltip("Icon representing this power-up. If null, UI will use its default icon.")]
    [ShowAssetPreview] public Sprite powerUpIcon;

    [Header("Spawn Weight")]
    [Tooltip("Relative chance for this power-up to appear in selection. Higher = more common.")]
    [Min(0f)]
    public float weight = 1f;
}

public class PowerUpChooser : MonoBehaviour
{
    [Header("Available & Selected")]
    public List<PowerUp> powerUps = new();
    public List<PowerUp> selectedPowerUps = new();

    [Header("Limits")]
    [Min(0)] public int maxAccessories = 1;
    [Min(0)] public int maxWeapons = 1;

    [Header("Stats UI")]
    [Tooltip("Optional: TextMeshProUGUI that will show 'Accessories: cur/max' and 'Weapons: cur/max'.")]
    [SerializeField] private TextMeshProUGUI statsSummaryText;

    public int CurrentAccessories => CountSelected(p => p.IsAccessory);
    public int CurrentWeapons => CountSelected(p => p.IsWeapon);
    public int MaxAccessories => maxAccessories;
    public int MaxWeapons => maxWeapons;

    public int RemainingAccessorySlots => Mathf.Max(0, maxAccessories - CurrentAccessories);
    public int RemainingWeaponSlots => Mathf.Max(0, maxWeapons - CurrentWeapons);

    private void Awake()
    {
        SyncActiveToSelected();   // NEW
        RefreshStatsText();
    }

    private void OnEnable()
    {
        SyncActiveToSelected();   // NEW
        RefreshStatsText();
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keep UI in sync when tweaking in the inspector
        RefreshStatsText();
    }
#endif

    public bool CanSelect(PowerUp pu)
    {
        if (pu == null) return false;
        if (pu.IsAccessory && RemainingAccessorySlots <= 0) return false;
        if (pu.IsWeapon && RemainingWeaponSlots <= 0) return false;
        return true;
    }

    public bool CanSelectByIndex(int index) =>
        index >= 0 && index < powerUps.Count && CanSelect(powerUps[index]);

    /// <summary>
    /// Attempts to choose the power-up at index. Spawns/enables its object,
    /// moves it to selected list, and removes it from available list.
    /// </summary>
    public bool TryChoosePowerUp(int index)
    {
        if (!CanSelectByIndex(index)) return false;

        var selected = powerUps[index];

        // Spawn or enable the associated object (if any)
        if (selected.powerUpObject != null)
        {
            if (!selected.powerUpObject.scene.IsValid())
                Instantiate(selected.powerUpObject);
            else
                selected.powerUpObject.SetActive(true);
        }

        selectedPowerUps.Add(selected);
        powerUps.RemoveAt(index);

        // Update UI after selection
        RefreshStatsText();
        return true;
    }
    // Add inside PowerUpChooser class
    public void SyncActiveToSelected()
    {
        if (powerUps == null) return;

        // Move any already-active, in-scene objects to selectedPowerUps
        for (int i = powerUps.Count - 1; i >= 0; i--)
        {
            var pu = powerUps[i];
            if (pu == null || pu.powerUpObject == null) continue;

            // Only treat as "already active" if it's an in-scene object AND active now
            if (pu.powerUpObject.scene.IsValid() && pu.powerUpObject.activeInHierarchy)
            {
                // Avoid duplicates if something already inserted it
                if (!selectedPowerUps.Contains(pu))
                    selectedPowerUps.Add(pu);

                powerUps.RemoveAt(i);
            }
        }

        RefreshStatsText();
    }

    private int CountSelected(System.Predicate<PowerUp> predicate)
    {
        int c = 0;
        for (int i = 0; i < selectedPowerUps.Count; i++)
            if (predicate(selectedPowerUps[i])) c++;
        return c;
    }

    /// <summary>
    /// Updates the bound TextMeshProUGUI with current/max counts.
    /// </summary>
    public void RefreshStatsText()
    {
        if (statsSummaryText == null) return;

        statsSummaryText.text =
            $"Accessories: {CurrentAccessories}/{MaxAccessories}\n" +
            $"Weapons: {CurrentWeapons}/{MaxWeapons}";
    }
}