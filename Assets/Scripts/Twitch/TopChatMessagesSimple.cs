using Lexone.UnityTwitchChat;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class TopChatMessagesSimple : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI output; // assign in Inspector

    // message -> count (after simple normalization)
    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

    private void Start()
    {
        // Add a listener for the IRC.OnChatMessage event
        IRC.Instance.OnChatMessage += OnChatMessage;
    }


    private void OnDisable()
    {
        if (IRC.Instance != null) IRC.Instance.OnChatMessage -= OnChatMessage;
    }

    private void OnChatMessage(Chatter c)
    {

        string raw = c?.message ?? "";
        string key = Normalize(raw);
        if (key.Length == 0) return;

        if (!counts.TryGetValue(key, out int n)) counts[key] = 1;
        else counts[key] = n + 1;

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (output == null)
            return;

        if (counts.Count == 0)
        {
            output.text = "Top chat messages:\n—";
            return;
        }

        var top3 = counts
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select((kv, i) => $"{i + 1}. {kv.Key} ({kv.Value})");

        output.text = string.Join("\n", top3);
    }

    // Lowercase, keep only letters/digits/spaces, collapse spaces, trim
    private static string Normalize(string s)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
        bool lastWasSpace = false;

        foreach (char ch in s.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasSpace = false;
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
            }
            // else: drop punctuation/emotes/urls etc.
        }

        return sb.ToString().Trim();
    }
}
