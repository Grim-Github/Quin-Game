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
    private const int kOptions = 3;

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
    [NonSerialized] private VoteEntry[] currentOptions = new VoteEntry[kOptions];
    [NonSerialized] private int[] tallies = new int[kOptions];
    private readonly Queue<string> recentIds = new();

    public event Action<float> OnCooldownStart; // duration
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

    private static readonly Regex VoteCmd = new(@"!vote\s*([1-3])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private void OnChatMessage(Chatter chatter)
    {
        if (chatter == null || phase != Phase.Voting) return;
        var msg = chatter.message;
        if (string.IsNullOrEmpty(msg)) return;

        var m = VoteCmd.Match(msg);
        if (!m.Success) return;

        int parsed = m.Groups[1].Value[0] - '1'; // 1..3 -> 0..2
        AcceptVote(parsed);
    }

    public void StartRoundNow() => StartVoting();
    public void ForceResolve() { if (phase == Phase.Voting) Resolve(); }

    public bool AcceptVote(int optionIndex)
    {
        if (phase != Phase.Voting || optionIndex < 0 || optionIndex >= kOptions) return false;
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

    private void EnterCooldown()
    {
        phase = Phase.Cooldown;
        phaseEndTime = Now + cooldownSeconds;
        OnCooldownStart?.Invoke(cooldownSeconds);
    }

    private void StartVoting()
    {
        PickOptionsWeightedWithoutReplacement();

        for (int i = 0; i < kOptions; i++) tallies[i] = 0;

        phase = Phase.Voting;
        phaseEndTime = Now + voteSeconds;
        OnVoteStart?.Invoke(currentOptions, voteSeconds);
    }

    private void Resolve()
    {
        phase = Phase.Resolving;

        // Winner by max votes (random among ties). If no votes, do not select.
        VoteEntry winner = PickWinnerFromTallies();

        if (winner != null)
        {
            try { winner.onWin?.Invoke(); }
            catch (Exception e) { Debug.LogException(e, this); }

            OnOptionInvoked?.Invoke(winner);
        }

        OnVoteEnd?.Invoke(winner, (int[])tallies.Clone());

        // Record shown ids
        for (int i = 0; i < kOptions; i++) PushRecent(currentOptions[i]?.id);

        EnterCooldown();
    }

    private VoteEntry PickWinnerFromTallies()
    {
        int max = Mathf.Max(tallies[0], Mathf.Max(tallies[1], tallies[2]));
        if (max <= 0) return null;

        List<int> tied = new(kOptions);
        for (int i = 0; i < kOptions; i++) if (tallies[i] == max) tied.Add(i);
        return currentOptions[tied[Random.Range(0, tied.Count)]];
    }

    // ---------- Selection ----------
    private void PickOptionsWeightedWithoutReplacement()
    {
        // Build eligible (exclude recent if possible)
        List<VoteEntry> eligible = BuildEligible(excludeRecent: true);
        if (eligible.Count < kOptions) eligible = BuildEligible(excludeRecent: false);

        if (eligible.Count == 0)
        {
            // Safe placeholders
            for (int i = 0; i < kOptions; i++)
                currentOptions[i] = new VoteEntry { description = "No options", weight = 1f, id = Guid.NewGuid().ToString("N") };
            return;
        }

        // Draw without replacement by weight
        var temp = new List<VoteEntry>(eligible);
        var picked = new List<VoteEntry>(kOptions);

        for (int k = 0; k < kOptions && temp.Count > 0; k++)
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

        while (picked.Count < kOptions) picked.Add(eligible[Random.Range(0, eligible.Count)]);

        int assignCount = Mathf.Min(kOptions, picked.Count, currentOptions.Length);
        for (int i = 0; i < assignCount; i++) currentOptions[i] = picked[i];
    }

    private static VoteEntry PickWeightedAmong(IReadOnlyList<VoteEntry> opts)
    {
        float w0 = Mathf.Max(0f, opts[0]?.weight ?? 0f);
        float w1 = Mathf.Max(0f, opts[1]?.weight ?? 0f);
        float w2 = Mathf.Max(0f, opts[2]?.weight ?? 0f);
        float total = w0 + w1 + w2;
        if (total <= 0f) { w0 = w1 = w2 = 1f; total = 3f; }

        float r = Random.value * total;
        int idx = 0;
        if ((r -= w0) > 0f) { idx = 1; if ((r -= w1) > 0f) idx = 2; }
        return opts[idx];
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
            int total = tallies[0] + tallies[1] + tallies[2];
            return
                $"VOTE! ({tt})\n" +
                $"[1] {currentOptions[0]?.description}\n" +
                $"[2] {currentOptions[1]?.description}\n" +
                $"[3] {currentOptions[2]?.description}\n\n" +
                $"Votes: 1) {tallies[0]}  2) {tallies[1]}  3) {tallies[2]}  |  Total: {total}";
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
        if (currentOptions == null || currentOptions.Length != kOptions) currentOptions = new VoteEntry[kOptions];
        if (tallies == null || tallies.Length != kOptions) tallies = new int[kOptions];
    }

    private void NormalizePoolIds()
    {
        for (int i = 0; i < pool.Count; i++) pool[i]?.EnsureId();
    }
}

