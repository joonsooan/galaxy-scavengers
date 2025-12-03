using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ResourceInfoCellClickable : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image resourceImage;
    [SerializeField] private TMP_Text resourceAmountText;

    private ResourceType _resourceType;
    private InventorySystem _inventorySystem;

    public ResourceType ResourceType => _resourceType;

    public void Initialize(ResourceType type, InventorySystem inventorySystem)
    {
        _resourceType = type;
        _inventorySystem = inventorySystem;
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
        }
        else
        {
            resourceImage.sprite = null;
            resourceImage.enabled = false;
        }
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
        if (_inventorySystem == null || ResourceManager.Instance == null)
        {
            return;
        }

        bool isShiftClick = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        
        if (isShiftClick)
        {
            int maxStack = _inventorySystem.GetMaxStackAmount(_resourceType);
            _inventorySystem.TryAddResourceToInventory(_resourceType, maxStack, moveAll: true);
        }
        else
        {
            int maxStack = _inventorySystem.GetMaxStackAmount(_resourceType);
            _inventorySystem.TryAddResourceToInventory(_resourceType, maxStack, moveAll: false);
        }
    }
}

