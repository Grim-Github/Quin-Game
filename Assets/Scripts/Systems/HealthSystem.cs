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
    [SerializeField] public float regenRate = 0f;

    [Header("Armor (Small-hit mitigation)")]
    [Tooltip("Flat armor rating. More armor = more mitigation on small hits.")]
    [SerializeField] public float armor = 0f;
    [Tooltip("How quickly mitigation falls off as the hit gets bigger. Higher = big hits bypass sooner.")]
    [SerializeField] private float armorScaling = 10f;
    [Tooltip("Cap the maximum mitigation fraction (0..0.95). 0.8 = up to 80% reduction on tiny hits.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float maxMitigation = 0.8f;

    [Header("UI")]
    [Tooltip("Optional slider to show current health.")]
    [SerializeField] public Slider healthSlider;
    [SerializeField] public TextMeshProUGUI healthText;

    [Header("Stats Display")]
    [SerializeField] private TextMeshProUGUI statsTextPrefab;
    [SerializeField] private Transform uiParent;
    [TextArea][SerializeField] public string extraTextField;
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] private Sprite iconSprite;

    [Header("SFX")]
    [SerializeField] private GameObject[] bloodPool;
    [SerializeField] private AudioClip damageClip;
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private GameObject bloodSFX;

    [Header("Loot")]
    [Tooltip("Weighted loot table. On death we roll once and spawn the result (if any).")]
    [SerializeField] private LootTable2D loot;

    [Header("Hit Flash")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitColor = new Color(1f, 0.5f, 0.5f, 1f);
    [SerializeField] private float hitFlashDuration = 0.1f;

    [Header("Damage Popup")]
    [Tooltip("Prefab with a TextMeshPro or TextMeshProUGUI to display damage taken.")]
    [SerializeField] private GameObject damagePopupPrefab;
    [Tooltip("Offset from entity position when spawning damage popup.")]
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 1f, 0f);

    private AudioSource soundSource;
    [HideInInspector] public float currentHealth;
    private bool isInvulnerable;

    private Color _originalColor;
    private bool _hasOriginalColor;
    private Coroutine _flashRoutine;
    private int lastDamageTaken = 0;
    private Snappy2DController movementController;

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

        if (statsTextPrefab != null && uiParent != null)
        {
            statsTextInstance = Instantiate(statsTextPrefab, uiParent);
            statsTextInstance.text = "";
            iconImage = statsTextInstance.GetComponentInChildren<Image>(true);
            if (iconImage != null && iconSprite != null)
                iconImage.sprite = iconSprite;
        }
    }

    private void Start()
    {
        // Reset to max at start
        ResetHealth();
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

    public void UpdateStatsText()
    {
        if (statsTextInstance != null)
        {
            float referenceDamage = Mathf.Max(1, lastDamageTaken); // avoid divide-by-zero visualization
            float currentMitigation = 0f;

            if (armor > 0f && armorScaling > 0f)
            {
                currentMitigation = armor / (armor + armorScaling * referenceDamage);
                currentMitigation = Mathf.Min(currentMitigation, maxMitigation);
            }

            string text =
                $"<b>{transform.name} Stats</b>\n" +
                $"Max Health: {maxHealth}\n" +
                $"Current Health: {CurrentHealth}\n" +
                $"Regen Rate: {regenRate:F2}/s\n" +
                $"Armor: {armor}\n" +
                $"Approx Mitigation: {(currentMitigation * 100f):F1}% " +
                $"(Max: {(maxMitigation * 100f):F0}%)\n" +
                $"Last Hit Damage: {lastDamageTaken}\n";

            if (movementController != null)
            {
                // Assumes these properties exist in your controller.
                text +=
                    $"Move Speed: {movementController.MoveSpeed:F2}\n" +
                    $"Dash Speed: {movementController.DashSpeed:F2}\n" +
                    $"Dash Duration: {movementController.DashDuration:F2}s\n" +
                    $"Dash Cooldown: {movementController.DashCooldown:F2}s\n";
            }

            text += extraTextField;

            statsTextInstance.text = text;
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || isInvulnerable || currentHealth <= 0) return;

        int mitigated = ApplyArmor(amount);
        if (mitigated <= 0) return;

        lastDamageTaken = mitigated;
        GetComponent<DPSChecker>()?.RegisterDamage(mitigated);

        currentHealth = Mathf.Clamp(currentHealth - mitigated, 0, maxHealth);
        SyncSlider();

        // Spawn damage popup
        if (damagePopupPrefab != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + popupOffset, Quaternion.identity);
            // Try both TMP types
            if (popup.TryGetComponent<TextMeshPro>(out var tmpWorld))
            {
                tmpWorld.text = mitigated.ToString();
            }
            else if (popup.TryGetComponent<TextMeshProUGUI>(out var tmpUI))
            {
                tmpUI.text = mitigated.ToString();
            }
            else
            {
                var childWorld = popup.GetComponentInChildren<TextMeshPro>();
                if (childWorld != null) childWorld.text = mitigated.ToString();
                var childUI = popup.GetComponentInChildren<TextMeshProUGUI>();
                if (childUI != null) childUI.text = mitigated.ToString();
            }
        }

        if (bloodSFX != null)
        {
            Quaternion randomRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            Instantiate(bloodSFX, transform.position, randomRotation);
        }

        if (soundSource != null && damageClip != null)
            soundSource.PlayOneShot(damageClip);

        if (spriteRenderer != null)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRedCoroutine());
        }

        if (transform.CompareTag("Player"))
        {
            FindAnyObjectByType<OrthoScrollZoom>()?.CameraShake(0.1f, 2f);
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
        if (spriteRenderer == null) yield break;
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
        // Deaths SFX
        if (deathClip != null)
        {
            GameObject tempAudio = new GameObject("DeathSound");
            var tempSource = tempAudio.AddComponent<AudioSource>();
            tempSource.clip = deathClip;
            tempSource.Play();
            Destroy(tempAudio, deathClip.length);
        }

        // Roll & spawn from loot table (if present)
        if (loot != null)
        {
            try
            {
                loot.RollAndSpawn();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SimpleHealth] Loot roll failed on {name}: {e.Message}");
            }
        }

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

    public void IncreaseMaxHealth(int amount)
    {
        if (amount <= 0) return;

        maxHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth); // heal gained HP only
        SyncSlider();
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
