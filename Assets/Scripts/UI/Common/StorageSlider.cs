using UnityEngine;
using UnityEngine.UI;

public class StorageSlider : MonoBehaviour
{
    [SerializeField] private Slider storageSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Color fullColor = Color.red;

    private IStorage _targetStorage;
    private Color _defaultFillColor;

    private void Awake()
    {
        _defaultFillColor = fillImage.color;
    }
    
    public void Initialize(IStorage storage, Vector3 offset)
    {
        _targetStorage = storage;

        if (_targetStorage != null)
        {
            _targetStorage.OnResourceChanged -= HandleResourceChanged;
            _targetStorage.OnResourceChanged += HandleResourceChanged;
            
            Transform targetTransform = (storage as Component)?.transform;
            if (targetTransform != null)
            {
                transform.position = targetTransform.position + offset;
            }
            
            UpdateTotalStorageUI();
        }
    }

    private void OnEnable()
    {
        if (_targetStorage != null)
        {
            _targetStorage.OnResourceChanged += HandleResourceChanged;
            UpdateTotalStorageUI();
        }
    }

    private void OnDisable()
    {
        if (_targetStorage != null)
        {
            _targetStorage.OnResourceChanged -= HandleResourceChanged;
        }
    }
    
    private void HandleResourceChanged(ResourceType type, int current, int max)
    {
        UpdateTotalStorageUI();
    }

    private void UpdateTotalStorageUI()
    {
        if (_targetStorage == null) return;
        
        int totalAmount = _targetStorage.GetTotalCurrentAmount();
        int maxCapacity = _targetStorage.GetMaxCapacity();

        float sliderValue = (maxCapacity == 0) ? 0 : (float)totalAmount / maxCapacity;
        storageSlider.value = sliderValue;

        if (fillImage != null)
        {
            fillImage.color = (totalAmount >= maxCapacity) ? fullColor : _defaultFillColor;
        }
    }
}