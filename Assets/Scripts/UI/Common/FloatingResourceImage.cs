using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class FloatingResourceImage : MonoBehaviour
{
    [SerializeField] private float moveDistance = 1f;
    [SerializeField] private float duration = 1f;
    [SerializeField] private Image resourceImage;
    [SerializeField] private TMP_Text text;
    
    private RectTransform _rectTransform;
    
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void Play(ResourceType resourceType)
    {
        // Kill any existing tweens to prevent conflicts
        transform.DOKill();
        if (resourceImage != null)
        {
            resourceImage.DOKill();
        }
        if (text != null)
        {
            text.DOKill();
        }
        
        // Null checks
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }
        
        if (_rectTransform == null || resourceImage == null || text == null) 
        {
            gameObject.SetActive(false);
            return;
        }

        Sprite resourceIcon = ResourceManager.Instance?.GetResourceIcon(resourceType);
        if (resourceIcon != null)
        {
            resourceImage.sprite = resourceIcon;
        }
        
        Color imageColor = Color.white;
        imageColor.a = 1f;
        resourceImage.color = imageColor;
        text.alpha = 1f;

        Vector2 startPos = transform.position;

        transform.DOMoveY(startPos.y + moveDistance, duration).SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (gameObject != null)
                {
                    gameObject.SetActive(false);
                }
            });

        if (resourceImage != null)
        {
            resourceImage.DOFade(0, duration);
        }
        if (text != null)
        {
            text.DOFade(0, duration);
        }
    }
}
