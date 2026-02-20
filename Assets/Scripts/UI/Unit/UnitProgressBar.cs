using UnityEngine;
using UnityEngine.UI;

public class UnitProgressBar : MonoBehaviour
{
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private float yOffset = 1.5f;

    private Transform _targetUnit;
    private Camera _mainCamera;
    private Canvas _canvas;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void FindAndSetParentCanvas()
    {
        if (_canvas != null) return;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null && GameManager.Instance?.uiManager != null)
            _canvas = GameManager.Instance.uiManager.GetObjectUICanvas();

        if (_canvas != null && transform.parent != _canvas.transform)
        {
            transform.SetParent(_canvas.transform, false);
        }
    }

    public void Initialize(Transform targetUnit)
    {
        _targetUnit = targetUnit;
        FindAndSetParentCanvas();

        transform.localScale = Vector3.one; 
        
        gameObject.SetActive(true);
    }

    public void SetProgress(float progress)
    {
        if (progressSlider != null)
        {
            progressSlider.value = Mathf.Clamp01(progress);
        }
    }

    public void SetColor(Color color)
    {
        if (fillImage != null)
        {
            fillImage.color = color;
        }
    }

    private void LateUpdate()
    {
        if (_targetUnit == null) 
        {
            Destroy(gameObject); 
            return;
        }

        if (_mainCamera == null) _mainCamera = Camera.main;

        transform.position = _targetUnit.position + new Vector3(0f, yOffset, 0f);
    }

    public void Destroy()
    {
        if (gameObject != null) Destroy(gameObject);
    }
}