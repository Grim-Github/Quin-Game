using UnityEngine;
using UnityEngine.Events;

public class TimedEvent : MonoBehaviour
{
    [Tooltip("How often (in seconds) the event should trigger.")]
    [SerializeField] private float interval = 1f;

    [Tooltip("Event to invoke every interval.")]
    public UnityEvent onTimedEvent;

    private float timer;

    public void InstantiateAtPostion(GameObject prefab)
    {
        Instantiate(prefab, transform.position, Quaternion.identity);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer -= interval; // Keeps leftover time for consistent intervals
            onTimedEvent?.Invoke();
        }
    }
}
