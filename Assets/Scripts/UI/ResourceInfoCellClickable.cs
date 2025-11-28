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

        UpdateAmount(0);
    }

    public void UpdateAmount(int amount)
    {
        if (resourceAmountText != null)
        {
            resourceAmountText.text = amount.ToString();
        }

        if (ResourceManager.Instance != null)
        {
            Sprite resourceIcon = ResourceManager.Instance.GetResourceIcon(_resourceType);
            if (resourceImage != null)
            {
                resourceImage.sprite = resourceIcon;
            }
        }
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

