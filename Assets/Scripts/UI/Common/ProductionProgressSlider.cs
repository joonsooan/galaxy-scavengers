using UnityEngine;
using UnityEngine.UI;

public class ProductionProgressSlider : MonoBehaviour
{
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private float yOffset = 2.0f;
    
    private Transform _targetTransform;
    private Canvas _canvas;
    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    public void Initialize(Transform target)
    {
        _targetTransform = target;
        FindAndSetParentCanvas();
        UpdateWorldPosition();
        gameObject.SetActive(false);
    }
    
    private void FindAndSetParentCanvas()
    {
        if (_canvas != null) {
            return;
        }

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null && GameManager.Instance != null && GameManager.Instance.uiManager != null) {
            _canvas = GameManager.Instance.uiManager.GetObjectUICanvas();
        }

        if (_canvas != null && transform.parent != _canvas.transform) {
            transform.SetParent(_canvas.transform, false);
            transform.localScale = Vector3.one;
        }
    }
    
    public void SetProgress(float value)
    {
        if (progressSlider != null) {
            progressSlider.value = Mathf.Clamp01(value);
        }
    }

    private void LateUpdate()
    {
        if (_targetTransform == null || !gameObject.activeSelf) {
            return;
        }

        if (_canvas == null) {
            FindAndSetParentCanvas();
        }

        UpdateWorldPosition();
    }

    private void UpdateWorldPosition()
    {
        if (_targetTransform == null) {
            return;
        }

        transform.position = _targetTransform.position + new Vector3(0f, yOffset, 0f);
    }
}
