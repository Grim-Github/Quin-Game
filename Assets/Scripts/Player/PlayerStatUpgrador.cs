using UnityEngine;

public class PlayerStatUpgrador : MonoBehaviour
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
