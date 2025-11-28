using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySystem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject currentResourcePanel;
    [SerializeField] private Button sortButton;

    [Header("Prefabs")]
    [SerializeField] private GameObject inventoryCellPrefab;
    [SerializeField] private GameObject resourceInfoCellPrefab;

    [Header("Inventory Settings")]
    [SerializeField] private int inventoryWidth = 5;
    [SerializeField] private int inventoryHeight = 5;
    [SerializeField] private int defaultMaxStackAmount = 100;
    [SerializeField] private List<ResourceStackData> customMaxStackAmounts = new List<ResourceStackData>();

    [System.Serializable]
    public class ResourceStackData
    {
        public ResourceType resourceType;
        public int maxStackAmount;
    }

    private Dictionary<ResourceType, int> maxStackAmounts = new Dictionary<ResourceType, int>();

    private List<InventoryCell> _inventoryCells = new List<InventoryCell>();
    private List<ResourceInfoCellClickable> _resourceInfoCells = new List<ResourceInfoCellClickable>();
    private GridLayoutGroup _inventoryGrid;
    private HorizontalLayoutGroup _resourcePanelLayout;

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
    }

    private void OnEnable()
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
        // Initialize with default values
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            maxStackAmounts[type] = defaultMaxStackAmount;
        }

        // Apply custom values from inspector
        foreach (ResourceStackData data in customMaxStackAmounts)
        {
            if (maxStackAmounts.ContainsKey(data.resourceType))
            {
                maxStackAmounts[data.resourceType] = data.maxStackAmount;
            }
        }
    }

    private void InitializeInventory()
    {
        if (inventoryPanel == null || inventoryCellPrefab == null)
        {
            Debug.LogError("InventoryPanel or InventoryCellPrefab is not assigned!");
            return;
        }

        _inventoryGrid = inventoryPanel.GetComponent<GridLayoutGroup>();
        if (_inventoryGrid == null)
        {
            Debug.LogError("InventoryPanel must have a GridLayoutGroup component!");
            return;
        }

        int totalSlots = inventoryWidth * inventoryHeight;
        for (int i = 0; i < totalSlots; i++)
        {
            GameObject cellObj = Instantiate(inventoryCellPrefab, inventoryPanel.transform);
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

        _resourcePanelLayout = currentResourcePanel.GetComponent<HorizontalLayoutGroup>();
        if (_resourcePanelLayout == null)
        {
            Debug.LogError("CurrentResourcePanel must have a HorizontalLayoutGroup component!");
            return;
        }

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            GameObject cellObj = Instantiate(resourceInfoCellPrefab, currentResourcePanel.transform);
            ResourceInfoCellClickable cell = cellObj.GetComponent<ResourceInfoCellClickable>();
            if (cell != null)
            {
                cell.Initialize(type, this);
                _resourceInfoCells.Add(cell);
                UpdateResourceInfoCell(cell, type);
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
        }
    }

    public int GetMaxStackAmount(ResourceType type)
    {
        maxStackAmounts.TryGetValue(type, out int maxStack);
        return maxStack;
    }

    public void SetMaxStackAmount(ResourceType type, int amount)
    {
        maxStackAmounts[type] = amount;
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
                emptyCell.SetResource(type, amountForThisCell);
                remainingToMove -= amountForThisCell;
            }
            else
            {
                break; // Can't remove more from ResourceManager
            }
        }

        return remainingToMove < amountToMove; // Return true if at least some was moved
    }

    public void ReturnResourceToManager(ResourceType type, int amount)
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(type, amount);
        }
    }

    private void UpdateResourceInfoCells(ResourceType type, int amount)
    {
        ResourceInfoCellClickable cell = _resourceInfoCells.FirstOrDefault(c => c.ResourceType == type);
        if (cell != null)
        {
            UpdateResourceInfoCell(cell, type);
        }
    }

    private void UpdateResourceInfoCell(ResourceInfoCellClickable cell, ResourceType type)
    {
        if (ResourceManager.Instance != null)
        {
            int amount = ResourceManager.Instance.GetResourceAmount(type);
            cell.UpdateAmount(amount);
        }
    }

    public void SortInventory()
    {
        // Get all non-empty cells
        List<InventoryCell> filledCells = _inventoryCells.Where(cell => !cell.IsEmpty()).ToList();
        
        // Sort by ResourceType, then by amount (descending)
        filledCells.Sort((a, b) =>
        {
            int typeComparison = a.ResourceType.CompareTo(b.ResourceType);
            if (typeComparison != 0)
            {
                return typeComparison;
            }
            return b.Amount.CompareTo(a.Amount); // Descending by amount
        });

        // Store the data
        List<(ResourceType type, int amount)> cellData = filledCells
            .Select(cell => (cell.ResourceType, cell.Amount))
            .ToList();

        // Clear all cells
        foreach (InventoryCell cell in _inventoryCells)
        {
            cell.Clear();
        }

        // Reassign sorted data to cells in order
        for (int i = 0; i < cellData.Count && i < _inventoryCells.Count; i++)
        {
            _inventoryCells[i].SetResource(cellData[i].type, cellData[i].amount);
        }
    }
}

