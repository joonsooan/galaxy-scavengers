using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceInfoCell : MonoBehaviour
{
    [Header("References")]
    public Image resourceImage;
    public TMP_Text resourceAmount;
    
    private ResourceType _resourceType;
    private int _requiredAmount;
    private Color _originalTextColor;
    private bool _isTrackingResources;
    
    private void OnEnable()
    {
        if (_isTrackingResources)
        {
            ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;
            UpdateColor();
        }
    }
    
    private void OnDisable()
    {
        ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;
    }
    
    public void SetInfo(ResourceType type, int amount)
    {
        SetInfo(type, amount, true);
    }

    public void SetInfoDisplayOnly(ResourceType type, int amount)
    {
        _resourceType = type;
        _requiredAmount = amount;
        _isTrackingResources = false;

        if (resourceAmount != null && _originalTextColor == Color.clear)
            _originalTextColor = resourceAmount.color;

        resourceAmount.text = amount.ToString();
        Sprite resourceIcon = GetResourceIcon(type);
        resourceImage.sprite = resourceIcon != null ? resourceIcon : null;

        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        if (resourceAmount != null)
            resourceAmount.color = _originalTextColor;
    }
    
    public void SetInfo(ResourceType type, int amount, bool rebuildImmediately)
    {
        _resourceType = type;
        _requiredAmount = amount;
        _isTrackingResources = true;
        
        if (resourceAmount != null && _originalTextColor == Color.clear)
        {
            _originalTextColor = resourceAmount.color;
        }
        
        resourceAmount.text = amount.ToString();
        
        Sprite resourceIcon = GetResourceIcon(type);
        if (resourceIcon != null)
        {
            resourceImage.sprite = resourceIcon;
        }
        else
        {
            resourceImage.sprite = null; 
        }
        
        UpdateColor();
        
        if (rebuildImmediately)
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        
        if (gameObject.activeInHierarchy)
        {
            ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;
            ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;
        }
    }
    
    private void OnResourceAmountChanged(ResourceType type, int amount)
    {
        if (type == _resourceType)
        {
            UpdateColor();
        }
    }
    
    private void UpdateColor()
    {
        if (resourceAmount == null || !_isTrackingResources)
        {
            return;
        }
        
        if (ResourceManager.Instance == null)
        {
            resourceAmount.color = _originalTextColor;
            return;
        }
        
        int currentAmount = ResourceManager.Instance.GetResourceAmount(_resourceType);
        
        if (currentAmount < _requiredAmount)
        {
            resourceAmount.color = Color.red;
        }
        else
        {
            resourceAmount.color = _originalTextColor;
        }
    }
    
    private Sprite GetResourceIcon(ResourceType type)
    {
        // Try BaseResourceDataManager first (for base scene)
        BaseResourceDataManager resourceDataManager = FindFirstObjectByType<BaseResourceDataManager>();
        if (resourceDataManager != null)
        {
            return resourceDataManager.GetResourceIcon(type);
        }
        
        // Fallback to ResourceManager (for game scene compatibility)
        if (ResourceManager.Instance != null)
        {
            return ResourceManager.Instance.GetResourceIcon(type);
        }
        
        return null;
    }
}
