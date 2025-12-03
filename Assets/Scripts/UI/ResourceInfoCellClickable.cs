using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ResourceInfoCellClickable : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image resourceImage;
    [SerializeField] private TMP_Text resourceAmountText;
    [SerializeField] private TMP_Text resourceTypeText; // Optional: to display resource type name

    private ResourceType _resourceType;
    private InventorySystem _inventorySystem;

    public ResourceType ResourceType => _resourceType;

    public void Initialize(ResourceType type, InventorySystem inventorySystem)
    {
        _resourceType = type;
        _inventorySystem = inventorySystem;

        if (resourceTypeText != null)
        {
            resourceTypeText.text = type.ToString();
        }

        // Update icon immediately when initializing
        UpdateIcon();
        UpdateAmount(0);
    }

    private void UpdateIcon()
    {
        if (resourceImage == null) return;

        if (ResourceManager.Instance != null)
        {
            Sprite resourceIcon = ResourceManager.Instance.GetResourceIcon(_resourceType);
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
        else
        {
            resourceImage.sprite = null;
            resourceImage.enabled = false;
        }
    }

    public void UpdateAmount(int amount)
    {
        if (resourceAmountText != null)
        {
            resourceAmountText.text = amount.ToString();
        }

        // Ensure icon is updated when amount changes
        UpdateIcon();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_inventorySystem == null || ResourceManager.Instance == null)
        {
            return;
        }

        bool isShiftClick = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        
        if (isShiftClick)
        {
            // Shift + Click: Move ALL available resources
            int maxStack = _inventorySystem.GetMaxStackAmount(_resourceType);
            _inventorySystem.TryAddResourceToInventory(_resourceType, maxStack, moveAll: true);
        }
        else
        {
            // Single Click: Move maxStackAmount (or remaining if less)
            int maxStack = _inventorySystem.GetMaxStackAmount(_resourceType);
            _inventorySystem.TryAddResourceToInventory(_resourceType, maxStack, moveAll: false);
        }
    }
}

