using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class SimpleHealth : MonoBehaviour
{
    // NEW: Damage types
    public enum DamageType
    {
        Physical = 0,
        Fire = 1,
        Cold = 2,
        Lightning = 3,
        Poison = 4
    }

    [Header("Health")]
    [SerializeField] public int maxHealth = 100;
    [Tooltip("If <=0, starts at maxHealth.")]
    [SerializeField] private int startingHealth = 100;
    [SerializeField] private int reservedHealth = 0;

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

    [Header("Evasion (Chance to completely dodge small hits)")]
    [SerializeField] public float evasion = 0f; // base evasion stat
    [SerializeField] private float evasionScaling = 10f; // bigger hits reduce chance
    [SerializeField, Range(0f, 0.95f)] private float maxEvasion = 0.8f; // cap

    [Header("Resistances (% damage reduced AFTER armor)")]
    [Tooltip("0..0.95 fraction of damage reduced for each type.")]

    [Range(0f, 0.95f)] public float fireResist = 0f;
    [Range(0f, 0.95f)] public float coldResist = 0f;
    [Range(0f, 0.95f)] public float lightningResist = 0f;
    [Range(0f, 0.95f)] public float poisonResist = 0f;

    [Header("UI")]
    [Tooltip("Optional slider to show current health.")]
    [SerializeField] public Slider healthSlider;
    [SerializeField] public Slider reservedSlider;
    [SerializeField] public TextMeshProUGUI healthText;

    [Header("Stats Display")]
    [Tooltip("Prefab root GameObject that contains a TextMeshProUGUI somewhere in its children.")]
    [SerializeField] private GameObject statsTextPrefab; // CHANGED: now a prefab root like Knife.cs
    [SerializeField] private Transform uiParent;
    [TextArea][SerializeField] public string extraTextField;
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] private Sprite iconSprite;

    [Header("SFX")]
    [SerializeField] private Volume playerVolume;
    [SerializeField] private GameObject[] deathObjects;
    [SerializeField] private AudioClip[] damageClip;
    [SerializeField] private AudioClip[] deathClip;
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
    private DamageType lastDamageType = DamageType.Physical; // NEW: remember last type
    private Snappy2DController movementController;

    // Stats UI (now matches Knife.cs pattern)
    [HideInInspector] public TextMeshProUGUI statsTextInstance;
    private Image iconImage;
    private AudioLowPassFilter filter;

    public bool IsAlive => currentHealth > 0f;
    public bool IsInvulnerable => isInvulnerable;
    public int CurrentHealth => Mathf.RoundToInt(currentHealth);
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        if (startingHealth <= 0) startingHealth = maxHealth;
        currentHealth = Mathf.Clamp(startingHealth, 0, maxHealth);
        SyncSlider();

        filter = GetComponent<AudioLowPassFilter>();

        movementController = GetComponent<Snappy2DController>();
        soundSource = GetComponent<AudioSource>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            _originalColor = spriteRenderer.color;
            _hasOriginalColor = true;
        }

        // Instantiate prefab root (like Knife.cs) and wire up text + icon
        if (statsTextPrefab != null && uiParent != null)
        {
            var go = Instantiate(statsTextPrefab, uiParent);
            statsTextInstance = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (statsTextInstance != null) statsTextInstance.text = string.Empty;

            var iconObj = go.transform.Find("Icon");
            if (iconObj != null)
                iconImage = iconObj.GetComponent<Image>();

            if (iconImage != null && iconSprite != null)
                iconImage.sprite = iconSprite;
        }
    }

    private void Start()
    {
        ResetHealth();
    }

    private void UpdateVolume()
    {
        if (playerVolume != null)
        {
            float hpFraction = currentHealth / Mathf.Max(1f, maxHealth);
            playerVolume.weight = 1f - hpFraction;
        }

        if (filter != null)
        {
            float hpFraction = currentHealth / Mathf.Max(1f, maxHealth);
            float minCutoff = 200f;
            float maxCutoff = 22000f;
            filter.cutoffFrequency = Mathf.Lerp(minCutoff, maxCutoff, hpFraction);
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

        UpdateVolume();
        UpdateStatsText();
    }

    public void UpdateStatsText()
    {
        if (statsTextInstance == null) return;

        float referenceDamage = Mathf.Max(1, lastDamageTaken);
        float currentMitigation = 0f;

        if (armor > 0f && armorScaling > 0f)
        {
            currentMitigation = armor / (armor + armorScaling * referenceDamage);
            currentMitigation = Mathf.Min(currentMitigation, maxMitigation);
        }

        // --- Evasion preview vs a reference hit (uses same referenceDamage as armor) ---
        float currentEvasion = 0f;
        if (evasion > 0f && evasionScaling > 0f)
        {
            currentEvasion = evasion / (evasion + evasionScaling * referenceDamage);
            currentEvasion = Mathf.Min(currentEvasion, maxEvasion);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{transform.name} Stats</b>");
        sb.AppendLine($"Max Health: {maxHealth}");
        sb.AppendLine($"Reserved Health: {reservedHealth}");
        sb.AppendLine($"Current Health: {CurrentHealth}");
        sb.AppendLine($"Regen Rate: {regenRate:F2}/s");

        sb.AppendLine($"Armor: {(int)armor}");
        sb.AppendLine($"Evasion: {(int)evasion}");

        sb.AppendLine($"Approx Mitigation: {(currentMitigation * 100f):F1}% (Max: {(maxMitigation * 100f):F0}%)");
        sb.AppendLine($"Approx Evasion: {(currentEvasion * 100f):F1}% (Max: {(maxEvasion * 100f):F0}%)");

        // NEW: show resistances
        sb.AppendLine($"Resist (Fire/Cold/Lightning/Poison): " +
                      $"{fireResist * 100f:F0}% / {coldResist * 100f:F0}% / {lightningResist * 100f:F0}% / {poisonResist * 100f:F0}%");

        sb.AppendLine($"Last Hit Damage: {lastDamageTaken} ({lastDamageType})");

        if (movementController != null)
        {
            sb.AppendLine($"Move Speed: {movementController.MoveSpeed:F2}");
            sb.AppendLine($"Dash Speed: {movementController.DashSpeed:F2}");
            sb.AppendLine($"Dash Duration: {movementController.DashDuration:F2}s");
            sb.AppendLine($"Dash Cooldown: {movementController.DashCooldown:F2}s");
        }

        if (!string.IsNullOrWhiteSpace(extraTextField))
            sb.AppendLine(extraTextField);

        statsTextInstance.text = sb.ToString();
    }

    // BACK-COMPAT: original signature forwards to Physical damage type
    public void TakeDamage(int amount, bool mitigatable = true)
    {
        TakeDamage(amount, DamageType.Physical, mitigatable);
    }

    // NEW: main overload with type
    public void TakeDamage(int amount, DamageType type = DamageType.Physical, bool mitigatable = true)
    {
        if (amount <= 0 || isInvulnerable || currentHealth <= 0) return;

        int dmg = amount;

        if (mitigatable)
        {
            // Evasion check BEFORE armor/resistance
            if (TryEvade(amount))
            {
                lastDamageTaken = 0;
                lastDamageType = type;

                // Show "Dodged" popup
                if (damagePopupPrefab != null)
                {
                    GameObject popup = Instantiate(damagePopupPrefab, transform.position + popupOffset, Quaternion.identity);
                    var tmpUI = popup.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmpUI != null)
                    {
                        tmpUI.color = Color.white;
                        tmpUI.text = "Dodged";
                    }
                }
                return;
            }


            if (type == DamageType.Physical)
            {
                // Armor (small-hit mitigation) first
                dmg = ApplyArmor(dmg);
            }


            // Then elemental/type resistance
            dmg = ApplyResistance(dmg, type);

        }

        if (dmg <= 0) return;

        lastDamageTaken = dmg;
        lastDamageType = type;
        GetComponent<DPSChecker>()?.RegisterDamage(dmg);

        currentHealth = Mathf.Clamp(currentHealth - dmg, 0, maxHealth);
        SyncSlider();

        // Damage popup
        if (damagePopupPrefab != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + popupOffset, Quaternion.identity);
            if (popup.TryGetComponent<TextMeshPro>(out var tmpWorld))
            {
                tmpWorld.text = dmg.ToString();
            }
            else if (popup.TryGetComponent<TextMeshProUGUI>(out var tmpUI))
            {
                tmpUI.text = dmg.ToString();
            }
            else
            {
                var childWorld = popup.GetComponentInChildren<TextMeshPro>();
                if (childWorld != null) childWorld.text = dmg.ToString();
                var childUI = popup.GetComponentInChildren<TextMeshProUGUI>();
                if (childUI != null) childUI.text = dmg.ToString();
            }
        }

        if (bloodSFX != null)
        {
            Quaternion randomRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            Instantiate(bloodSFX, transform.position, randomRotation);
        }

        if (soundSource != null && damageClip != null && damageClip.Length > 0)
            soundSource.PlayOneShot(damageClip[Random.Range(0, damageClip.Length)]);

        if (spriteRenderer != null)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRedCoroutine());
        }

        if (transform.CompareTag("Player"))
        {
            float shakeStrength = Mathf.Clamp01((float)dmg * 3 / maxHealth); // 0..1 based on % HP lost
            float duration = Mathf.Lerp(0.05f, 0.25f, shakeStrength);          // small to big duration
            float intensity = Mathf.Lerp(0.5f, 3f, shakeStrength);             // small to big intensity
            FindAnyObjectByType<OrthoScrollZoom>()?.CameraShake(duration, intensity);
        }

        if (currentHealth <= 0) Die();
        else if (invulnerabilityDuration > 0) StartCoroutine(InvulnerabilityCoroutine());
    }

    private bool TryEvade(int rawDamage)
    {
        if (rawDamage <= 0 || evasion <= 0f || evasionScaling <= 0f) return false;
        float chance = evasion / (evasion + evasionScaling * rawDamage);
        chance = Mathf.Min(chance, maxEvasion);
        return Random.value < chance;
    }

    private int ApplyArmor(int rawDamage)
    {
        if (rawDamage <= 0 || armor <= 0f || armorScaling <= 0f) return rawDamage;
        float m = armor / (armor + armorScaling * rawDamage);
        if (maxMitigation > 0f) m = Mathf.Min(m, maxMitigation);
        float reduced = rawDamage * (1f - m);
        return Mathf.Max(0, Mathf.RoundToInt(reduced));
    }

    // NEW: per-type resistance after armor
    private int ApplyResistance(int rawDamage, DamageType type)
    {
        if (rawDamage <= 0) return 0;

        float resist = 0f;
        switch (type)
        {
            case DamageType.Fire: resist = fireResist; break;
            case DamageType.Cold: resist = coldResist; break;
            case DamageType.Lightning: resist = lightningResist; break;
            case DamageType.Poison: resist = poisonResist; break;
        }

        resist = Mathf.Clamp(resist, 0f, 0.95f);
        float reduced = rawDamage * (1f - resist);
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
        if (deathObjects.Length > 0)
        {
            foreach (var obj in deathObjects)
            {
                if (obj != null)
                {
                    Instantiate(obj, transform.position, Quaternion.identity);
                }
            }
        }

        if (deathClip != null && deathClip.Length > 0)
        {
            GameObject tempAudio = new GameObject("DeathSound");
            var tempSource = tempAudio.AddComponent<AudioSource>();
            var deathClipSelected = deathClip[Random.Range(0, deathClip.Length)];
            tempSource.clip = deathClipSelected;
            tempSource.Play();
            Destroy(tempAudio, deathClipSelected.length);
        }

        if (loot != null)
        {
            try { loot.RollAndSpawn(); }
            catch (System.Exception e) { Debug.LogWarning($"[SimpleHealth] Loot roll failed on {name}: {e.Message}"); }
        }

        Destroy(gameObject);
    }

    public void SyncSlider()
    {
        if (reservedSlider != null)
        {
            reservedSlider.maxValue = maxHealth;
            reservedSlider.value = reservedHealth;
        }

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth - reservedHealth;
        }

        if (healthText != null)
            healthText.text = $"{Mathf.RoundToInt(currentHealth)}/{maxHealth}";
    }

    public void ReserveLife(int amount)
    {
        if (amount <= 0) return;
        reservedHealth = Mathf.Clamp(reservedHealth + amount, 0, maxHealth);
        IncreaseMaxHealth(-amount);
        SyncSlider();
    }

    public void IncreaseMaxHealth(int amount)
    {
        maxHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        SyncSlider();
    }

    public void GiveArmor(float amount)
    {
        if (amount == 0f) return;
        armor = Mathf.Max(0f, armor + amount);
    }

    public void GiveEvasion(float amount)
    {
        if (amount == 0f) return;
        evasion = Mathf.Max(0f, evasion + amount);
    }

    // OPTIONAL HELPERS: adjust resistances at runtime
    public void GiveResistance(DamageType type, float amount)
    {
        if (Mathf.Approximately(amount, 0f)) return;
        switch (type)
        {
            case DamageType.Fire: fireResist = Mathf.Clamp(fireResist + amount, 0f, 0.95f); break;
            case DamageType.Cold: coldResist = Mathf.Clamp(coldResist + amount, 0f, 0.95f); break;
            case DamageType.Lightning: lightningResist = Mathf.Clamp(lightningResist + amount, 0f, 0.95f); break;
            case DamageType.Poison: poisonResist = Mathf.Clamp(poisonResist + amount, 0f, 0.95f); break;
        }
    }

    private System.Collections.IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityDuration);
        isInvulnerable = false;
    }
}
