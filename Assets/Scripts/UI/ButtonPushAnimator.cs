using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonPushAnimator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float pressedScale = 0.9f;
    [SerializeField] private float duration = 0.1f;

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(pressedScale, duration).SetEase(Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(1f, duration).SetEase(Ease.OutBack);
    }
}
