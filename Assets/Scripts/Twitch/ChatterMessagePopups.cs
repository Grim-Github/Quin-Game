using Lexone.UnityTwitchChat;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ChatterMessagePopups : MonoBehaviour
{
    [Header("Message Popup Settings")]
    [Tooltip("Prefab with a TextMeshProUGUI component.")]
    public GameObject messagePrefab;

    [Tooltip("Offset from the chatter's position where the prefab will be spawned.")]
    public Vector3 spawnOffset = new Vector3(0f, 1.5f, 0f);

    private void OnEnable()
    {
        if (IRC.Instance != null)
            IRC.Instance.OnChatMessage += OnChatMessage;
    }

    private void OnDisable()
    {
        if (IRC.Instance != null)
            IRC.Instance.OnChatMessage -= OnChatMessage;
    }

    private void OnChatMessage(Chatter chatter)
    {
        // Match chatter to this object
        if (chatter.tags.displayName == transform.name)
        {
            ShowMessage(chatter.message);
        }
    }

    /// <summary>
    /// Spawns the popup prefab at this object's position + offset
    /// and updates its TextMeshPro text to the given message.
    /// </summary>
    public void ShowMessage(string message)
    {
        if (!messagePrefab)
        {
            Debug.LogWarning("[ChatterMessagePopups] No messagePrefab assigned.");
            return;
        }

        Vector3 spawnPos = transform.position + spawnOffset;
        GameObject msgInstance = Instantiate(messagePrefab, spawnPos, Quaternion.identity);

        TextMeshPro tmp = msgInstance.GetComponent<TextMeshPro>();
        if (tmp)
        {
            tmp.text = message;
            tmp.color = Color.white; // Set default color, can be customized
            tmp.GetComponent<DamagePopup2D>().lifetime = message.Length * 0.1f; // Example: adjust lifetime based on message length
        }
        else
        {
            Debug.LogWarning("[ChatterMessagePopups] Spawned prefab has no TextMeshPro component.");
        }
    }
}
