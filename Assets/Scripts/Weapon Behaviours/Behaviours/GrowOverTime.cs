using UnityEngine;

public class GrowOverTime : MonoBehaviour
{
    [Tooltip("How fast the object grows per second.")]
    public float growthRate = 1f;

    private void Update()
    {
        transform.localScale += Vector3.one * growthRate * Time.deltaTime;
    }
}
