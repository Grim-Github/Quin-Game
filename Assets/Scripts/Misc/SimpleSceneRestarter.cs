using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleSceneRestarter : MonoBehaviour
{
    public KeyCode restartKey = KeyCode.R; // Key to restart the scene

    void Update()
    {
        if (Input.GetKeyDown(restartKey))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
