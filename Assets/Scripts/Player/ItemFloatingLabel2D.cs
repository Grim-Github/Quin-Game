using TMPro;
using UnityEngine;

public class ItemFloatingLabel2D : MonoBehaviour
{
    [SerializeField] private ItemGiver itemGiver;
    [SerializeField] private TextMeshProUGUI labelPrefab;
    [SerializeField] private float yOffset = 0.5f;

    private void Awake()
    {
        UpdateLabel();
    }

    public void UpdateLabel()
    {
        if (!itemGiver) itemGiver = GetComponent<ItemGiver>();
        if (!itemGiver || !labelPrefab) return;

        // Build text: "Orb 1 x3" or "Orb 1 x2-5"
        string itemName = itemGiver.itemName.Replace("(Clone)", "");
        string amountText = (itemGiver.minAmount == itemGiver.maxAmount)
            ? $"x{itemGiver.maxAmount}"
            : $"x{itemGiver.minAmount}-{itemGiver.maxAmount}";

        labelPrefab.text = $"{itemName} {amountText}";
        labelPrefab.alignment = TextAlignmentOptions.Center;

        // Position above item
        labelPrefab.transform.localPosition = new Vector3(0, yOffset, 0);
    }
}
