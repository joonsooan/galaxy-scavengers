using UnityEngine;

public class NotifierIcon : MonoBehaviour
{
    [Header("Heartbeat Animation Settings")]
    [SerializeField] private float heartbeatDuration = 0.8f;
    [SerializeField] private float scaleUpAmount = 1.3f;
    [SerializeField] private float scaleDownAmount = 0.9f;
    [SerializeField] private float scaleUp2Amount = 1.15f;

    private static float _sharedPhase;
    private RectTransform _rectTransform;
    private Vector3 _originalScale;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform != null)
        {
            _originalScale = _rectTransform.localScale;
            SetPivotToCenter();
        }
        else
        {
            _originalScale = transform.localScale;
        }
    }

    private void OnDisable()
    {
        Transform targetTransform = _rectTransform != null ? (Transform)_rectTransform : transform;
        if (targetTransform != null)
            targetTransform.localScale = _originalScale;
    }

    private void SetPivotToCenter()
    {
        if (_rectTransform == null) return;

        Vector2 size = _rectTransform.rect.size;
        if (size.x <= 0 || size.y <= 0) return;

        Vector2 oldPivot = _rectTransform.pivot;
        Vector2 deltaPivot = oldPivot - new Vector2(0.5f, 0.5f);
        Vector2 deltaPosition = new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);

        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.anchoredPosition -= deltaPosition;
    }

    private void LateUpdate()
    {
        _sharedPhase = (Time.unscaledTime % heartbeatDuration) / heartbeatDuration;
        ApplyScaleForPhase(_sharedPhase);
    }

    private void ApplyScaleForPhase(float phase)
    {
        Transform targetTransform = _rectTransform != null ? (Transform)_rectTransform : transform;
        if (targetTransform == null) return;

        float scaleMultiplier;
        if (phase < 0.25f)
        {
            float t = phase / 0.25f;
            t = 1f - (1f - t) * (1f - t);
            scaleMultiplier = Mathf.Lerp(1f, scaleUpAmount, t);
        }
        else if (phase < 0.4f)
        {
            float t = (phase - 0.25f) / 0.15f;
            t = t * t;
            scaleMultiplier = Mathf.Lerp(scaleUpAmount, scaleDownAmount, t);
        }
        else if (phase < 0.6f)
        {
            float t = (phase - 0.4f) / 0.2f;
            t = 1f - (1f - t) * (1f - t);
            scaleMultiplier = Mathf.Lerp(scaleDownAmount, scaleUp2Amount, t);
        }
        else
        {
            float t = (phase - 0.6f) / 0.4f;
            t = 1f - (1f - t) * (1f - t);
            scaleMultiplier = Mathf.Lerp(scaleUp2Amount, 1f, t);
        }

        targetTransform.localScale = _originalScale * scaleMultiplier;
    }
}
