using Lexone.UnityTwitchChat;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class TopChatMessagesSimple : MonoBehaviour
{
    public static TopChatMessagesSimple Instance { get; private set; }
    [SerializeField] private TextMeshProUGUI output; // assign in Inspector

    // word -> count (after simple normalization)
    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

    private void Awake()
    {
        Instance = this;
        // Add a listener for the IRC.OnChatMessage event
        IRC.Instance.OnChatMessage += OnChatMessage;
    }

    private void OnDisable()
    {
        if (IRC.Instance != null) IRC.Instance.OnChatMessage -= OnChatMessage;
    }

    // Return the current top N words (descending by count)
    public List<string> GetTopWords(int n = 3)
    {
        if (n <= 0) n = 3;
        // Snapshot and order; ignore null/empty keys just in case
        return counts
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .Take(n)
            .Select(kv => kv.Key)
            .ToList();
    }

    private void OnChatMessage(Chatter c)
    {
        string raw = c?.message ?? string.Empty;
        string norm = Normalize(raw);
        if (norm.Length == 0) return;

        // Split into words and count each
        var words = norm.Split(' ');
        foreach (var w in words)
        {
            if (string.IsNullOrWhiteSpace(w)) continue;
            if (w.Length <= 3) continue; // ignore very short words
            if (!counts.TryGetValue(w, out int n)) counts[w] = 1;
            else counts[w] = n + 1;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (output == null)
            return;

        if (counts.Count == 0)
        {
            output.text = "Top chat words:\n-";
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
