using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BaseResourceInfoCellClickable : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image resourceImage;
    [SerializeField] private TMP_Text resourceAmountText;

    private ResourceType _resourceType;
    private BaseInventorySystem _baseInventorySystem;

    public ResourceType ResourceType => _resourceType;

    public void Initialize(ResourceType type, BaseInventorySystem baseInventorySystem)
    {
        _resourceType = type;
        _baseInventorySystem = baseInventorySystem;
        UpdateIcon();
        UpdateAmount(0);
    }

    private void UpdateIcon()
    {
        if (resourceImage == null) return;

        Sprite resourceIcon = GetResourceIcon();
        if (resourceIcon != null)
        {
            resourceImage.sprite = resourceIcon;
            resourceImage.enabled = true;
        }
        else
        {
            resourceImage.sprite = null;
            resourceImage.enabled = false;
        }
    }
    
    private Sprite GetResourceIcon()
    {
        // Try BaseResourceDataManager first (for base scene)
        if (BaseResourceDataManager.Instance != null)
        {
            return BaseResourceDataManager.Instance.GetResourceIcon(_resourceType);
        }
        
        // Fallback to ResourceManager (for game scene compatibility)
        if (ResourceManager.Instance != null)
        {
            return ResourceManager.Instance.GetResourceIcon(_resourceType);
        }
        
        return null;
    }

    public void UpdateAmount(int amount)
    {
        bool shouldShow = amount > 0;
        gameObject.SetActive(shouldShow);

        if (shouldShow)
        {
            if (resourceAmountText != null)
            {
                resourceAmountText.text = amount.ToString();
            }

            UpdateIcon();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_baseInventorySystem == null || BaseInventoryManager.Instance == null)
        {
            return;
        }

        bool isShiftClick = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        
        if (isShiftClick)
        {
            int maxStack = _baseInventorySystem.GetMaxStackAmount(_resourceType);
            _baseInventorySystem.TryAddResourceToInventory(_resourceType, maxStack, moveAll: true);
        }
        else
        {
            int maxStack = _baseInventorySystem.GetMaxStackAmount(_resourceType);
            _baseInventorySystem.TryAddResourceToInventory(_resourceType, maxStack, moveAll: false);
        }
    }
}

