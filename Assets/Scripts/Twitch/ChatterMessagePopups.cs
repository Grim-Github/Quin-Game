using Lexone.UnityTwitchChat;
using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ChatterMessagePopups : MonoBehaviour
{
    [Header("Message Popup Settings")]
    [Tooltip("Prefab with a TextMeshProUGUI component.")]
    public GameObject messagePrefab;
    public AudioClip messageSound;

    [Tooltip("Offset from the chatter's position where the prefab will be spawned.")]
    public Vector3 spawnOffset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("Random pitch range for message sound.")]
    [Range(0.5f, 2f)] public float minPitch = 0.9f;
    [Range(0.5f, 2f)] public float maxPitch = 1.1f;

    [Tooltip("Delay between each letter in seconds.")]
    [Range(0.001f, 0.2f)] public float letterDelay = 0.03f;

    private AudioSource enemySource;

    private void Awake()
    {
        enemySource = GetComponent<AudioSource>();
        if (enemySource == null)
        {
            enemySource = gameObject.AddComponent<AudioSource>();
            enemySource.playOnAwake = false;
        }
    }

    private void Update()
    {
        if (Time.timeScale == 0)
        {
            // If the game is paused, stop any playing sound
            if (enemySource != null && enemySource.isPlaying)
            {
                enemySource.Stop();
            }
        }
    }

    private void OnEnable()
    {
        if (IRC.Instance != null)
            IRC.Instance.OnChatMessage += OnChatMessage;
    }

    private void OnDisable()
    {
        if (IRC.Instance != null)
            IRC.Instance.OnChatMessage -= OnChatMessage;
    }

    private void OnChatMessage(Chatter chatter)
    {
        if (chatter.tags.displayName == transform.name)
        {
            ShowMessage(chatter.message);
        }
    }

    public void ShowMessage(string message)
    {
        if (!messagePrefab)
        {
            Debug.LogWarning("[ChatterMessagePopups] No messagePrefab assigned.");
            return;
        }

        Vector3 spawnPos = transform.position + spawnOffset;
        GameObject msgInstance = Instantiate(messagePrefab, spawnPos, Quaternion.identity);

        TextMeshPro tmp = msgInstance.GetComponent<TextMeshPro>();
        if (tmp)
        {
            Color tmpColor = Color.gray; tmpColor.a = 0.4f;

            tmp.color = tmpColor;

            var popup = tmp.GetComponent<DamagePopup2D>();
            if (popup != null)
                popup.lifetime = message.Length * 0.2f;

            StartCoroutine(TypewriterEffect(tmp, message));
        }
        else
        {
            Debug.LogWarning("[ChatterMessagePopups] Spawned prefab has no TextMeshPro component.");
        }
    }

    private IEnumerator TypewriterEffect(TextMeshPro tmp, string message)
    {
        tmp.text = string.Empty;

        // Play sound if assigned
        if (messageSound != null && enemySource != null)
        {
            enemySource.pitch = Random.Range(minPitch, maxPitch);
            enemySource.clip = messageSound;
            enemySource.loop = true; // loop while typing
            enemySource.Play();
        }

        foreach (char c in message)
        {
            tmp.text += c;
            yield return new WaitForSeconds(letterDelay);
        }

        // Stop the sound after typing is finished
        if (enemySource != null && enemySource.isPlaying)
        {
            enemySource.Stop();
            enemySource.pitch = 1f; // reset pitch
            enemySource.loop = false;
        }
    }
}
