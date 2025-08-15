using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TriggerTeleport2D : MonoBehaviour
{
    [SerializeField] private Transform destination;
    [SerializeField] private TriggerTeleport2D linkedTeleport;
    [SerializeField] private float linkCooldown = 0.2f;

    private bool cooldownActive = false;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (cooldownActive || destination == null) return;

        // Teleport the thing that entered
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

        // Trigger cooldown on linked teleport to avoid instant back-teleport
        if (linkedTeleport != null)
        {
            linkedTeleport.StartCooldown(linkCooldown);
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
