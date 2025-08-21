using UnityEngine;

public class PlayerStatUpgrader : MonoBehaviour
{
    public PowerUp statPowerUp;

    private PowerUpChooser powerUpChooser;

    private void Awake()
    {
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();
        if (statPowerUp != null && powerUpChooser != null)
        {
            powerUpChooser.powerUps.Add(statPowerUp);
        }
    }

}
