using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum ArrowDirection
{
    Up,
    Down,
    Left,
    Right,
    Auto
}

public class ArrowBounceAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private ArrowDirection direction = ArrowDirection.Auto;
    [SerializeField] private float bounceDistance = 20f;
    [SerializeField] private float bounceDuration = 0.5f;
    [SerializeField] private Ease bounceEase = Ease.OutQuad;

    private RectTransform _rectTransform;
    private Image _image;
    private Vector2 _originalPosition;
    private Sequence _bounceSequence;
    private ArrowDirection _detectedDirection;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null)
        {
            _rectTransform = gameObject.AddComponent<RectTransform>();
        }
        _image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (_rectTransform != null)
        {
            _originalPosition = _rectTransform.anchoredPosition;
        }

        if (direction == ArrowDirection.Auto)
        {
            DetectDirection();
        }
        else
        {
            _detectedDirection = direction;
        }

        StartBounceAnimation();
    }

    private void OnDisable()
    {
        StopBounceAnimation();
        
        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = _originalPosition;
        }
    }

    private void DetectDirection()
    {
        if (_rectTransform == null) return;

        float rotationZ = _rectTransform.localEulerAngles.z;
        
        if (rotationZ >= 315f || rotationZ < 45f)
        {
            _detectedDirection = ArrowDirection.Up;
        }
        else if (rotationZ >= 45f && rotationZ < 135f)
        {
            _detectedDirection = ArrowDirection.Right;
        }
        else if (rotationZ >= 135f && rotationZ < 225f)
        {
            _detectedDirection = ArrowDirection.Down;
        }
        else if (rotationZ >= 225f && rotationZ < 315f)
        {
            _detectedDirection = ArrowDirection.Left;
        }
        else
        {
            _detectedDirection = ArrowDirection.Up;
        }

        if (_image != null && _image.sprite != null)
        {
            Vector2 size = _rectTransform.sizeDelta;
            if (size.x > size.y)
            {
                if (Mathf.Abs(rotationZ - 0f) < 10f || Mathf.Abs(rotationZ - 180f) < 10f)
                {
                    _detectedDirection = ArrowDirection.Left;
                }
                else
                {
                    _detectedDirection = ArrowDirection.Right;
                }
            }
            else
            {
                if (Mathf.Abs(rotationZ - 90f) < 10f || Mathf.Abs(rotationZ - 270f) < 10f)
                {
                    _detectedDirection = ArrowDirection.Down;
                }
                else
                {
                    _detectedDirection = ArrowDirection.Up;
                }
            }
        }
    }

    private void StartBounceAnimation()
    {
        if (_rectTransform == null) return;

        StopBounceAnimation();

        Vector2 bounceVector = GetBounceVector();
        Vector2 targetPosition = _originalPosition + bounceVector;

        _bounceSequence = DOTween.Sequence();
        _bounceSequence.Append(_rectTransform.DOAnchorPos(targetPosition, bounceDuration)
            .SetEase(bounceEase));
        _bounceSequence.Append(_rectTransform.DOAnchorPos(_originalPosition, bounceDuration)
            .SetEase(bounceEase));
        _bounceSequence.SetLoops(-1, LoopType.Restart);
        _bounceSequence.SetUpdate(true);
    }

    private Vector2 GetBounceVector()
    {
        switch (_detectedDirection)
        {
            case ArrowDirection.Up:
                return new Vector2(0f, bounceDistance);
            case ArrowDirection.Down:
                return new Vector2(0f, -bounceDistance);
            case ArrowDirection.Left:
                return new Vector2(-bounceDistance, 0f);
            case ArrowDirection.Right:
                return new Vector2(bounceDistance, 0f);
            default:
                return new Vector2(0f, bounceDistance);
        }
    }

    private void StopBounceAnimation()
    {
        if (_bounceSequence != null && _bounceSequence.IsActive())
        {
            _bounceSequence.Kill();
            _bounceSequence = null;
        }
    }

    private void OnDestroy()
    {
        StopBounceAnimation();
    }
}
