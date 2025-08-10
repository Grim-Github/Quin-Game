using TMPro;
using UnityEngine;

public class ChatterStats : MonoBehaviour
{
    public TextMeshProUGUI nameGUI;
    public int power = 0;

    private void Start()
    {
        GetComponent<SimpleHealth>().maxHealth += power * 20; GetComponent<SimpleHealth>().Heal(power * 20);
        GetComponent<SimpleHealth>().SyncSlider();
        Debug.Log(power);
    }
}
