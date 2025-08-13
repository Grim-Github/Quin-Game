using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WeaponRerollButton : MonoBehaviour
{
    [Header("Popup Settings")]
    [Tooltip("Prefab with TextMeshPro (3D) or TextMeshProUGUI (UI).")]
    public GameObject rerollPopupPrefab;

    [Tooltip("Seconds before the popup is destroyed. Set ≤0 to keep it.")]
    public float popupLifetime = 2f;

    [Tooltip("Offset from the chosen spawn position.")]
    public Vector3 popupOffset = new Vector3(0f, 0.5f, 0f);

    [Tooltip("If true, spawn at the weapon's parent position when available.")]
    public bool spawnAtParent = true;

    // Hook this to your UI Button OnClick
    public void RerollRandomWeapon()
    {
        // Unity 6 API: fast, no sorting, include inactive (we'll filter)
        WeaponRarity[] weapons = Object.FindObjectsByType<WeaponRarity>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (weapons == null || weapons.Length == 0)
        {
            Debug.LogWarning("No WeaponRarity found in the scene.");
            return;
        }

        // Filter to active & enabled
        var active = new List<WeaponRarity>(weapons.Length);
        foreach (var w in weapons)
            if (w != null && w.isActiveAndEnabled && w.gameObject.activeInHierarchy)
                active.Add(w);

        if (active.Count == 0)
        {
            Debug.LogWarning("No active WeaponRarity found to reroll.");
            return;
        }

        // Pick one and reroll stats (not rarity)
        WeaponRarity selected = active[Random.Range(0, active.Count)];
        selected.RerollStats();
        Debug.Log($"Rerolled stats for: {selected.transform.name}");

        SpawnPopup(selected);
    }

    private void SpawnPopup(WeaponRarity selected)
    {
        if (rerollPopupPrefab == null) return;

        Vector3 basePos = (spawnAtParent && selected.transform.parent != null)
            ? selected.transform.parent.position
            : selected.transform.position;

        GameObject popup = Instantiate(rerollPopupPrefab, basePos + popupOffset, Quaternion.identity);

        // Support either TextMeshPro (world) or TextMeshProUGUI (UI)
        if (popup.TryGetComponent<TextMeshPro>(out var tmp))
        {
            tmp.text = $"Rerolled: {selected.transform.name}";
        }
        else if (popup.TryGetComponent<TextMeshProUGUI>(out var tmpUI))
        {
            tmpUI.text = $"Rerolled: {selected.transform.name}";
        }
        else
        {
            Debug.LogWarning("Popup prefab has no TextMeshPro/TextMeshProUGUI component.");
        }

        if (popupLifetime > 0f) Destroy(popup, popupLifetime);
    }
}
