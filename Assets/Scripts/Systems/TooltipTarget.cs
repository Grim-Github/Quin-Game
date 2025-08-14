using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea] public string tooltipMessage;

    [Header("Append From Other Components")]
    [SerializeField] private bool appendExtraFromHealth = true;
    [SerializeField] private bool appendFromInventoryActivator = true;

    [Header("Text Appearance")]
    [Tooltip("Color of the tooltip text.")]
    [SerializeField] private Color textColor = Color.white;

    // Optional: if your TooltipManager wants to follow a specific rect when this is on UI
    [SerializeField] private RectTransform uiAnchorOverride;

    private SimpleHealth health;
    private InventoryButtonActivator invActivator;

    private void Awake()
    {
        health = GetComponent<SimpleHealth>();
        invActivator = GetComponent<InventoryButtonActivator>();
    }

    // ===== Shared =====
    private string BuildTooltipText()
    {
        string full = tooltipMessage;

        // 1) SimpleHealth extras (unchanged from your file)
        if (appendExtraFromHealth && health != null)
        {
            string extra = health.extraTextField; // assumes public on your SimpleHealth
            if (!string.IsNullOrWhiteSpace(extra))
            {
                if (!string.IsNullOrWhiteSpace(full)) full += "\n\n";
                full += extra;
            }
        }

        // 2) InventoryButtonActivator requirement block
        if (appendFromInventoryActivator && invActivator != null)
        {
            // Use public getters (you'll need to add these to InventoryButtonActivator if not already there)
            string reqName = invActivator.requiredItemName;
            int reqAmt = Mathf.Max(1, invActivator.requiredAmount);

            string reqBlock = $"<b>Requires</b>: {reqName} x{reqAmt}";

            if (!string.IsNullOrWhiteSpace(reqBlock))
            {
                if (!string.IsNullOrWhiteSpace(full)) full += "\n\n";
                full += reqBlock;
            }
        }


        // 3) Wrap entire text in color
        string colorHex = ColorUtility.ToHtmlStringRGB(textColor);
        full = $"<color=#{colorHex}>{full}</color>";

        return full;
    }

    private void ShowTooltipInternal()
    {
        var mgr = TooltipManager.Instance;
        if (mgr == null) return;

        string full = BuildTooltipText();
        if (string.IsNullOrWhiteSpace(full)) return;

        mgr.ShowTooltip(full, this, uiAnchorOverride != null ? uiAnchorOverride : null);
    }

    private void HideTooltipInternal()
    {
        var mgr = TooltipManager.Instance;
        if (mgr != null) mgr.HideTooltip();
    }

    // ===== World objects (needs Collider or Collider2D) =====
    private void OnMouseEnter() => ShowTooltipInternal();
    private void OnMouseExit() => HideTooltipInternal();

    // ===== uGUI elements =====
    public void OnPointerEnter(PointerEventData eventData) => ShowTooltipInternal();
    public void OnPointerExit(PointerEventData eventData) => HideTooltipInternal();

    // ===== Lifecycle safety =====
    private void OnDisable() => HideTooltipInternal();
    private void OnDestroy() => HideTooltipInternal();
}
