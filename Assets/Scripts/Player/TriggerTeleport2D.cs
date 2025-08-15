using UnityEngine;

public class UnityEventsRandom : MonoBehaviour
{
    public void Test(string textToTest)
    {
        Debug.Log(textToTest);
    }
}

[RequireComponent(typeof(Collider2D))]
public class TriggerTeleport2D : UnityEventsRandom
{
    [Header("Teleport Settings")]
    [SerializeField] private Transform destination;
    [SerializeField] private TriggerTeleport2D linkedTeleport;
    [SerializeField] private float linkCooldown = 0.2f;

    [Header("Layer Filtering")]
    [Tooltip("Only objects on these layers will be teleported.")]
    [SerializeField] private LayerMask teleportLayers = (1 << 6) | (1 << 7); // default: layers 6 & 7

    [Header("Sound")]
    [SerializeField] private AudioClip teleportSFX;

    private bool cooldownActive = false;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (cooldownActive || destination == null) return;

        // Check layer eligibility
        if ((teleportLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // Teleport
        var rb = other.attachedRigidbody;
        if (rb != null)
        {
            rb.position = destination.position;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        else
        {
            other.transform.position = destination.position;
        }

        // Play sound
        PlaySound();

        // Trigger cooldown on linked teleport
        if (linkedTeleport != null)
        {
            linkedTeleport.StartCooldown(linkCooldown);
        }
    }

    private void PlaySound()
    {
        if (teleportSFX == null) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var playerAudio = player.GetComponent<AudioSource>();
            if (playerAudio != null)
            {
                playerAudio.PlayOneShot(teleportSFX);
            }

        }
    }

    public void StartCooldown(float duration)
    {
        if (!gameObject.activeInHierarchy) return;
        StopAllCoroutines();
        StartCoroutine(CooldownRoutine(duration));
    }

    private System.Collections.IEnumerator CooldownRoutine(float duration)
    {
        cooldownActive = true;
        yield return new WaitForSeconds(duration);
        cooldownActive = false;
    }
}
