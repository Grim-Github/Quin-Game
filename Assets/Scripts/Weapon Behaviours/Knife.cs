using TMPro;
using UnityEngine;
using UnityEngine.UI; // Added for Image

public class Knife : MonoBehaviour
{
    [Header("AOE Damage")]
    [SerializeField] private float radius = 1f;
    [SerializeField] public int damage = 10;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private int maxTargetsPerTick = 0;

    [Header("Lifesteal")]
    [Range(0f, 1f)]
    [SerializeField] private float lifestealPercent = 0.25f;

    [Header("Criticals")]
    [Range(0f, 1f)] public float critChance = 0f;
    [Min(1f)] public float critMultiplier = 2f;

    [Header("Upgrades")]
    public WeaponUpgrades nextUpgrade;

    [Header("SFX")]
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private AudioClip stabClip;
    [SerializeField] private GameObject slashEffect;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statsTextPrefab;
    [SerializeField] private Transform uiParent;
    [TextArea][SerializeField] private string extraTextField;
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] private Sprite weaponSprite;

    private TextMeshProUGUI statsTextInstance;
    private Image iconImage;
    private AudioSource shootSource;
    private SimpleHealth parentHealth;
    private WeaponTick wt;
    private PowerUpChooser powerUpChooser;

    private void Awake()
    {
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();
        if (nextUpgrade != null && powerUpChooser != null)
        {
            powerUpChooser.powerUps.Add(nextUpgrade.Upgrade);
        }

        shootSource = GetComponent<AudioSource>();

        if (transform.parent != null)
            parentHealth = transform.parent.parent.GetComponent<SimpleHealth>();

        if (statsTextPrefab != null && uiParent != null)
        {
            statsTextInstance = Instantiate(statsTextPrefab, uiParent);
            statsTextInstance.text = "";

            // Find Image in parent and assign sprite
            iconImage = statsTextInstance.GetComponentInChildren<Image>(true);
            if (iconImage != null && weaponSprite != null)
                iconImage.sprite = weaponSprite;

        }

        wt = GetComponent<WeaponTick>();
        UpdateStatsText();
    }

    private void Update()
    {
        UpdateStatsText();
    }

    private void UpdateStatsText()
    {
        if (statsTextInstance != null)
        {
            statsTextInstance.text =
                $"<b>{transform.name} Stats</b>\n" +
                $"Radius: {radius:F2}\n" +
                $"Damage: {damage}\n" +
                $"Attack Delay: {wt.interval:F1}s\n" +
                $"Lifesteal: {(lifestealPercent * 100f):F0}%\n" +
                $"Crit: {(critChance * 100f):F0}% x{critMultiplier:F2}\n" +
                (maxTargetsPerTick > 0 ? $"Max Targets: {maxTargetsPerTick}\n" : "") +
                extraTextField;
        }
    }

    public void OnKnifeTick()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, targetMask);
        shootSource?.PlayOneShot(shootClip);

        if (hits.Length == 0)
        {
            if (slashEffect != null)
            {
                Vector3 fxPos = transform.position + (Vector3)(Random.insideUnitCircle * 1f);
                Instantiate(slashEffect, fxPos, Quaternion.identity);
            }
            return;
        }

        int targetsHit = 0;
        foreach (var col in hits)
        {
            if (col == null || col.gameObject == gameObject) continue;

            shootSource?.PlayOneShot(stabClip);

            if (slashEffect != null)
                Instantiate(slashEffect, col.transform.position, Quaternion.identity);

            SimpleHealth health = col.GetComponent<SimpleHealth>();
            if (health != null && health.IsAlive && !health.IsInvulnerable)
            {
                bool isCrit = Random.value < Mathf.Clamp01(critChance);
                float mult = isCrit ? Mathf.Max(1f, critMultiplier) : 1f;
                int dealt = Mathf.RoundToInt(damage * mult);

                health.TakeDamage(dealt);

                if (lifestealPercent > 0f && parentHealth != null && parentHealth.IsAlive)
                {
                    int healAmount = Mathf.RoundToInt(dealt * lifestealPercent);
                    parentHealth.Heal(healAmount);
                }

                targetsHit++;
                if (maxTargetsPerTick > 0 && targetsHit >= maxTargetsPerTick)
                    break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
