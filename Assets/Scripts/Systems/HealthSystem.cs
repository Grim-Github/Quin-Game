using NaughtyAttributes;
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

    [BoxGroup("Health")][SerializeField] public int maxHealth = 100;
    [BoxGroup("Health")]
    [Tooltip("If <=0, starts at maxHealth.")]
    [SerializeField] private int startingHealth = 100;
    [BoxGroup("Health")][SerializeField] private int reservedHealth = 0;

    [BoxGroup("Invulnerability")]
    [Tooltip("Seconds of invulnerability after taking damage.")]
    [SerializeField] private float invulnerabilityDuration = 1f;

    [BoxGroup("Regeneration")]
    [Tooltip("Health regenerated per second. Can be fractional.")]
    [SerializeField] public float regenRate = 0f;

    [BoxGroup("Armor (Small-hit mitigation)")]
    [Tooltip("Flat armor rating. More armor = more mitigation on small hits.")]
    [SerializeField] public float armor = 0f;
    [BoxGroup("Armor (Small-hit mitigation)")]
    [Tooltip("How quickly mitigation falls off as the hit gets bigger. Higher = big hits bypass sooner.")]
    [SerializeField] private float armorScaling = 10f;
    [BoxGroup("Armor (Small-hit mitigation)")]
    [Tooltip("Cap the maximum mitigation fraction (0..0.95). 0.8 = up to 80% reduction on tiny hits.")]
    [Range(0f, 0.95f)][SerializeField] private float maxMitigation = 0.8f;

    [BoxGroup("Evasion (Chance to completely dodge small hits)")]
    [SerializeField] public float evasion = 0f;
    [BoxGroup("Evasion (Chance to completely dodge small hits)")]
    [SerializeField] private float evasionScaling = 10f;
    [BoxGroup("Evasion (Chance to completely dodge small hits)")]
    [SerializeField, Range(0f, 0.95f)] private float maxEvasion = 0.8f;

    [BoxGroup("Resistances (% damage reduced AFTER armor)")]
    [Tooltip("0..0.95 fraction of damage reduced for each type.")]
    [Range(0f, 0.95f)] public float fireResist = 0f;
    [BoxGroup("Resistances (% damage reduced AFTER armor)")]
    [Range(0f, 0.95f)] public float coldResist = 0f;
    [BoxGroup("Resistances (% damage reduced AFTER armor)")]
    [Range(0f, 0.95f)] public float lightningResist = 0f;
    [BoxGroup("Resistances (% damage reduced AFTER armor)")]
    [Range(0f, 0.95f)] public float poisonResist = 0f;

    [BoxGroup("UI")]
    [Tooltip("Optional slider to show current health.")]
    [SerializeField] public Slider healthSlider;
    [BoxGroup("UI")][SerializeField] public Slider reservedSlider;
    [BoxGroup("UI")][SerializeField] public TextMeshProUGUI healthText;

    [BoxGroup("Stats Display")]
    [Tooltip("Prefab root GameObject that contains a TextMeshProUGUI somewhere in its children.")]
    [SerializeField] private GameObject statsTextPrefab;
    [BoxGroup("Stats Display")][SerializeField] private Transform uiParent;
    [BoxGroup("Stats Display")][TextArea][SerializeField] public string extraTextField;
    [BoxGroup("Stats Display")]
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] private Sprite iconSprite;

    [BoxGroup("SFX")][SerializeField] private Volume playerVolume;
    [BoxGroup("SFX")][SerializeField] private GameObject[] deathObjects;
    [BoxGroup("SFX")][SerializeField] private AudioClip[] damageClip;
    [BoxGroup("SFX")][SerializeField] private AudioClip[] deathClip;
    [BoxGroup("SFX")][SerializeField] private GameObject bloodSFX;

    [BoxGroup("Loot")]
    [Tooltip("Weighted loot table. On death we roll once and spawn the result (if any).")]
    [SerializeField] private LootTable2D loot;

    [BoxGroup("Hit Flash")][SerializeField] private SpriteRenderer spriteRenderer;
    [BoxGroup("Hit Flash")][SerializeField] private Color hitColor = new Color(1f, 0.5f, 0.5f, 1f);
    [BoxGroup("Hit Flash")][SerializeField] private float hitFlashDuration = 0.1f;

    [BoxGroup("Damage Popup")]
    [Tooltip("Prefab with a TextMeshPro or TextMeshProUGUI to display damage taken.")]
    [SerializeField] private GameObject damagePopupPrefab;
    [BoxGroup("Damage Popup")]
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
    private readonly System.Text.StringBuilder _statsBuilder = new System.Text.StringBuilder(256);

    // Cached components for performance
    private DPSChecker _dpsChecker;
    private StatusEffectSystem _statusEffectSystem;
    private OrthoScrollZoom _orthoScrollZoom;

    // Stats UI (now matches Knife.cs pattern)
    // Split into separate parts instead of one giant text block
    [HideInInspector] public TextMeshProUGUI healthStatsText;
    [HideInInspector] public TextMeshProUGUI defenseStatsText;
    [HideInInspector] public TextMeshProUGUI movementStatsText;

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

        // Cache components
        filter = GetComponent<AudioLowPassFilter>();
        movementController = GetComponent<Snappy2DController>();
        soundSource = GetComponent<AudioSource>();
        _dpsChecker = GetComponent<DPSChecker>();
        _statusEffectSystem = GetComponent<StatusEffectSystem>();
        _orthoScrollZoom = FindAnyObjectByType<OrthoScrollZoom>(); // Note: Still searches scene once. Consider a singleton or service locator for manager-type objects.

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
            // Health section
            var go1 = Instantiate(statsTextPrefab, uiParent);
            healthStatsText = go1.GetComponentInChildren<TextMeshProUGUI>(true);

            // Defense section
            var go2 = Instantiate(statsTextPrefab, uiParent);
            defenseStatsText = go2.GetComponentInChildren<TextMeshProUGUI>(true);

            // Movement section
            var go3 = Instantiate(statsTextPrefab, uiParent);
            movementStatsText = go3.GetComponentInChildren<TextMeshProUGUI>(true);

            // Clear initial
            if (healthStatsText != null) healthStatsText.text = string.Empty;
            if (defenseStatsText != null) defenseStatsText.text = string.Empty;
            if (movementStatsText != null) movementStatsText.text = string.Empty;

            // Icon stays optional
            var iconObj = go1.transform.Find("Icon");
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
            int oldHealthInt = CurrentHealth;
            currentHealth = Mathf.Min(currentHealth + regenRate * Time.deltaTime, maxHealth);
            SyncSlider();
            if (CurrentHealth != oldHealthInt)
            {
                UpdateStatsText();
            }
        }

        UpdateVolume();
    }

    public void UpdateStatsText()
    {
        float referenceDamage = Mathf.Max(1, lastDamageTaken);

        // HEALTH
        if (healthStatsText != null)
        {
            _statsBuilder.Clear();
            _statsBuilder.AppendLine($"<b>Health</b>");
            _statsBuilder.AppendLine($"Max Health: {maxHealth}");
            _statsBuilder.AppendLine($"Reserved: {reservedHealth}");
            _statsBuilder.AppendLine($"Current: {CurrentHealth}");
            _statsBuilder.AppendLine($"Regen: {regenRate:F2}/s");
            healthStatsText.text = _statsBuilder.ToString();
        }

        // DEFENSE & RESISTANCES
        if (defenseStatsText != null)
        {
            float mitigation = (armor > 0f && armorScaling > 0f)
                ? Mathf.Min(armor / (armor + armorScaling * referenceDamage), maxMitigation)
                : 0f;
            float evasionChance = (evasion > 0f && evasionScaling > 0f)
                ? Mathf.Min(evasion / (evasion + evasionScaling * referenceDamage), maxEvasion)
                : 0f;

            _statsBuilder.Clear();
            _statsBuilder.AppendLine($"<b>Defense</b>");
            _statsBuilder.AppendLine($"Armor: {(int)armor} (Mitigation: {mitigation * 100f:F1}%)");
            _statsBuilder.AppendLine($"Evasion: {(int)evasion} (Chance: {evasionChance * 100f:F1}%)");
            _statsBuilder.AppendLine($"Fire Res: {fireResist * 100f:F0}%");
            _statsBuilder.AppendLine($"Cold Res: {coldResist * 100f:F0}%");
            _statsBuilder.AppendLine($"Lightning Res: {lightningResist * 100f:F0}%");
            _statsBuilder.AppendLine($"Poison Res: {poisonResist * 100f:F0}%");
            _statsBuilder.AppendLine($"Last Hit: {lastDamageTaken} ({lastDamageType})");
            defenseStatsText.text = _statsBuilder.ToString();
        }

        // MOVEMENT
        if (movementStatsText != null && movementController != null)
        {
            _statsBuilder.Clear();
            _statsBuilder.AppendLine($"<b>Movement</b>");
            _statsBuilder.AppendLine($"Move Speed: {movementController.MoveSpeed:F2}");
            _statsBuilder.AppendLine($"Dash Speed: {movementController.DashSpeed:F2}");
            _statsBuilder.AppendLine($"Dash Duration: {movementController.DashDuration:F2}s");
            _statsBuilder.AppendLine($"Dash Cooldown: {movementController.DashCooldown:F2}s");
            movementStatsText.text = _statsBuilder.ToString();
        }
    }

    private void TryApplyAilments(StatusEffectSystem ses, DamageType type, int dmg)
    {
        if (ses == null || dmg <= 0) return;

        float dmgFrac = Mathf.Clamp01((float)dmg / Mathf.Max(1, maxHealth));
        int dotDamage = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.20f));

        const float shockMult = 1;
        const float igniteMult = 1;
        const float poisonMult = 1;
        const float bleedMult = 1;

        float roll = Random.value;

        switch (type)
        {
            case DamageType.Lightning:
                {
                    float chance = Mathf.Clamp01(dmgFrac * shockMult);
                    // Debug.Log($"[Ailment] Lightning hit {dmg} dmg → Shock chance {chance:P1}, roll={roll:F2}");
                    if (roll < chance)
                        ses.AddStatus(StatusEffectSystem.StatusType.Shock, 5f, 1f);
                    break;
                }
            case DamageType.Fire:
                {
                    float chance = Mathf.Clamp01(dmgFrac * igniteMult);
                    // Debug.Log($"[Ailment] Fire hit {dmg} dmg → Ignite chance {chance:P1}, roll={roll:F2}");
                    if (roll < chance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Ignite, 5f, 1f);
                        ses.igniteDamagePerTick = dotDamage;
                    }
                    break;
                }
            case DamageType.Cold:
                {
                    float chance = Mathf.Clamp01(dmgFrac * igniteMult);
                    // Debug.Log($"[Ailment] Cold hit {dmg} dmg → Cold chance {chance:P1}, roll={roll:F2}");
                    if (roll < chance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Frozen, 3f, 1f);
                    }
                    break;
                }
            case DamageType.Poison:
                {
                    float chance = Mathf.Clamp01(dmgFrac * poisonMult);
                    //  Debug.Log($"[Ailment] Poison hit {dmg} dmg → Poison chance {chance:P1}, roll={roll:F2}");
                    if (roll < chance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Poison, 15f, 0.5f);
                        ses.poisonDamagePerTick = 1;
                    }
                    break;
                }
            case DamageType.Physical:
                {
                    float chance = Mathf.Clamp01(dmgFrac * bleedMult);
                    // Debug.Log($"[Ailment] Physical hit {dmg} dmg → Bleed chance {chance:P1}, roll={roll:F2}");
                    if (roll < chance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Bleeding, 5f, 1f);
                        ses.bleedingDamagePerTick = dotDamage;
                    }
                    break;
                }
        }
    }



    // BACK-COMPAT: original signature forwards to Physical damage type
    public void TakeDamage(int amount, bool mitigatable = true)
    {
        TakeDamage(amount, DamageType.Physical, mitigatable);
    }

    // NEW: main overload with type
    public void TakeDamage(int amount, DamageType type = DamageType.Physical, bool mitigatable = true, bool applyAilments = true)
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
        _dpsChecker?.RegisterDamage(dmg);

        //ailments
        if (_statusEffectSystem != null)
        {
            if (_statusEffectSystem.HasStatus(StatusEffectSystem.StatusType.Shock))
                dmg *= 2;
            if (applyAilments)
            {
                TryApplyAilments(_statusEffectSystem, type, dmg);
            }

        }




        currentHealth = Mathf.Clamp(currentHealth - dmg, 0, maxHealth);
        SyncSlider();

        // Damage popup
        if (damagePopupPrefab != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + popupOffset, Quaternion.identity);
            // Determine color based on whether this is the Player
            Color popupColor = transform.CompareTag("Player") ? Color.red : Color.white;

            if (popup.TryGetComponent<TextMeshPro>(out var tmpWorld))
            {
                tmpWorld.text = dmg.ToString();
                tmpWorld.color = popupColor;
            }
            else if (popup.TryGetComponent<TextMeshProUGUI>(out var tmpUI))
            {
                tmpUI.text = dmg.ToString();
                tmpUI.color = popupColor;
            }
            else
            {
                var childWorld = popup.GetComponentInChildren<TextMeshPro>();
                if (childWorld != null)
                {
                    childWorld.text = dmg.ToString();
                    childWorld.color = popupColor;
                }
                var childUI = popup.GetComponentInChildren<TextMeshProUGUI>();
                if (childUI != null)
                {
                    childUI.text = dmg.ToString();
                    childUI.color = popupColor;
                }
            }
        }

        if (bloodSFX != null)
        {
            Quaternion randomRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            Instantiate(bloodSFX, transform.position, randomRotation);
        }

        if (soundSource != null && damageClip != null && damageClip.Length > 0)
        {
            soundSource.pitch = Random.Range(0.9f, 1.1f);
            soundSource.PlayOneShot(damageClip[Random.Range(0, damageClip.Length)]);
            soundSource.pitch = 1f;
        }

        if (spriteRenderer != null)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRedCoroutine());
        }

        if (transform.CompareTag("Player"))
        {
            float shakeStrength = Mathf.Clamp01((float)dmg * 3 / maxHealth); // 0..1 based on % HP lost
            float duration = Mathf.Lerp(0.05f, 0.1f, shakeStrength);          // small to big duration
            float intensity = Mathf.Lerp(0.5f, 3f, shakeStrength);             // small to big intensity
            _orthoScrollZoom?.CameraShake(duration, intensity);
        }

        UpdateStatsText();

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
        UpdateStatsText();
    }

    public void Kill()
    {
        if (currentHealth <= 0) return;
        currentHealth = 0;
        SyncSlider();
        UpdateStatsText();
        Die();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        SyncSlider();
        UpdateStatsText();
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

        if (!gameObject.CompareTag("Player"))
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }

    }

    public void SyncSlider()
    {
        if (reservedSlider != null)
        {
            reservedSlider.minValue = 0;
            reservedSlider.maxValue = Mathf.Max(0, maxHealth);
            reservedSlider.value = Mathf.Clamp(reservedHealth, 0, reservedSlider.maxValue);
        }

        if (healthSlider != null)
        {
            healthSlider.minValue = 0;
            healthSlider.maxValue = Mathf.Max(0, maxHealth);
            // Show actual current health on the slider; reserved is shown separately
            healthSlider.value = Mathf.Clamp(currentHealth, 0, healthSlider.maxValue);
        }

        if (healthText != null)
            healthText.text = $"{Mathf.RoundToInt(currentHealth)}/{maxHealth}";
    }

    public void ReserveLife(int amount)
    {
        if (amount <= 0) return;
        reservedHealth = Mathf.Clamp(reservedHealth + amount, 0, maxHealth);
        IncreaseMaxHealth(-amount);
    }

    public void IncreaseMaxHealth(int amount)
    {
        maxHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        SyncSlider();
        UpdateStatsText();
    }

    public void GiveArmor(float amount)
    {
        if (amount == 0f) return;
        armor = Mathf.Max(0f, armor + amount);
        UpdateStatsText();
    }

    public void GiveEvasion(float amount)
    {
        if (amount == 0f) return;
        evasion = Mathf.Max(0f, evasion + amount);
        UpdateStatsText();
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
        UpdateStatsText();
    }

    #region Public Unity Event Helpers

    // --- Health ---
    public void SetMaxHealth(int value)
    {
        maxHealth = Mathf.Max(1, value);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        SyncSlider();
        UpdateStatsText();
    }

    public void AddMaxHealth(int amount)
    {
        IncreaseMaxHealth(amount);
    }

    public void SetHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        SyncSlider();
        UpdateStatsText();
    }

    public void HealByPercentage(float percentage)
    {
        if (percentage <= 0) return;
        int amountToHeal = Mathf.RoundToInt(maxHealth * percentage / 100f);
        Heal(amountToHeal);
    }

    public void TakeDamageByPercentage(float percentage)
    {
        if (percentage <= 0) return;
        int amountToTake = Mathf.RoundToInt(maxHealth * percentage / 100f);
        TakeDamage(amountToTake, DamageType.Physical, false); // Percentage damage is unmitigatable
    }

    public void SetHealthByPercentage(float percentage)
    {
        currentHealth = Mathf.Clamp(maxHealth * percentage / 100f, 0, maxHealth);
        SyncSlider();
        UpdateStatsText();
    }

    // --- Regeneration ---
    public void AddRegen(float amount)
    {
        regenRate += amount;
        UpdateStatsText();
    }

    public void SetRegen(float value)
    {
        regenRate = value;
        UpdateStatsText();
    }

    // --- Defense ---
    public void SetArmor(float value)
    {
        armor = Mathf.Max(0f, value);
        UpdateStatsText();
    }

    public void SetEvasion(float value)
    {
        evasion = Mathf.Max(0f, value);
        UpdateStatsText();
    }

    // --- Resistances ---
    public void AddFireResist(float amount) => GiveResistance(DamageType.Fire, amount);
    public void SetFireResist(float value) { fireResist = Mathf.Clamp(value, 0f, 0.95f); UpdateStatsText(); }
    public void AddColdResist(float amount) => GiveResistance(DamageType.Cold, amount);
    public void SetColdResist(float value) { coldResist = Mathf.Clamp(value, 0f, 0.95f); UpdateStatsText(); }
    public void AddLightningResist(float amount) => GiveResistance(DamageType.Lightning, amount);
    public void SetLightningResist(float value) { lightningResist = Mathf.Clamp(value, 0f, 0.95f); UpdateStatsText(); }
    public void AddPoisonResist(float amount) => GiveResistance(DamageType.Poison, amount);
    public void SetPoisonResist(float value) { poisonResist = Mathf.Clamp(poisonResist + value, 0f, 0.95f); UpdateStatsText(); }

    // --- Invulnerability ---
    public void SetInvulnerable(float duration)
    {
        if (duration > 0)
        {
            StartCoroutine(InvulnerabilityCoroutineWithDuration(duration));
        }
    }

    private System.Collections.IEnumerator InvulnerabilityCoroutineWithDuration(float duration)
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(duration);
        isInvulnerable = false;
    }

    #endregion

    private System.Collections.IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityDuration);
        isInvulnerable = false;
    }
}
