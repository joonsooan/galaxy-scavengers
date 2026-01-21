using DG.Tweening;
using UnityEngine;

public class FloatAnimation : MonoBehaviour
{
    [Header("Float Settings")]
    [SerializeField] private float floatDistance = 20f;
    [SerializeField] private float floatDuration = 2f;
    [SerializeField] private Ease floatEase = Ease.InOutSine;
    
    private RectTransform _rectTransform;
    private Vector3 _originalPosition;
    private Tween _floatTween;
    
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _originalPosition = transform.localPosition;
    }
    
    private void OnEnable()
    {
        StartFloat();
    }
    
    private void OnDisable()
    {
        StopFloat();
    }
    
    private void OnDestroy()
    {
        StopFloat();
    }
    
    private void StartFloat()
    {
        StopFloat();
        
        _rectTransform.localPosition = _originalPosition + (Vector3.down * floatDistance);

        _floatTween = _rectTransform.DOLocalMoveY(_originalPosition.y + floatDistance, floatDuration)
            .SetEase(floatEase)
            .SetLoops(-1, LoopType.Yoyo);
    }
    
    private void StopFloat()
    {
        if (_floatTween != null && _floatTween.IsActive())
        {
            _floatTween.Kill();
        }
        _floatTween = null;
    }
}
