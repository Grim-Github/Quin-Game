using UnityEngine;

public class ItemGiver : MonoBehaviour
{
    [Header("Item Settings")]
    [SerializeField] public string itemName = "Orb 1";
    [Min(1)][SerializeField] public int minAmount = 1;
    [Min(1)][SerializeField] public int maxAmount = 1;
    [SerializeField] public Sprite icon; // Optional, only if your InventoryItem supports icons

    [Header("Inventory Target")]
    [SerializeField] private SimpleInventory playerInventory;
    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        // Try to auto-assign player inventory in the editor
        FindPlayerInventory();
    }

    private void Awake()
    {
        if (!playerInventory)
        {
            FindPlayerInventory();
        }
    }

    private void FindPlayerInventory()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            playerInventory = player.GetComponentInChildren<SimpleInventory>();
        }
    }

    /// <summary>
    /// Gives the configured item to the player's inventory.
    /// </summary>
    public void GiveToPlayer()
    {
        if (!playerInventory)
        {
            Debug.LogWarning("[ItemGiver] No player inventory found.");
            return;
        }

        int amount = Random.Range(minAmount, maxAmount + 1);

        // If your SimpleInventory supports icon
        if (icon != null)
        {
            playerInventory.AddItem(itemName, amount, icon);
        }
        else
        {
            playerInventory.AddItem(itemName, amount);
        }

        // Debug.Log($"[ItemGiver] Gave {amount}x {itemName} to player.");
    }
}
