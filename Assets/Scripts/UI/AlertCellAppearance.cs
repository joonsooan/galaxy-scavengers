using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class AlertCellAppearance : MonoBehaviour
{
    [SerializeField] private float dropOffset = 80f;
    [SerializeField] private float dropDuration = 0.4f;
    [SerializeField] private Ease dropEase = Ease.OutQuad;
    [SerializeField] private float pulseAlphaDuration = 0.15f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    private RectTransform _rectTransform;
    private Vector2 _restPosition;
    private Image _panelImage;
    private float _defaultAlpha = 1f;
    private Sequence _sequence;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform != null)
            _restPosition = _rectTransform.anchoredPosition;
        _panelImage = GetComponent<Image>();
        if (_panelImage != null)
            _defaultAlpha = _panelImage.color.a;
    }

    private void OnEnable()
    {
        if (_rectTransform == null) return;
        _restPosition = _rectTransform.anchoredPosition;
        if (_panelImage != null)
        {
            Color c = _panelImage.color;
            c.a = _defaultAlpha;
            _panelImage.color = c;
        }
        _sequence?.Kill();
        _sequence = null;
    }

    private void OnDisable()
    {
        _sequence?.Kill();
        _sequence = null;
        if (_rectTransform != null)
            _rectTransform.anchoredPosition = _restPosition;
        if (_panelImage != null)
        {
            Color c = _panelImage.color;
            c.a = _defaultAlpha;
            _panelImage.color = c;
        }
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
    }

    public void SetHovered(bool hovered)
    {
        if (_panelImage == null) return;
        _sequence?.Kill();
        _sequence = null;
        Color c = _panelImage.color;
        c.a = hovered ? _defaultAlpha : 0f;
        _panelImage.color = c;
    }
}
