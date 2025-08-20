using TMPro;
using UnityEngine;

public class VoteTextUI : MonoBehaviour
{
    [Tooltip("Assign your VoteManager here. If left empty, will search in scene.")]
    public VoteManager manager;

    public TextMeshProUGUI tmp;

    private void Awake()
    {
        if (!manager) manager = FindAnyObjectByType<VoteManager>();
    }

    private void OnEnable()
    {
        // Optional: subscribe for immediate refreshes
        if (manager != null)
        {
            manager.OnVoteStart += HandleUpdate;
            manager.OnVoteTick += HandleUpdate;
            manager.OnCooldownStart += HandleCooldown;
            manager.OnCooldownTick += HandleCooldown;
            manager.OnVoteEnd += HandleVoteEnd;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.OnVoteStart -= HandleUpdate;
            manager.OnVoteTick -= HandleUpdate;
            manager.OnCooldownStart -= HandleCooldown;
            manager.OnCooldownTick -= HandleCooldown;
            manager.OnVoteEnd -= HandleVoteEnd;
        }
    }

    private void Update()
    {
        // Lightweight safety: keep it fresh even without events (in case of external time changes)
        Refresh();
    }

    private void HandleUpdate(VoteEntry[] _, float __) => Refresh();
    private void HandleUpdate(float __, int[] ___) => Refresh();
    private void HandleCooldown(float __) => Refresh();
    private void HandleVoteEnd(VoteEntry __, int[] ___) => Refresh();

    private void Refresh()
    {
        if (!manager || !tmp) return;
        tmp.text = manager.GetVoteDisplay();
    }
}
