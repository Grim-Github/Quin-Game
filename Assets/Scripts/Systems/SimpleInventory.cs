using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class InventoryItem
{
    public string itemName;
    public int amount;
    public Sprite icon; // optional; ignore if you don’t use icons

    public InventoryItem(string name, int amount, Sprite icon = null)
    {
        itemName = name;
        this.amount = amount;
        this.icon = icon;
    }
}

public class SimpleInventory : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private List<InventoryItem> items = new List<InventoryItem>();

    [Header("Events")]
    public UnityEvent OnInventoryChanged = new UnityEvent();  // Inspector-friendly
    public event Action InventoryChanged;                     // Code-only

    private void NotifyChanged()
    {
        OnInventoryChanged?.Invoke();
        InventoryChanged?.Invoke();
    }

    private void Awake()
    {
        NotifyChanged();
    }

    /// <summary>Adds an item or increases amount. (Icon optional)</summary>
    public void AddItem(string itemName, int amountToAdd, Sprite icon = null)
    {
        if (amountToAdd <= 0) return;

        var existingItem = items.Find(i => i.itemName == itemName);

        if (existingItem != null)
        {
            existingItem.amount += amountToAdd;
            if (icon != null) existingItem.icon = icon; // update/keep latest if provided
        }
        else
        {
            items.Add(new InventoryItem(itemName, amountToAdd, icon));
        }

        NotifyChanged();
    }

    /// <summary>Removes a specific amount (clamped at 0).</summary>
    public void RemoveItem(string itemName, int amountToRemove)
    {
        var it = items.Find(i => i.itemName == itemName);
        if (it == null) return;

        it.amount -= amountToRemove;
        if (it.amount < 0) it.amount = 0;

        NotifyChanged();
    }

    public int GetAmount(string itemName)
    {
        var it = items.Find(i => i.itemName == itemName);
        return it != null ? Mathf.Max(0, it.amount) : 0;
    }

    public bool HasItemAmount(string itemName, int minAmount)
    {
        if (minAmount <= 0) return true;
        return GetAmount(itemName) >= minAmount;
    }

    public bool TryConsume(string itemName, int amount)
    {
        if (amount <= 0) return false;
        var it = items.Find(i => i.itemName == itemName);
        if (it == null || it.amount < amount) return false;

        it.amount -= amount;
        if (it.amount < 0) it.amount = 0;

        NotifyChanged();
        return true;
    }

    /// <summary>Returns *live* list; don’t modify from outside.</summary>
    public List<InventoryItem> GetItems() => items;
}
