using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TooltipTarget : MonoBehaviour
{
    [TextArea] public string tooltipMessage;
    [SerializeField] private bool appendExtraFromHealth = true;

    private SimpleHealth health;

    private void Awake()
    {
        health = GetComponent<SimpleHealth>();
    }

    private void OnMouseEnter()
    {
        var mgr = TooltipManager.Instance;
        if (mgr == null) return;

        string full = tooltipMessage;

        if (appendExtraFromHealth && health != null)
        {
            string extra = health.extraTextField; // public field now
            if (!string.IsNullOrWhiteSpace(extra))
            {
                if (!string.IsNullOrWhiteSpace(full)) full += "\n\n";
                full += extra;
            }
        }

        if (string.IsNullOrWhiteSpace(full)) return; // nothing to show
        mgr.ShowTooltip(full, this); // pass target so manager can verify liveness
    }

    private void OnMouseExit()
    {
        var mgr = TooltipManager.Instance;
        if (mgr != null) mgr.HideTooltip();
    }

    private void OnDisable()
    {
        var mgr = TooltipManager.Instance;
        if (mgr != null) mgr.HideTooltip();
    }

    private void OnDestroy()
    {
        var mgr = TooltipManager.Instance;
        if (mgr != null) mgr.HideTooltip();
    }
}
