using UnityEngine;
using UnityEngine.Events;

[AddComponentMenu("Gameplay/Simple Interactable 2D")]
public class SimpleInteractable2D : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Player is found once at runtime using this tag.")]
    public string playerTag = "Player";

    [Header("Interaction")]
    [Tooltip("How close the player must be to interact.")]
    public float interactRange = 1.5f;
    [Tooltip("Key to press (old Input system).")]
    public KeyCode interactKey = KeyCode.E;
    [Tooltip("If true, allows interaction only once.")]
    public bool interactOnce = false;

    [Header("Events")]
    public UnityEvent onEnterRange;
    public UnityEvent onExitRange;
    public UnityEvent onInteract;

    Transform _player;
    bool _inRange;
    bool _used;

    void Start()
    {
        FindPlayer();
    }

    public void HealPlayer(int value)
    {
        // Example method to heal player, can be customized
        if (_player != null)
        {
            // Assuming the player has a Health component
            var health = _player.GetComponent<SimpleHealth>();
            if (health != null)
            {
                health.Heal(value); // Heal by 10 points, adjust as needed
            }
        }
    }

    public void PlaySoundAtPlayer(AudioClip sfx)
    {
        GameObject.FindGameObjectWithTag("Player").GetComponent<AudioSource>().PlayOneShot(sfx);
    }

    void Update()
    {
        // Lazy-reacquire if player was not found or got recreated
        if (_player == null) FindPlayer();
        if (_player == null) return;

        float d = Vector2.Distance(transform.position, _player.position);
        bool nowInRange = d <= interactRange;

        if (nowInRange && !_inRange)
        {
            _inRange = true;
            onEnterRange?.Invoke();
        }
        else if (!nowInRange && _inRange)
        {
            _inRange = false;
            onExitRange?.Invoke();
        }

        if (_inRange && !_used && Input.GetKeyDown(interactKey))
        {
            onInteract?.Invoke();
            if (interactOnce) _used = true;
        }
    }

    public void TryInteract()
    {
        // Optional: call from other scripts/UI
        if (_inRange && !_used)
        {
            onInteract?.Invoke();
            if (interactOnce) _used = true;
        }
    }

    void FindPlayer()
    {
        var go = GameObject.FindGameObjectWithTag(playerTag);
        _player = go ? go.transform : null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
