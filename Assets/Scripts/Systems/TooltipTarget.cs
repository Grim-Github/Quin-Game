using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea] public string tooltipMessage;

    [Header("Behaviour")]
    [Tooltip("Automatically refresh tooltip text every frame while visible.")]
    [SerializeField] private bool autoUpdate = false; // default = false

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
    private SimpleInventory playerInventory; // cached reference to player's inventory
    private bool isShowing;

    private void Awake()
    {
        health = GetComponent<SimpleHealth>();
        invActivator = GetComponent<InventoryButtonActivator>();
    }

    // ===== Shared =====
    // Lazily find the player's SimpleInventory located as a child of the Player-tagged GameObject
    private SimpleInventory GetPlayerInventory()
    {
        if (playerInventory != null) return playerInventory;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;

        // include inactive children when searching
#if UNITY_2023_1_OR_NEWER
        playerInventory = player.GetComponentInChildren<SimpleInventory>(true);
#else
        playerInventory = player.GetComponentInChildren<SimpleInventory>(true);
#endif
        return playerInventory;
    }

    private string BuildTooltipText()
    {
        string full = tooltipMessage;

        // 1) SimpleHealth extras (unchanged from your file)
        if (appendExtraFromHealth && health != null)
        {
            string extra = health.extraTextField; // assumes public on your SimpleHealth

            full += "<sprite name=\"heart_0\"> " + (int)health.currentHealth + "/" + health.maxHealth;

            ChatterStats cs = GetComponent<ChatterStats>();

            if (cs != null)
            {
                full += "\n<color=#1212FC><sprite name=\"power\"> " + cs.power + "</color>";

            }


            if (!string.IsNullOrWhiteSpace(extra))
            {
                if (!string.IsNullOrWhiteSpace(full)) full += "\n";
                full += extra;
            }
        }

        // 2) InventoryButtonActivator requirement block
        if (appendFromInventoryActivator && invActivator != null)
        {
            // Use public getters (you'll need to add these to InventoryButtonActivator if not already there)
            string reqName = invActivator.requiredItemName;
            int reqAmt = Mathf.Max(1, invActivator.requiredAmount);

            // Look up how many the player currently has in their inventory
            int haveAmt = 0;
            var inv = GetPlayerInventory();
            if (inv != null && !string.IsNullOrEmpty(reqName))
            {
                haveAmt = inv.GetAmount(reqName);
            }

            string reqBlock = $"<b>Requires</b>: {reqName} x{reqAmt}  (You have: {haveAmt})";

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
        isShowing = true;
    }

    private void HideTooltipInternal()
    {
        var mgr = TooltipManager.Instance;
        if (mgr != null) mgr.HideTooltip();
        isShowing = false;
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

    private void Update()
    {
        if (autoUpdate && isShowing)
        {
            // Rebuild and re-show to update the text while open
            ShowTooltipInternal();
        }
    }
}
