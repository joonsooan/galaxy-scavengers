using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class NotifierIcon : MonoBehaviour
{
    [Header("Heartbeat Animation Settings")]
    [SerializeField] private float heartbeatDuration = 0.6f;
    [SerializeField] private float scaleUpAmount = 1.3f;
    [SerializeField] private float scaleDownAmount = 0.9f;
    [SerializeField] private float scaleUp2Amount = 1.15f;
    [SerializeField] private Ease heartbeatEase = Ease.OutQuad;
    
    private RectTransform _rectTransform;
    private Vector3 _originalScale;
    private Sequence _heartbeatSequence;
    
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform != null)
        {
            _originalScale = _rectTransform.localScale;
            
            // Set pivot to center for scaling from center, adjusting position to maintain visual position
            SetPivotToCenter();
        }
        else
        {
            _originalScale = transform.localScale;
        }
    }
    
    private void SetPivotToCenter()
    {
        if (_rectTransform == null) return;
        
        Vector2 size = _rectTransform.rect.size;
        Vector2 oldPivot = _rectTransform.pivot;
        Vector2 deltaPivot = oldPivot - new Vector2(0.5f, 0.5f);
        Vector2 deltaPosition = new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);
        
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.anchoredPosition -= deltaPosition;
    }
    
    private void OnEnable()
    {
        StartHeartbeat();
    }
    
    private void OnDisable()
    {
        StopHeartbeat();
    }
    
    private void OnDestroy()
    {
        StopHeartbeat();
    }
    
    private void StartHeartbeat()
    {
        StopHeartbeat();
        
        Transform targetTransform = _rectTransform != null ? (Transform)_rectTransform : transform;
        if (targetTransform == null) return;
        
        _heartbeatSequence = DOTween.Sequence();
        
        _heartbeatSequence.Append(targetTransform.DOScale(_originalScale * scaleUpAmount, heartbeatDuration * 0.25f)
            .SetEase(Ease.OutQuad));
        
        _heartbeatSequence.Append(targetTransform.DOScale(_originalScale * scaleDownAmount, heartbeatDuration * 0.15f)
            .SetEase(Ease.InQuad));
        
        _heartbeatSequence.Append(targetTransform.DOScale(_originalScale * scaleUp2Amount, heartbeatDuration * 0.2f)
            .SetEase(Ease.OutQuad));
        
        _heartbeatSequence.Append(targetTransform.DOScale(_originalScale, heartbeatDuration * 0.4f)
            .SetEase(heartbeatEase));
        
        _heartbeatSequence.SetLoops(-1, LoopType.Restart);
    }
    
    private void StopHeartbeat()
    {
        if (_heartbeatSequence != null && _heartbeatSequence.IsActive())
        {
            _heartbeatSequence.Kill();
            _heartbeatSequence = null;
        }
    }
}
