using Lexone.UnityTwitchChat;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

[Serializable]
public class VoteEntry
{
    [TextArea] public string description = "Describe the chaos";
    public UnityEvent onWin;
    [Min(0f)] public float weight = 1f;
    [Tooltip("Optional identifier for history/analytics. If empty, an internal GUID will be used.")]
    public string id;

    public string EnsureId()
    {
        if (string.IsNullOrWhiteSpace(id)) id = Guid.NewGuid().ToString("N");
        return id;
    }
}

public class VoteManager : MonoBehaviour
{
    private const int kMaxOptions = 6;
    [Header("Options")]
    [Range(2, 6)]
    [SerializeField] private int configuredOptionCount = 3;
    [NonSerialized] private int currentOptionCount = 3;

    [Header("Pool")]
    [SerializeField] private List<VoteEntry> pool = new();

    [Header("Round Timing (seconds)")]
    [Min(0.5f)][SerializeField] private float cooldownSeconds = 20f;
    [Min(2f)][SerializeField] private float voteSeconds = 20f;

    [Header("Timing Source")]
    [Tooltip("Read Time.time (true) or Time.unscaledTime (false). This script will never modify timeScale.")]
    [SerializeField] private bool useScaledTime = true;

    [Header("Repeat Control")]
    [Tooltip("How many recently SHOWN ids to remember to reduce repeats (0 disables).")]
    [Min(0)][SerializeField] private int recentHistorySize = 6;

    public enum Phase { Cooldown, Voting, Resolving }
    [NonSerialized] private Phase phase = Phase.Cooldown;
    [NonSerialized] private float phaseEndTime;
    [NonSerialized] private VoteEntry[] currentOptions = new VoteEntry[kMaxOptions];
    [NonSerialized] private int[] tallies = new int[kMaxOptions];
    private readonly Queue<string> recentIds = new();

    public event Action<float> OnCooldownStart;// duration
    public event Action<float> OnCooldownTick;  // remaining
    public event Action OnCooldownEnd;

    public event Action<VoteEntry[], float> OnVoteStart; // options, duration
    public event Action<float, int[]> OnVoteTick;        // remaining, tallies
    public event Action<VoteEntry, int[]> OnVoteEnd;     // winner, final tallies
    public event Action<VoteEntry> OnOptionInvoked;      // after UnityEvent fired

    public Phase CurrentPhase => phase;
    public float TimeRemaining => Mathf.Max(0f, phaseEndTime - Now);
    public IReadOnlyList<VoteEntry> CurrentOptions => currentOptions;
    public IReadOnlyList<int> Tallies => tallies;

    public void SetPool(IEnumerable<VoteEntry> entries)
    {
        pool.Clear();
        if (entries != null) pool.AddRange(entries);
        NormalizePoolIds();
    }

    // Voting keywords are dynamic (top words from chat)
    private readonly string[] voteKeywords = new string[kMaxOptions];

    private void OnChatMessage(Chatter chatter)
    {
        if (chatter == null || phase != Phase.Voting) return;
        var msg = chatter.message;
        if (string.IsNullOrEmpty(msg)) return;

        if (!TryParseVote(msg, out int idx)) return;
        AcceptVote(idx);
    }

    public void StartRoundNow() => StartVoting();
    public void ForceResolve() { if (phase == Phase.Voting) Resolve(); }

    public bool AcceptVote(int optionIndex)
    {
        if (phase != Phase.Voting || optionIndex < 0 || optionIndex >= currentOptionCount) return false;
        tallies[optionIndex]++;
        return true;
    }

    private void Awake() { EnsureArraySizes(); NormalizePoolIds(); }
    private void OnValidate() => EnsureArraySizes();
    private void Start() => IRC.Instance.OnChatMessage += OnChatMessage;
    private void OnEnable() => EnterCooldown();

    private void Update()
    {
        switch (phase)
        {
            case Phase.Cooldown:
                OnCooldownTick?.Invoke(TimeRemaining);
                if (Now >= phaseEndTime) { OnCooldownEnd?.Invoke(); StartVoting(); }
                break;

            case Phase.Voting:
                OnVoteTick?.Invoke(TimeRemaining, tallies);
                if (Now >= phaseEndTime) Resolve();
                break;
        }
    }

    private float Now => useScaledTime ? Time.time : Time.unscaledTime;

    private void RefreshVoteKeywords()
    {
        string[] fallback = { "LOLW", "ICANT", "ABOBA", "Pog", "GIGA", "HYPE" };
        var src = TopChatMessagesSimple.Instance;
        List<string> top = src != null ? src.GetTopWords(currentOptionCount) : null;
        for (int i = 0; i < currentOptionCount; i++)
        {
            string v = (top != null && i < top.Count && !string.IsNullOrWhiteSpace(top[i])) ? top[i] : fallback[i];
            voteKeywords[i] = v;
        }
        // clear any leftover slots
        for (int i = currentOptionCount; i < kMaxOptions; i++) voteKeywords[i] = string.Empty;
    }

    private bool TryParseVote(string message, out int index)
    {
        index = -1;
        if (string.IsNullOrEmpty(message)) return false;

        var parts = message.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        for (int p = 0; p < parts.Length; p++)
        {
            // strip non-alnum from ends so "aboba!" still matches
            string token = StripNonAlnum(parts[p]);
            if (string.IsNullOrEmpty(token)) continue;
            for (int i = 0; i < currentOptionCount; i++)
            {
                if (!string.IsNullOrEmpty(voteKeywords[i]) && string.Equals(token, voteKeywords[i], StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }
        }
        return false;
    }

    private static string StripNonAlnum(string s)
    {
        int start = 0, end = s.Length - 1;
        while (start <= end && !char.IsLetterOrDigit(s[start])) start++;
        while (end >= start && !char.IsLetterOrDigit(s[end])) end--;
        if (end < start) return string.Empty;
        return s.Substring(start, end - start + 1);
    }

    private void EnterCooldown()
    {
        phase = Phase.Cooldown;
        phaseEndTime = Now + cooldownSeconds;
        OnCooldownStart?.Invoke(cooldownSeconds);
    }

    private void StartVoting()
    {
        currentOptionCount = Mathf.Clamp(configuredOptionCount, 2, kMaxOptions);
        RefreshVoteKeywords();
        PickOptionsWeightedWithoutReplacement();

        for (int i = 0; i < currentOptionCount; i++) tallies[i] = 0;
        for (int i = currentOptionCount; i < kMaxOptions; i++) tallies[i] = 0;

        phase = Phase.Voting;
        phaseEndTime = Now + voteSeconds;
        OnVoteStart?.Invoke(currentOptions, voteSeconds);
    }

    private void Resolve()
    {
        phase = Phase.Resolving;

        // Determine max votes and ties
        int max = 0;
        for (int i = 0; i < currentOptionCount; i++) if (tallies[i] > max) max = tallies[i];

        if (max <= 0)
        {
            // No votes -> no winner
            OnVoteEnd?.Invoke(null, (int[])tallies.Clone());
            EnterCooldown();
            return;
        }

        List<int> tied = new List<int>(currentOptionCount);
        for (int i = 0; i < currentOptionCount; i++) if (tallies[i] == max) tied.Add(i);

        if (tied.Count >= 2)
        {
            // Tie-break round with only tied options
            StartTieBreak(tied);
            return;
        }

        // Single winner
        int winnerIdx = tied[0];
        var winner = currentOptions[winnerIdx];
        if (winner != null)
        {
            try { winner.onWin?.Invoke(); }
            catch (Exception e) { Debug.LogException(e, this); }
            OnOptionInvoked?.Invoke(winner);
        }

        OnVoteEnd?.Invoke(winner, (int[])tallies.Clone());

        // Record shown ids
        for (int i = 0; i < currentOptionCount; i++) PushRecent(currentOptions[i]?.id);

        EnterCooldown();
    }

    private void StartTieBreak(List<int> tiedIndices)
    {
        // Prepare options array with only tied options in leading slots
        int n = Mathf.Clamp(tiedIndices.Count, 2, kMaxOptions);
        currentOptionCount = n;
        for (int i = 0; i < n; i++) currentOptions[i] = currentOptions[tiedIndices[i]];
        for (int i = n; i < kMaxOptions; i++) currentOptions[i] = null;
        for (int i = 0; i < kMaxOptions; i++) tallies[i] = 0;

        RefreshVoteKeywords();

        phase = Phase.Voting;
        phaseEndTime = Now + voteSeconds;
        OnVoteStart?.Invoke(currentOptions, voteSeconds);
    }

    // No direct PickWinner method; handled in Resolve with tie-retry

    // ---------- Selection ----------
    private void PickOptionsWeightedWithoutReplacement()
    {
        // Build eligible (exclude recent if possible)
        List<VoteEntry> eligible = BuildEligible(excludeRecent: true);
        if (eligible.Count < currentOptionCount) eligible = BuildEligible(excludeRecent: false);

        if (eligible.Count == 0)
        {
            // Safe placeholders
            for (int i = 0; i < currentOptionCount; i++)
                currentOptions[i] = new VoteEntry { description = "No options", weight = 1f, id = Guid.NewGuid().ToString("N") };
            return;
        }

        // Draw without replacement by weight
        var temp = new List<VoteEntry>(eligible);
        var picked = new List<VoteEntry>(currentOptionCount);

        for (int k = 0; k < currentOptionCount && temp.Count > 0; k++)
        {
            float total = 0f;
            for (int i = 0; i < temp.Count; i++) total += Mathf.Max(0f, temp[i].weight);

            VoteEntry chosen;
            if (total <= 0f) chosen = temp[Random.Range(0, temp.Count)];
            else
            {
                float r = Random.value * total;
                chosen = temp[^1]; // fallback last
                for (int i = 0; i < temp.Count; i++)
                {
                    r -= Mathf.Max(0f, temp[i].weight);
                    if (r <= 0f) { chosen = temp[i]; break; }
                }
            }

            picked.Add(chosen);
            temp.Remove(chosen);
        }

        while (picked.Count < currentOptionCount) picked.Add(eligible[Random.Range(0, eligible.Count)]);

        int assignCount = Mathf.Min(currentOptionCount, picked.Count, currentOptions.Length);
        for (int i = 0; i < assignCount; i++) currentOptions[i] = picked[i];
        for (int i = assignCount; i < kMaxOptions; i++) currentOptions[i] = null;
    }

    private static VoteEntry PickWeightedAmong(IReadOnlyList<VoteEntry> opts)
    {
        // Not used in current flow; kept for completeness
        float total = 0f;
        for (int i = 0; i < opts.Count; i++) total += Mathf.Max(0f, opts[i]?.weight ?? 0f);
        if (total <= 0f) total = opts.Count;
        float r = Random.value * total;
        for (int i = 0; i < opts.Count; i++)
        {
            float w = Mathf.Max(0f, opts[i]?.weight ?? 0f);
            if (total <= opts.Count) w = 1f; // uniform if all non-positive
            r -= w;
            if (r <= 0f) return opts[i];
        }
        return opts[^1];
    }

    private List<VoteEntry> BuildEligible(bool excludeRecent)
    {
        var list = new List<VoteEntry>(pool.Count);
        foreach (var e in pool)
        {
            if (e == null || e.weight <= 0f) continue;
            if (excludeRecent && recentHistorySize > 0 && recentIds.Contains(e.id)) continue;
            list.Add(e);
        }
        return list;
    }

    private void PushRecent(string id)
    {
        if (recentHistorySize <= 0 || string.IsNullOrEmpty(id)) return;
        recentIds.Enqueue(id);
        while (recentIds.Count > recentHistorySize) recentIds.Dequeue();
    }

    // ---------- Display ----------
    public string GetVoteDisplay()
    {
        if (phase == Phase.Voting)
        {
            string tt = FormatMMSS(TimeRemaining);
            int total = 0;
            for (int i = 0; i < currentOptionCount; i++) total += tallies[i];

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append($"VOTE! ({tt})\n");
            for (int i = 0; i < currentOptionCount; i++)
                sb.Append($"[{voteKeywords[i]}] {currentOptions[i]?.description}\n");
            sb.Append("\n");
            for (int i = 0; i < currentOptionCount; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append($"{voteKeywords[i]} {tallies[i]}");
            }
            sb.Append($"  |  Total: {total}");
            return sb.ToString();
        }
        if (phase == Phase.Cooldown) return $"Next vote in: {FormatMMSS(TimeRemaining)}";
        return "Resolving...";
    }

    private static string FormatMMSS(float seconds)
    {
        var t = TimeSpan.FromSeconds(Mathf.CeilToInt(seconds));
        return $"{t.Minutes:00}:{t.Seconds:00}";
    }

    // ---------- Internals ----------
    private void EnsureArraySizes()
    {
        if (currentOptions == null || currentOptions.Length != kMaxOptions) currentOptions = new VoteEntry[kMaxOptions];
        if (tallies == null || tallies.Length != kMaxOptions) tallies = new int[kMaxOptions];
    }

    private void NormalizePoolIds()
    {
        for (int i = 0; i < pool.Count; i++) pool[i]?.EnsureId();
    }
}
