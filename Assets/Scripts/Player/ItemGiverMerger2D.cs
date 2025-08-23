using UnityEngine;

public class ItemGiverRadiusMerge2D : MonoBehaviour
{
    [SerializeField] private float mergeRadius = 0.75f;
    [SerializeField] private LayerMask itemLayer;

    private void Awake()
    {
        // Auto-assign to this object's layer
        itemLayer = 1 << gameObject.layer;
        MergeNow();
    }

    void MergeNow()
    {
        ItemGiver myGiver = GetComponent<ItemGiver>();
        if (!myGiver) return;

        // Only check colliders in the same layer
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, mergeRadius, itemLayer);

        // Find the first other ItemGiver with the same name
        ItemGiver target = null;
        foreach (var h in hits)
        {
            if (!h) continue;
            var other = h.GetComponent<ItemGiver>();
            if (other == null || other == myGiver) continue;

            if (other.itemName == myGiver.itemName)
            {
                target = other;
                break; // first match is fine; keep it simple
            }
        }

        if (target == null) return;

        // Merge THIS into the OTHER, then destroy THIS
        target.minAmount += myGiver.minAmount;
        target.maxAmount += myGiver.maxAmount;

        // Update the receiver's label if present
        var targetLabel = target.GetComponent<ItemFloatingLabel2D>();
        if (targetLabel) targetLabel.UpdateLabel();

        // Destroy this object last
        Destroy(gameObject);
    }
}
