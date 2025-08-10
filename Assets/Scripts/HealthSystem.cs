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

    [Header("Stats Display")]
    [SerializeField] private TextMeshProUGUI statsTextPrefab;
    [SerializeField] private Transform uiParent;
    [TextArea][SerializeField] private string extraTextField;
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] private Sprite iconSprite;

    [Header("SFX")]
    [SerializeField] private GameObject[] bloodPool;
    [SerializeField] private AudioClip damageClip;
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private GameObject bloodSFX;
    [SerializeField] private GameObject dropItem;

    [Header("Hit Flash")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitColor = new Color(1f, 0.5f, 0.5f, 1f);
    [SerializeField] private float hitFlashDuration = 0.1f;

    private AudioSource soundSource;
    private float currentHealth;
    private bool isInvulnerable;

    private Color _originalColor;
    private bool _hasOriginalColor;
    private Coroutine _flashRoutine;
    private int lastDamageTaken = 0;
    private Snappy2DController movementController; // cache movement script

    private TextMeshProUGUI statsTextInstance;
    private Image iconImage;

    public bool IsAlive => currentHealth > 0f;
    public bool IsInvulnerable => isInvulnerable;
    public int CurrentHealth => Mathf.RoundToInt(currentHealth);
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        if (startingHealth <= 0) startingHealth = maxHealth;
        currentHealth = Mathf.Clamp(startingHealth, 0, maxHealth);
        SyncSlider();
        movementController = GetComponent<Snappy2DController>();
        soundSource = GetComponent<AudioSource>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            _originalColor = spriteRenderer.color;
            _hasOriginalColor = true;
        }

        // Create stats display
        if (statsTextPrefab != null && uiParent != null)
        {
            statsTextInstance = Instantiate(statsTextPrefab, uiParent);
            statsTextInstance.text = "";
            iconImage = statsTextInstance.GetComponentInChildren<Image>(true);
            if (iconImage != null && iconSprite != null)
                iconImage.sprite = iconSprite;
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

        UpdateStatsText();
    }

    private void UpdateStatsText()
    {
        if (statsTextInstance != null)
        {
            float referenceDamage = lastDamageTaken;
            float currentMitigation = 0f;

            if (armor > 0f && armorScaling > 0f)
            {
                currentMitigation = armor / (armor + armorScaling * referenceDamage);
                currentMitigation = Mathf.Min(currentMitigation, maxMitigation);
            }

            // Build the health/armor part
            string text =
                $"<b>{transform.name} Stats</b>\n" +
                $"Max Health: {maxHealth}\n" +
                $"Current Health: {CurrentHealth}\n" +
                $"Regen Rate: {regenRate:F2}/s\n" +
                $"Armor: {armor}\n" +
                $"Approx Mitigation: {(currentMitigation * 100f):F1}% " +
                $"(Max: {(maxMitigation * 100f):F0}%)\n" +
                $"Last Hit Damage: {lastDamageTaken}\n";

            // Add Snappy2DController stats if available
            if (movementController != null)
            {
                text +=
                    $"Move Speed: {movementController.MoveSpeed:F2}\n" +
                    $"Dash Speed: {movementController.DashSpeed:F2}\n" +
                    $"Dash Duration: {movementController.DashDuration:F2}s\n" +
                    $"Dash Cooldown: {movementController.DashCooldown:F2}s\n";
            }

            // Append any extra text field
            text += extraTextField;

            statsTextInstance.text = text;
        }
    }



    public void TakeDamage(int amount)
    {
        if (amount <= 0 || isInvulnerable || currentHealth <= 0) return;

        int mitigated = ApplyArmor(amount);
        if (mitigated <= 0) return;
        lastDamageTaken = mitigated; // store how much damage was actually dealt
        currentHealth = Mathf.Clamp(currentHealth - mitigated, 0, maxHealth);
        SyncSlider();

        if (bloodSFX != null)
            Instantiate(bloodSFX, transform.position, Quaternion.identity);

        if (soundSource != null && damageClip != null)
            soundSource.PlayOneShot(damageClip);

        if (spriteRenderer != null)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRedCoroutine());
        }

        if (transform.CompareTag("Player"))
        {
            FindAnyObjectByType<OrthoScrollZoom>().CameraShake(0.1f, 2f);
        }

        if (currentHealth <= 0)
            Die();
        else
            StartCoroutine(InvulnerabilityCoroutine());
    }

    private int ApplyArmor(int rawDamage)
    {
        if (rawDamage <= 0 || armor <= 0f || armorScaling <= 0f) return rawDamage;
        float m = armor / (armor + armorScaling * rawDamage);
        if (maxMitigation > 0f) m = Mathf.Min(m, maxMitigation);
        float reduced = rawDamage * (1f - m);
        return Mathf.Max(0, Mathf.RoundToInt(reduced));
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
            healthText.text = $"{Mathf.RoundToInt(currentHealth)}/{maxHealth}";
    }

    public void GiveArmor(float amount)
    {
        if (amount == 0f) return;
        armor = Mathf.Max(0f, armor + amount);
    }

    private System.Collections.IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityDuration);
        isInvulnerable = false;
    }
}
