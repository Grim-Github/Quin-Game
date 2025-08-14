using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(AudioSource))]
public class FootstepController2D : MonoBehaviour
{
    [Header("Footstep Settings")]
    [Tooltip("List of footstep sounds to play.")]
    public AudioClip[] footstepClips;

    [Tooltip("Minimum speed to trigger footsteps.")]
    public float minVelocity = 0.1f;

    [Tooltip("Time between each footstep sound in seconds.")]
    public float stepInterval = 0.4f;

    [Range(0f, 1f)]
    [Tooltip("Random pitch variation for each step.")]
    public float pitchVariation = 0.1f;

    private Rigidbody2D rb;
    private AudioSource audioSource;
    private float stepTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        // Check movement
        if (rb.linearVelocity.magnitude > minVelocity)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                PlayFootstep();
                stepTimer = stepInterval;
            }
        }
        else
        {
            // Reset timer when stopped so it plays instantly on next move
            stepTimer = 0f;
        }
    }

    private void PlayFootstep()
    {
        if (footstepClips.Length == 0) return;

        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(clip);
    }
}
