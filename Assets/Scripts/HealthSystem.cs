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

    [Header("Armor (Small-hit mitigation)")]
    [Tooltip("Flat armor rating. More armor = more mitigation on small hits.")]
    [SerializeField] private float armor = 0f;
    [Tooltip("How quickly mitigation falls off as the hit gets bigger. Higher = big hits bypass sooner.")]
    [SerializeField] private float armorScaling = 10f;
    [Tooltip("Cap the maximum mitigation fraction (0..0.95). 0.8 = up to 80% reduction on tiny hits.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float maxMitigation = 0.8f;

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
        if (_hasOriginalColor && spriteRenderer != null)
            spriteRenderer.color = _originalColor;
    }

    private void OnDisable()
    {
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

        // --- Armor mitigation here ---
        int mitigated = ApplyArmor(amount);
        if (mitigated <= 0) return; // fully absorbed -> no effects
        // -----------------------------

        currentHealth = Mathf.Clamp(currentHealth - mitigated, 0, maxHealth);
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

    // Mitigation decays with hit size: small hits reduced a lot; big hits barely reduced.
    private int ApplyArmor(int rawDamage)
    {
        if (rawDamage <= 0 || armor <= 0f || armorScaling <= 0f) return rawDamage;

        // Mitigation fraction m in [0,1): increases with armor, decreases with hit size
        float m = armor / (armor + armorScaling * rawDamage);
        if (maxMitigation > 0f) m = Mathf.Min(m, maxMitigation);

        float reduced = rawDamage * (1f - m);
        int result = Mathf.Max(0, Mathf.RoundToInt(reduced));
        return result;
    }

    private System.Collections.IEnumerator FlashRedCoroutine()
    {
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

        Destroy(gameObject);
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
        yield return new WaitForSeconds(invulnerabilityDuration);
        isInvulnerable = false;
    }
}
