using UnityEngine;
using UnityEngine.UI;

public class AlertCellAppearance : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Vector2 _restPosition;
    private Image _panelImage;
    private float _defaultAlpha = 1f;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform != null)
            _restPosition = _rectTransform.anchoredPosition;
        _panelImage = GetComponent<Image>();
        if (_panelImage != null)
        {
            _defaultAlpha = _panelImage.color.a;
        }
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
    }

    private void OnDisable()
    {
        if (_rectTransform != null)
            _rectTransform.anchoredPosition = _restPosition;
        if (_panelImage != null)
        {
            Color c = _panelImage.color;
            c.a = _defaultAlpha;
            _panelImage.color = c;
        }
    }

    public void SetHovered(bool hovered)
    {
        if (_panelImage == null) return;
        Color c = _panelImage.color;
        c.a = hovered ? _defaultAlpha : 0f;
        _panelImage.color = c;
    }
}
