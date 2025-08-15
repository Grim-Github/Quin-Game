using UnityEngine;

[DisallowMultipleComponent]
public class CanvasRangeActivator : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("Tag of the player object to track.")]
    public string playerTag = "Player";

    [Tooltip("Max distance before the canvas is hidden.")]
    public float activationRange = 10f;

    [Header("Canvas Settings")]
    [Tooltip("Canvas to enable/disable. If left empty, first child Canvas is used.")]
    public Canvas targetCanvas;

    private Transform player;

    private void Awake()
    {
        // Auto-find child canvas if not set
        if (targetCanvas == null)
            targetCanvas = GetComponentInChildren<Canvas>(true);
    }

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogWarning($"[CanvasRangeActivator] No GameObject found with tag '{playerTag}'");
    }

    private void Update()
    {
        if (player == null || targetCanvas == null)
            return;

        float dist = Vector3.Distance(transform.position, player.position);
        bool shouldBeActive = dist <= activationRange;

        if (targetCanvas.gameObject.activeSelf != shouldBeActive)
            targetCanvas.gameObject.SetActive(shouldBeActive);
    }
}
