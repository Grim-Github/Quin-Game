using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIInventory : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SimpleInventory inventory; // Assign your inventory
    [SerializeField] private Transform contentParent;   // e.g., Grid/Vertical Layout Content
    [SerializeField] private GameObject itemUIPrefab;   // Prefab with an Image + TextMeshProUGUI

    [Header("Options")]
    [SerializeField] private bool clearBeforePopulate = true;
    [SerializeField] private bool subscribeInCode = true; // auto-subscribe to InventoryChanged

    private void OnEnable()
    {
        if (subscribeInCode && inventory != null)
            inventory.InventoryChanged += RefreshUI;

        RefreshUI();
    }

    private void OnDisable()
    {
        if (subscribeInCode && inventory != null)
            inventory.InventoryChanged -= RefreshUI;
    }

    [ContextMenu("Refresh UI")]
    public void RefreshUI()
    {
        if (!inventory || !itemUIPrefab || !contentParent)
        {
            Debug.LogWarning("[UIInventory] Missing references.");
            return;
        }

        if (clearBeforePopulate)
        {
            for (int i = contentParent.childCount - 1; i >= 0; i--)
                Destroy(contentParent.GetChild(i).gameObject);
        }

        foreach (var item in inventory.GetItems())
        {
            if (item.amount <= 0) continue; // hide zero-amount items; remove if you want to show them

            var entry = Instantiate(itemUIPrefab, contentParent);

            // Find UI components (first in children)
            Image iconImage = entry.GetComponentInChildren<Image>(true);
            TextMeshProUGUI amountText = entry.GetComponentInChildren<TextMeshProUGUI>(true);

            if (iconImage != null)
            {
                iconImage.enabled = (item.icon != null);
                if (item.icon != null)
                    iconImage.sprite = item.icon;
            }

            if (amountText != null)
                amountText.text = $"{item.itemName} x{item.amount}";
        }
    }
}
