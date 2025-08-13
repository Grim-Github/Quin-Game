using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Button helper that rerolls ONE random weapon in the scene
/// using the new WeaponRarityController system.
/// </summary>
public class WeaponRerollButton : MonoBehaviour
{
    [Header("Action")]
    [Tooltip("If true, calls RerollRarityAndStats(); otherwise RerollStats().")]
    public bool includeRarity = false;

    [Header("Popup Settings")]
    [Tooltip("Prefab with TextMeshPro (3D) or TextMeshProUGUI (UI).")]
    public GameObject rerollPopupPrefab;

    [Tooltip("Seconds before the popup is destroyed. Set ≤0 to keep it.")]
    public float popupLifetime = 2f;

    [Tooltip("Offset from the chosen spawn position.")]
    public Vector3 popupOffset = new Vector3(0f, 0.5f, 0f);

    [Tooltip("If true, spawn at the weapon's parent position when available.")]
    public bool spawnAtParent = true;

    [Tooltip("Optional parent for UI popups (if prefab uses TextMeshProUGUI).")]
    public Transform uiPopupParent;

    // Hook this to your UI Button OnClick
    public void RerollRandomWeapon()
    {
        var controllers = Object.FindObjectsByType<WeaponRarityController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        WeaponRarityController picked = PickActive(controllers);
        if (picked == null)
        {
            Debug.LogWarning("No active WeaponRarityController found in the scene.");
            return;
        }

        if (includeRarity) picked.RerollRarityAndStats();
        else picked.RerollStats();

        Debug.Log($"[Reroll] {(includeRarity ? "Rarity+Stats" : "Stats")} for: {picked.transform.name}");
        SpawnPopup(picked.transform, picked.name);
    }

    // ------------------ helpers ------------------

    private static T PickActive<T>(T[] all) where T : Behaviour
    {
        if (all == null || all.Length == 0) return null;
        var active = new List<T>(all.Length);
        foreach (var c in all)
            if (c != null && c.isActiveAndEnabled && c.gameObject.activeInHierarchy)
                active.Add(c);
        if (active.Count == 0) return null;
        return active[Random.Range(0, active.Count)];
    }

    private void SpawnPopup(Transform target, string weaponName)
    {
        if (rerollPopupPrefab == null || target == null) return;

        Vector3 basePos = (spawnAtParent && target.parent != null)
            ? target.parent.position
            : target.position;

        GameObject popup;

        // If prefab is UI-based and a parent is provided, spawn under that parent.
        if (rerollPopupPrefab.GetComponent<TextMeshProUGUI>() != null && uiPopupParent != null)
        {
            popup = Instantiate(rerollPopupPrefab, uiPopupParent, false);
            if (popup.TryGetComponent<RectTransform>(out var rt))
            {
                Vector3 screen = Camera.main ? Camera.main.WorldToScreenPoint(basePos + popupOffset) : (Vector3)Input.mousePosition;
                rt.position = screen;
            }
        }
        else
        {
            popup = Instantiate(rerollPopupPrefab, basePos + popupOffset, Quaternion.identity);
        }

        // Text assignment
        if (popup.TryGetComponent<TextMeshPro>(out var tmp))
            tmp.text = $"Rerolled: {weaponName}{(includeRarity ? " (Rarity+Stats)" : " (Stats)")}";
        else if (popup.TryGetComponent<TextMeshProUGUI>(out var tmpUI))
            tmpUI.text = $"Rerolled: {weaponName}{(includeRarity ? " (Rarity+Stats)" : " (Stats)")}";
        else
            Debug.LogWarning("Popup prefab has no TextMeshPro/TextMeshProUGUI component.");

        if (popupLifetime > 0f) Destroy(popup, popupLifetime);
    }
}
