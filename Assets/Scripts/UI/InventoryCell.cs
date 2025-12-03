using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryCell : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text amountText;

    private ResourceType _resourceType;
    private int _amount;
    private bool _isEmpty = true;
    private InventorySystem _inventorySystem;

    public ResourceType ResourceType => _resourceType;
    public int Amount => _amount;
    public bool IsEmpty() => _isEmpty;

    public void Initialize(InventorySystem inventorySystem)
    {
        _inventorySystem = inventorySystem;
        Clear();
    }

    public void SetResource(ResourceType type, int amount)
    {
        _resourceType = type;
        _amount = amount;
        _isEmpty = false;

        UpdateUI();
    }

    public void Clear()
    {
        _resourceType = ResourceType.Ferrite; // Default, but won't be used
        _amount = 0;
        _isEmpty = true;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (amountText != null)
        {
            amountText.text = "";
        }
    }

    private void UpdateUI()
    {
        if (ResourceManager.Instance != null)
        {
            Sprite icon = ResourceManager.Instance.GetResourceIcon(_resourceType);
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }
        }

        if (amountText != null)
        {
            amountText.text = _amount.ToString();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isEmpty || _inventorySystem == null)
        {
            return;
        }

        // Check for shift key - use Input.GetKey which works reliably with pointer events
        bool isShiftClick = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (isShiftClick)
        {
            // Shift + Click: Return ALL resources of the same type from inventory
            _inventorySystem.ReturnAllResourcesOfType(_resourceType);
        }
        else
        {
            // Single Click: Return only this cell's resource to ResourceManager
            _inventorySystem.ReturnResourceToManager(_resourceType, _amount);
            Clear();
        }
    }
}

