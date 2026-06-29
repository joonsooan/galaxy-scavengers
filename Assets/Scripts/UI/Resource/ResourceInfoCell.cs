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
    private bool _tracksToken;
    private bool _tokenCostLabelShowsBalanceFraction = true;
    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        if (_tracksToken)
        {
            GameplayTokenWallet.OnBalanceChanged += OnTokenBalanceChanged;
            RefreshTokenCostDisplay();
            return;
        }

        if (_isTrackingResources)
        {
            ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;
            UpdateColor();
        }
    }

    private void OnDisable()
    {
        ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;
        GameplayTokenWallet.OnBalanceChanged -= OnTokenBalanceChanged;
    }
    
    public void SetInfo(ResourceType type, int amount)
    {
        SetInfo(type, amount, true);
    }

    public void SetInfoDisplayOnly(ResourceType type, int amount)
    {
        _tracksToken = false;
        _resourceType = type;
        _requiredAmount = amount;
        _isTrackingResources = false;

        if (resourceAmount != null && _originalTextColor == Color.clear)
            _originalTextColor = resourceAmount.color;

        resourceAmount.text = amount.ToString();
        Sprite resourceIcon = GetResourceIcon(type);
        if (resourceImage != null)
        {
            resourceImage.enabled = true;
            resourceImage.sprite = resourceIcon != null ? resourceIcon : null;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
        if (resourceAmount != null)
            resourceAmount.color = _originalTextColor;
    }
    
    public void SetInfo(ResourceType type, int amount, bool rebuildImmediately)
    {
        _tracksToken = false;
        _resourceType = type;
        _requiredAmount = amount;
        _isTrackingResources = true;
        
        if (resourceAmount != null && _originalTextColor == Color.clear)
        {
            _originalTextColor = resourceAmount.color;
        }
        
        resourceAmount.text = amount.ToString();
        
        Sprite resourceIcon = GetResourceIcon(type);
        if (resourceImage != null)
        {
            resourceImage.enabled = true;
            if (resourceIcon != null)
            {
                resourceImage.sprite = resourceIcon;
            }
            else
            {
                resourceImage.sprite = null;
            }
        }

        UpdateColor();
        
        if (rebuildImmediately)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
        
        if (gameObject.activeInHierarchy)
        {
            ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;
            ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;
        }
    }

    public void SetTokenCost(int tokenCost, bool rebuildImmediately)
    {
        SetTokenCost(tokenCost, null, rebuildImmediately, true);
    }

    public void SetTokenCost(int tokenCost, Sprite tokenIcon, bool rebuildImmediately)
    {
        SetTokenCost(tokenCost, tokenIcon, rebuildImmediately, true);
    }

    public void SetTokenCost(int tokenCost, Sprite tokenIcon, bool rebuildImmediately, bool labelShowsBalanceFraction)
    {
        _tracksToken = true;
        _isTrackingResources = true;
        _tokenCostLabelShowsBalanceFraction = labelShowsBalanceFraction;
        _requiredAmount = tokenCost;
        _resourceType = ResourceType.None;

        if (resourceAmount != null && _originalTextColor == Color.clear)
        {
            _originalTextColor = resourceAmount.color;
        }

        if (resourceImage != null)
        {
            if (tokenIcon != null)
            {
                resourceImage.sprite = tokenIcon;
                resourceImage.enabled = true;
            }
            else
            {
                resourceImage.sprite = null;
                resourceImage.enabled = false;
            }
        }

        RefreshTokenCostDisplay();

        if (rebuildImmediately)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
        }

        if (gameObject.activeInHierarchy)
        {
            GameplayTokenWallet.OnBalanceChanged -= OnTokenBalanceChanged;
            GameplayTokenWallet.OnBalanceChanged += OnTokenBalanceChanged;
        }
    }

    private void OnTokenBalanceChanged()
    {
        RefreshTokenCostDisplay();
    }

    private void RefreshTokenCostDisplay()
    {
        if (resourceAmount == null || !_tracksToken)
        {
            return;
        }

        int current = GameplayTokenWallet.Instance != null ? GameplayTokenWallet.Instance.Balance : 0;
        resourceAmount.text = _tokenCostLabelShowsBalanceFraction
            ? $"{current}/{_requiredAmount}"
            : _requiredAmount.ToString();

        if (current < _requiredAmount)
        {
            resourceAmount.color = Color.red;
        }
        else
        {
            resourceAmount.color = _originalTextColor;
        }
    }

    private void OnResourceAmountChanged(ResourceType type, int amount)
    {
        if (_tracksToken)
        {
            return;
        }

        if (type == _resourceType)
        {
            UpdateColor();
        }
    }

    private void UpdateColor()
    {
        if (resourceAmount == null || !_isTrackingResources || _tracksToken)
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
            Sprite baseIcon = resourceDataManager.GetResourceIcon(type);
            if (baseIcon != null)
            {
                return baseIcon;
            }
        }
        
        // Fallback to ResourceManager (for game scene compatibility)
        if (ResourceManager.Instance != null)
        {
            return ResourceManager.Instance.GetResourceIcon(type);
        }
        
        return null;
    }
}
