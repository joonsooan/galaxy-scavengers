using UnityEngine;
using UnityEngine.UI;

public class UnitHealthBar : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private float yOffset = 1.5f;
    [SerializeField] private float fadeDelay = 5f;
    [SerializeField] private float fadeDuration = 0.5f;

    private Transform _target;
    private Camera _mainCamera;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private float _lastChangeTime;
    private bool _isVisible;
    private const string ObjectUICanvasName = "ObjectUI Canvas";

    private void Awake()
    {
        _mainCamera = Camera.main;
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        _canvasGroup.alpha = 0f;
        _isVisible = false;
    }

    private void FindAndSetParentCanvas()
    {
        if (_canvas != null) return;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
        {
            GameObject canvasObj = GameObject.Find(ObjectUICanvasName);
            if (canvasObj != null)
            {
                _canvas = canvasObj.GetComponent<Canvas>();
            }
        }

        if (_canvas != null && transform.parent != _canvas.transform)
        {
            transform.SetParent(_canvas.transform, false);
        }
    }

    public void Initialize(Transform target)
    {
        _target = target;
        FindAndSetParentCanvas();
        transform.localScale = Vector3.one;
        gameObject.SetActive(true);
        _lastChangeTime = Time.time;
        ShowImmediate();
    }

    public void SetHealth(float ratio)
    {
        if (healthSlider != null)
        {
            healthSlider.value = Mathf.Clamp01(ratio);
        }

        _lastChangeTime = Time.time;

        if (ratio >= 1f)
        {
            HideImmediate();
        }
        else
        {
            ShowImmediate();
        }
    }

    public void SetColor(Color color)
    {
        if (fillImage != null)
        {
            fillImage.color = color;
        }
    }

    public void SetYOffset(float offset)
    {
        yOffset = offset;
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            Destroy(gameObject);
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        transform.position = _target.position + new Vector3(0f, yOffset, 0f);

        if (_isVisible && Time.time - _lastChangeTime >= fadeDelay)
        {
            float t = (Time.time - (_lastChangeTime + fadeDelay)) / fadeDuration;
            if (t >= 1f)
            {
                _canvasGroup.alpha = 0f;
                _isVisible = false;
            }
            else if (t > 0f)
            {
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            }
        }
    }

    private void ShowImmediate()
    {
        _canvasGroup.alpha = 1f;
        _isVisible = true;
    }

    private void HideImmediate()
    {
        _canvasGroup.alpha = 0f;
        _isVisible = false;
    }

    public void Destroy()
    {
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }
}

