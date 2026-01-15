using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class FloatingResourceImage : MonoBehaviour
{
    [SerializeField] private float moveDistance = 1f;
    [SerializeField] private float duration = 1f;
    [SerializeField] private Image resourceImage;
    
    private RectTransform _rectTransform;
    
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void Play(ResourceType resourceType)
    {
        if (_rectTransform == null || resourceImage == null) return;

        Sprite resourceIcon = ResourceManager.Instance?.GetResourceIcon(resourceType);
        if (resourceIcon != null)
        {
            resourceImage.sprite = resourceIcon;
        }
        
        Color imageColor = Color.white;
        imageColor.a = 1f;
        resourceImage.color = imageColor;

        Vector2 startPos = _rectTransform.position;

        _rectTransform.DOAnchorPosY(startPos.y + moveDistance, duration).SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
            });

        resourceImage.DOFade(0, duration);
    }
}
