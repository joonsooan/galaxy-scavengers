using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySystem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject inventoryGridContainer;
    [SerializeField] private GameObject currentResourcePanel;
    [SerializeField] private Button sortButton;

    [Header("Prefabs")]
    [SerializeField] private GameObject inventoryCellPrefab;
    [SerializeField] private GameObject resourceInfoCellPrefab;

    [Header("Inventory Settings")]
    [SerializeField] private int inventoryWidth = 5;
    [SerializeField] private int inventoryHeight = 5;
    [SerializeField] private int defaultMaxStackAmount = 100;
    [SerializeField] private List<ResourceStackData> customMaxStackAmounts = new ();

    [Serializable]
    public class ResourceStackData
    {
        public ResourceType resourceType;
        public int maxStackAmount;
    }

    private readonly Dictionary<ResourceType, int> _maxStackAmounts = new ();

    private readonly List<InventoryCell> _inventoryCells = new ();
    private readonly List<ResourceInfoCellClickable> _resourceInfoCells = new ();
    private GridLayoutGroup _inventoryGrid;
    private RectTransform _resourcePanelContent;

    private void Awake()
    {
        InitializeMaxStackAmounts();
    }

    private void Start()
    {
        InitializeInventory();
        InitializeResourcePanel();
        
        if (sortButton != null)
        {
            sortButton.onClick.AddListener(SortInventory);
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }

        if (currentResourcePanel != null)
        {
            currentResourcePanel.SetActive(false);
        }

        // Ensure event subscription happens in Start as well
        SubscribeToResourceEvents();
    }

    private void OnEnable()
    {
        SubscribeToResourceEvents();
    }

    private void SubscribeToResourceEvents()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.OnResourceAmountChanged += UpdateResourceInfoCells;
        }
    }

    private void OnDisable()
    {
        ResourceManager.OnResourceAmountChanged -= UpdateResourceInfoCells;
    }

    private void InitializeMaxStackAmounts()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            _maxStackAmounts[type] = defaultMaxStackAmount;
        }

        foreach (ResourceStackData data in customMaxStackAmounts)
        {
            if (_maxStackAmounts.ContainsKey(data.resourceType))
            {
                _maxStackAmounts[data.resourceType] = data.maxStackAmount;
            }
        }
    }

    private void InitializeInventory()
    {
        if (inventoryGridContainer == null || inventoryCellPrefab == null)
        {
            Debug.LogError("InventoryGridContainer or InventoryCellPrefab is not assigned!");
            return;
        }

        _inventoryGrid = inventoryGridContainer.GetComponent<GridLayoutGroup>();
        if (_inventoryGrid == null)
        {
            Debug.LogError("InventoryGridContainer must have a GridLayoutGroup component!");
            return;
        }

        int totalSlots = inventoryWidth * inventoryHeight;
        for (int i = 0; i < totalSlots; i++)
        {
            GameObject cellObj = Instantiate(inventoryCellPrefab, inventoryGridContainer.transform);
            InventoryCell cell = cellObj.GetComponent<InventoryCell>();
            if (cell != null)
            {
                cell.Initialize(this);
                _inventoryCells.Add(cell);
            }
        }
    }

    private void InitializeResourcePanel()
    {
        if (currentResourcePanel == null || resourceInfoCellPrefab == null)
        {
            Debug.LogError("CurrentResourcePanel or ResourceInfoCellPrefab is not assigned!");
            return;
        }

        GameObject currentResourcePanelScroll = null;
        foreach (Transform child in currentResourcePanel.transform)
        {
            if (child.name.Contains("Scroll") || child.GetComponent<ScrollRect>() != null)
            {
                currentResourcePanelScroll = child.gameObject;
                break;
            }
        }

        if (currentResourcePanelScroll == null)
        {
            Debug.LogError("Could not find currentResourcePanelScroll as a child of currentResourcePanel!");
            return;
        }

        // Get the ScrollRect and its content
        ScrollRect scrollRect = currentResourcePanelScroll.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            Debug.LogError("CurrentResourcePanelScroll must have a ScrollRect component!");
            return;
        }

        _resourcePanelContent = scrollRect.content;
        if (_resourcePanelContent == null)
        {
            Debug.LogError("ScrollRect content is not assigned!");
            return;
        }

        // Initialize all resource cells with current data
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            GameObject cellObj = Instantiate(resourceInfoCellPrefab, _resourcePanelContent);
            ResourceInfoCellClickable cell = cellObj.GetComponent<ResourceInfoCellClickable>();
            if (cell != null)
            {
                cell.Initialize(type, this);
                _resourceInfoCells.Add(cell);
                
                // Initialize with current resource amount from ResourceManager
                if (ResourceManager.Instance != null)
                {
                    int currentAmount = ResourceManager.Instance.GetResourceAmount(type);
                    cell.UpdateAmount(currentAmount);
                }
            }
        }
    }

    public void ToggleInventory()
    {
        bool isActive = inventoryPanel != null && inventoryPanel.activeSelf;
        
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(!isActive);
        }
        
        if (currentResourcePanel != null)
        {
            currentResourcePanel.SetActive(!isActive);
            
            // Refresh all resource cells when panel is shown to ensure data is up to date
            if (!isActive)
            {
                RefreshAllResourceCells();
            }
        }
    }

    public GameObject GetInventoryPanel()
    {
        return inventoryPanel;
    }
    
    private void RefreshAllResourceCells()
    {
        if (ResourceManager.Instance == null) return;
        
        foreach (ResourceInfoCellClickable cell in _resourceInfoCells)
        {
            if (cell != null)
            {
                int currentAmount = ResourceManager.Instance.GetResourceAmount(cell.ResourceType);
                cell.UpdateAmount(currentAmount);
            }
        }
    }

    public int GetMaxStackAmount(ResourceType type)
    {
        _maxStackAmounts.TryGetValue(type, out int maxStack);
        return maxStack;
    }

    public void SetMaxStackAmount(ResourceType type, int amount)
    {
        _maxStackAmounts[type] = amount;
    }

    public InventoryCell FindFirstEmptyCell()
    {
        return _inventoryCells.FirstOrDefault(cell => cell.IsEmpty());
    }

    public List<InventoryCell> GetAllEmptyCells()
    {
        return _inventoryCells.Where(cell => cell.IsEmpty()).ToList();
    }

    public bool TryAddResourceToInventory(ResourceType type, int amount, bool moveAll = false)
    {
        if (ResourceManager.Instance == null)
        {
            return false;
        }

        int availableAmount = ResourceManager.Instance.GetResourceAmount(type);
        if (availableAmount <= 0)
        {
            return false;
        }

        int amountToMove = moveAll ? availableAmount : Mathf.Min(amount, availableAmount);
        int maxStack = GetMaxStackAmount(type);
        int remainingToMove = amountToMove;

        while (remainingToMove > 0)
        {
            InventoryCell emptyCell = FindFirstEmptyCell();
            if (emptyCell == null)
            {
                break; // Inventory is full
            }

            int amountForThisCell = Mathf.Min(remainingToMove, maxStack);
            if (ResourceManager.Instance.RemoveResource(type, amountForThisCell))
            {
                // Set the resource in the inventory cell
                emptyCell.SetResource(type, amountForThisCell);
                remainingToMove -= amountForThisCell;
            }
            else
            {
                break; // Can't remove more from ResourceManager
            }
        }

        // Manually update the resource info cell to ensure it reflects the current amount
        // The event should also trigger, but this ensures it updates even if there's a timing issue
        if (ResourceManager.Instance != null)
        {
            int currentAmount = ResourceManager.Instance.GetResourceAmount(type);
            UpdateResourceInfoCells(type, currentAmount);
        }

        return remainingToMove < amountToMove;
    }

    public void ReturnResourceToManager(ResourceType type, int amount)
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(type, amount);
        }
    }

    public void ReturnAllResourcesOfType(ResourceType type)
    {
        if (ResourceManager.Instance == null)
        {
            return;
        }

        int totalAmount = 0;
        List<InventoryCell> cellsToClear = new List<InventoryCell>();

        // Find all cells with the same resource type and calculate total amount
        foreach (InventoryCell cell in _inventoryCells)
        {
            if (!cell.IsEmpty() && cell.ResourceType == type)
            {
                totalAmount += cell.Amount;
                cellsToClear.Add(cell);
            }
        }

        // Return all resources to ResourceManager
        if (totalAmount > 0)
        {
            ResourceManager.Instance.AddResource(type, totalAmount);
            
            // Clear all cells that had this resource type
            foreach (InventoryCell cell in cellsToClear)
            {
                cell.Clear();
            }
        }
    }

    private void UpdateResourceInfoCells(ResourceType type, int amount)
    {
        // Update the specific resource cell with the new amount
        ResourceInfoCellClickable cell = _resourceInfoCells.FirstOrDefault(c => c.ResourceType == type);
        if (cell != null)
        {
            cell.UpdateAmount(amount);
        }
    }

    public void SortInventory()
    {
        List<InventoryCell> filledCells = _inventoryCells.Where(cell => !cell.IsEmpty()).ToList();
        
        filledCells.Sort((a, b) =>
        {
            int typeComparison = a.ResourceType.CompareTo(b.ResourceType);
            if (typeComparison != 0)
            {
                return typeComparison;
            }
            return b.Amount.CompareTo(a.Amount);
        });

        List<(ResourceType type, int amount)> cellData = filledCells
            .Select(cell => (cell.ResourceType, cell.Amount))
            .ToList();

        foreach (InventoryCell cell in _inventoryCells)
        {
            cell.Clear();
        }

        for (int i = 0; i < cellData.Count && i < _inventoryCells.Count; i++)
        {
            _inventoryCells[i].SetResource(cellData[i].type, cellData[i].amount);
        }
    }
}

