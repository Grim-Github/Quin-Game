using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimpleHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] public int maxHealth = 100;
    [Tooltip("If <=0, starts at maxHealth.")]
    [SerializeField] private int startingHealth = 100;

    [Header("Invulnerability")]
    [Tooltip("Seconds of invulnerability after taking damage.")]
    [SerializeField] private float invulnerabilityDuration = 1f;

    [Header("Regeneration")]
    [Tooltip("Health regenerated per second. Can be fractional.")]
    [SerializeField] private float regenRate = 0f;

    [Header("UI")]
    [Tooltip("Optional slider to show current health.")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("SFX")]
    [SerializeField] private GameObject[] bloodPool; // (not used here but kept for compatibility)
    [SerializeField] private AudioClip damageClip;
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private GameObject bloodSFX;
    [SerializeField] private GameObject dropItem;

    [Header("Hit Flash")]
    [Tooltip("Renderer to flash when taking damage. If null, auto-finds in children.")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("Color used during hit flash (alpha is ignored; original alpha kept).")]
    [SerializeField] private Color hitColor = new Color(1f, 0.5f, 0.5f, 1f);
    [Tooltip("Duration of the hit flash (unscaled time).")]
    [SerializeField] private float hitFlashDuration = 0.1f;

    private AudioSource soundSource;

    private float currentHealth;
    private bool isInvulnerable;

    // Flash bookkeeping
    private Color _originalColor;
    private bool _hasOriginalColor;
    private Coroutine _flashRoutine;

    public bool IsAlive => currentHealth > 0f;
    public bool IsInvulnerable => isInvulnerable;
    public int CurrentHealth => Mathf.RoundToInt(currentHealth);
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        if (startingHealth <= 0) startingHealth = maxHealth;
        currentHealth = Mathf.Clamp(startingHealth, 0, maxHealth);
        SyncSlider();

        soundSource = GetComponent<AudioSource>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            _originalColor = spriteRenderer.color;
            _hasOriginalColor = true;
        }
    }

    private void OnEnable()
    {
        // Ensure color is sane when object re-enables
        if (_hasOriginalColor && spriteRenderer != null)
            spriteRenderer.color = _originalColor;
    }

    private void OnDisable()
    {
        // Restore color if we get disabled during a flash
        if (_hasOriginalColor && spriteRenderer != null)
            spriteRenderer.color = _originalColor;
        _flashRoutine = null;
    }

    private void Update()
    {
        if (regenRate > 0f && IsAlive && currentHealth < maxHealth)
        {
            currentHealth = Mathf.Min(currentHealth + regenRate * Time.deltaTime, maxHealth);
            SyncSlider();
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || isInvulnerable || currentHealth <= 0) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);
        SyncSlider();

        // Blood effect
        if (bloodSFX != null)
            Instantiate(bloodSFX, transform.position, Quaternion.identity);

        // Damage sound
        if (soundSource != null && damageClip != null)
            soundSource.PlayOneShot(damageClip);

        // Flash (uses unscaled time so it always reverts even if timeScale = 0)
        if (spriteRenderer != null)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRedCoroutine());
        }

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvulnerabilityCoroutine());
        }
    }

    private System.Collections.IEnumerator FlashRedCoroutine()
    {
        // Keep original alpha
        var c = spriteRenderer.color;
        var target = new Color(hitColor.r, hitColor.g, hitColor.b, c.a);

        spriteRenderer.color = target;
        yield return new WaitForSecondsRealtime(hitFlashDuration);
        if (_hasOriginalColor) spriteRenderer.color = _originalColor;
        _flashRoutine = null;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        SyncSlider();
    }

    public void Kill()
    {
        if (currentHealth <= 0) return;
        currentHealth = 0;
        SyncSlider();
        Die();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        SyncSlider();
    }

    private void Die()
    {
        // Play death sound from a temporary object so it isn't cut off
        if (deathClip != null)
        {
            GameObject tempAudio = new GameObject("DeathSound");
            var tempSource = tempAudio.AddComponent<AudioSource>();
            tempSource.clip = deathClip;
            tempSource.Play();
            Destroy(tempAudio, deathClip.length);
        }

        if (dropItem != null)
            Instantiate(dropItem, transform.position, Quaternion.identity);

        Destroy(gameObject); // Destroy entity instantly
    }

    public void SyncSlider()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.RoundToInt(currentHealth)}/{maxHealth}";
        }
    }

    private System.Collections.IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityDuration); // obeys timeScale (pause-friendly)
        isInvulnerable = false;
    }
}
