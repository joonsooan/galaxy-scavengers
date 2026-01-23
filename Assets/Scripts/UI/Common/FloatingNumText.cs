using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingNumText : MonoBehaviour
{
    [SerializeField] private float moveDistance = 1f;
    [SerializeField] private float duration = 1f;
    
    private TMP_Text _text;
    private RectTransform _rectTransform;
    
    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _rectTransform = GetComponent<RectTransform>();
    }

    public void Play(string message, Color color)
    {
        if (_text == null || _rectTransform == null) return;

        _text.text = message;
        _text.color = color;
        _text.alpha = 1f;

        Vector2 startPos = _rectTransform.anchoredPosition;

        _rectTransform.DOAnchorPosY(startPos.y + moveDistance, duration).SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
            });

        _text.DOFade(0, duration);
    }
}