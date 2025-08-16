using Lexone.UnityTwitchChat;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ChatterAction : MonoBehaviour
{
    [System.Serializable]
    public class ActionEntry
    {
        [Tooltip("Message text to look for inside the incoming chat message (case-insensitive substring).")]
        public string messageActivation = "";

        [Tooltip("Event fired when the activation is found in the incoming chat message.")]
        public UnityEvent onTriggered;
    }

    [Header("Matching")]
    [Tooltip("If true, only react to chatters whose displayName equals this GameObject's name.")]
    [SerializeField] private bool restrictToThisDisplayName = true;

    [Header("Actions")]
    [SerializeField] private ActionEntry[] actions;

    private void Start()
    {
        IRC.Instance.OnChatMessage += OnChatMessage;
    }

    private void OnDisable()
    {
        if (IRC.Instance != null)
            IRC.Instance.OnChatMessage -= OnChatMessage;
    }

    private void OnChatMessage(Chatter recentChatter)
    {
        if (recentChatter == null) return;

        if (restrictToThisDisplayName &&
            !string.Equals(recentChatter.tags.displayName, transform.name))
        {
            return;
        }

        string incoming = recentChatter.message?.ToLowerInvariant() ?? string.Empty;

        foreach (var entry in actions)
        {
            if (entry == null || string.IsNullOrEmpty(entry.messageActivation)) continue;

            string activation = entry.messageActivation.ToLowerInvariant();

            if (incoming.Contains(activation))
            {
                try
                {
                    entry.onTriggered?.Invoke();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ChatterAction] Error invoking event for activation '{activation}': {ex}");
                }
            }
        }
    }
}
