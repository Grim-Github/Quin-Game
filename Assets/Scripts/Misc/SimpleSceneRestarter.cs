using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleSceneRestarter : MonoBehaviour
{
    public KeyCode restartKey = KeyCode.R; // Key to restart the scene

    void Update()
    {
        if (Input.GetKeyDown(restartKey))
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
