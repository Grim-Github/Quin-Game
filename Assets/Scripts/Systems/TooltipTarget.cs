using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea] public string tooltipMessage;
    [SerializeField] private bool appendExtraFromHealth = true;

    // Optional: if your TooltipManager wants to follow a specific rect when this is on UI
    [SerializeField] private RectTransform uiAnchorOverride;

    private SimpleHealth health;

    private void Awake()
    {
        health = GetComponent<SimpleHealth>();
    }

    // ===== Shared =====
    private string BuildTooltipText()
    {
        string full = tooltipMessage;

        if (appendExtraFromHealth && health != null)
        {
            string extra = health.extraTextField; // assumes public on your SimpleHealth
            if (!string.IsNullOrWhiteSpace(extra))
            {
                if (!string.IsNullOrWhiteSpace(full)) full += "\n\n";
                full += extra;
            }
        }

        return full;
    }

    private void ShowTooltipInternal()
    {
        var mgr = TooltipManager.Instance;
        if (mgr == null) return;

        string full = BuildTooltipText();
        if (string.IsNullOrWhiteSpace(full)) return;

        // If your manager supports anchoring/following a RectTransform for UI elements,
        // pass 'this' so it can verify liveness, and optionally give it an anchor hint.
        // (Adjust this call to match your TooltipManager API.)
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
