using UnityEngine;

/// <summary>
/// Plays a random UI sound from an array of clips using the player's AudioSource (found via tag).
/// </summary>
public class UIAudioPlayer : MonoBehaviour
{
    [Header("Audio Clips")]
    [Tooltip("List of audio clips to randomly choose from.")]
    public AudioClip[] clips;

    [Header("Player Tag")]
    [Tooltip("Tag used to find the player's AudioSource.")]
    public string playerTag = "Player";

    private AudioSource playerAudio;

    private void Awake()
    {
        // Find player by tag and get AudioSource
        var playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            playerAudio = playerObj.GetComponent<AudioSource>();
            if (playerAudio == null)
                Debug.LogError($"[UIAudioPlayer] No AudioSource found on object with tag '{playerTag}'.");
        }
        else
        {
            Debug.LogError($"[UIAudioPlayer] No GameObject found with tag '{playerTag}'.");
        }
    }

    /// <summary>
    /// Plays a random clip from the array via the player's AudioSource.
    /// </summary>
    public void PlayRandomClip()
    {
        if (playerAudio == null || clips == null || clips.Length == 0)
        {
            Debug.LogWarning("[UIAudioPlayer] Missing AudioSource or clips.");
            return;
        }

        var clip = clips[Random.Range(0, clips.Length)];
        if (clip != null)
            playerAudio.PlayOneShot(clip);
    }
}
