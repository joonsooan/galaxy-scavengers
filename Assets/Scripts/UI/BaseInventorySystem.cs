using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BaseInventorySystem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject inventoryGridContainer;
    [SerializeField] private Button sortButton;
    [SerializeField] private Button unloadToBaseButton;

    [Header("Prefabs")]
    [SerializeField] private GameObject baseInventoryCellPrefab;

    [Header("Inventory Settings")]
    [SerializeField] private int inventoryWidth = 5;
    [SerializeField] private int inventoryHeight = 5;
    [SerializeField] private int defaultMaxStackAmount = 100;
    [SerializeField] private List<InventorySystem.ResourceStackData> customMaxStackAmounts = new();

    private readonly Dictionary<ResourceType, int> _maxStackAmounts = new();
    private readonly List<BaseInventoryCell> _inventoryCells = new();
    private GridLayoutGroup _inventoryGrid;

    private void Awake()
    {
        InitializeMaxStackAmounts();
    }

    private void Start()
    {
        InitializeInventory();
        
        if (sortButton != null)
        {
            sortButton.onClick.AddListener(SortInventory);
        }

        if (unloadToBaseButton != null)
        {
            unloadToBaseButton.onClick.AddListener(UnloadAllToBaseInventory);
        }

        if (inventoryPanel != null)
        {
            HideInventoryPanel();
        }
    }

    private void HideInventoryPanel()
    {
        inventoryPanel.SetActive(false);
    }

    private void InitializeMaxStackAmounts()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            _maxStackAmounts[type] = defaultMaxStackAmount;
        }

        foreach (var data in customMaxStackAmounts)
        {
            if (_maxStackAmounts.ContainsKey(data.resourceType))
            {
                _maxStackAmounts[data.resourceType] = data.maxStackAmount;
            }
        }
    }

    private void InitializeInventory()
    {
        if (inventoryGridContainer == null || baseInventoryCellPrefab == null)
        {
            Debug.LogError("InventoryGridContainer or BaseInventoryCellPrefab is not assigned!");
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
            GameObject cellObj = Instantiate(baseInventoryCellPrefab, inventoryGridContainer.transform);
            BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();
            if (cell != null)
            {
                cell.Initialize(this);
                _inventoryCells.Add(cell);
            }
        }
        
        // Load base inventory into cells will be done when panel is opened
    }

    public void ToggleInventory()
    {
        bool isActive = inventoryPanel != null && inventoryPanel.activeSelf;
        
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(!isActive);
            
            if (!isActive)
            {
                // Load base inventory when opening
                LoadBaseInventoryToCells();
            }
        }
    }

    public GameObject GetInventoryPanel()
    {
        return inventoryPanel;
    }

    public int GetMaxStackAmount(ResourceType type)
    {
        _maxStackAmounts.TryGetValue(type, out int maxStack);
        return maxStack;
    }

    public BaseInventoryCell FindFirstEmptyCell()
    {
        return _inventoryCells.FirstOrDefault(cell => cell.IsEmpty());
    }

    public List<BaseInventoryCell> GetAllEmptyCells()
    {
        return _inventoryCells.Where(cell => cell.IsEmpty()).ToList();
    }

    // Load base inventory data into cells
    private void LoadBaseInventoryToCells()
    {
        if (BaseInventoryManager.Instance == null) return;

        // Clear all cells first
        foreach (BaseInventoryCell cell in _inventoryCells)
        {
            cell.Clear();
        }

        // Get all resources from base inventory
        Dictionary<ResourceType, int> baseResources = BaseInventoryManager.Instance.GetAllResources();
        
        // Sort resources by type
        var sortedResources = baseResources
            .Where(kvp => kvp.Value > 0)
            .OrderBy(kvp => kvp.Key)
            .ToList();

        int cellIndex = 0;
        foreach (var resource in sortedResources)
        {
            if (cellIndex >= _inventoryCells.Count) break;

            ResourceType type = resource.Key;
            int totalAmount = resource.Value;
            int maxStack = GetMaxStackAmount(type);

            // Distribute resources across cells if needed
            while (totalAmount > 0 && cellIndex < _inventoryCells.Count)
            {
                int amountForCell = Mathf.Min(totalAmount, maxStack);
                _inventoryCells[cellIndex].SetResource(type, amountForCell);
                totalAmount -= amountForCell;
                cellIndex++;
            }
        }
    }

    // Add resource from base inventory to a cell
    public bool TryAddResourceToInventory(ResourceType type, int amount, bool moveAll = false)
    {
        if (BaseInventoryManager.Instance == null)
        {
            return false;
        }

        int availableAmount = BaseInventoryManager.Instance.GetResourceAmount(type);
        if (availableAmount <= 0)
        {
            return false;
        }

        int amountToMove = moveAll ? availableAmount : Mathf.Min(amount, availableAmount);
        int maxStack = GetMaxStackAmount(type);
        int remainingToMove = amountToMove;

        while (remainingToMove > 0)
        {
            BaseInventoryCell emptyCell = FindFirstEmptyCell();
            if (emptyCell == null)
            {
                break;
            }

            int amountForThisCell = Mathf.Min(remainingToMove, maxStack);
            if (BaseInventoryManager.Instance.RemoveResource(type, amountForThisCell))
            {
                emptyCell.SetResource(type, amountForThisCell);
                remainingToMove -= amountForThisCell;
            }
            else
            {
                break;
            }
        }

        return remainingToMove < amountToMove;
    }

    // Return resource from cell back to base inventory
    public void ReturnResourceToBaseInventory(ResourceType type, int amount)
    {
        if (BaseInventoryManager.Instance != null)
        {
            BaseInventoryManager.Instance.AddResource(type, amount);
        }
    }

    public void ReturnAllResourcesOfType(ResourceType type)
    {
        if (BaseInventoryManager.Instance == null)
        {
            return;
        }

        int totalAmount = 0;
        List<BaseInventoryCell> cellsToClear = new List<BaseInventoryCell>();

        foreach (BaseInventoryCell cell in _inventoryCells)
        {
            if (!cell.IsEmpty() && cell.ResourceType == type)
            {
                totalAmount += cell.Amount;
                cellsToClear.Add(cell);
            }
        }

        if (totalAmount > 0)
        {
            BaseInventoryManager.Instance.AddResource(type, totalAmount);
            
            foreach (BaseInventoryCell cell in cellsToClear)
            {
                cell.Clear();
            }
        }
    }

    public void SortInventory()
    {
        List<BaseInventoryCell> filledCells = _inventoryCells.Where(cell => !cell.IsEmpty()).ToList();
        
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

        foreach (BaseInventoryCell cell in _inventoryCells)
        {
            cell.Clear();
        }

        for (int i = 0; i < cellData.Count && i < _inventoryCells.Count; i++)
        {
            _inventoryCells[i].SetResource(cellData[i].type, cellData[i].amount);
        }
    }

    // Unload all resources from game scene inventory to base inventory
    public void UnloadAllToBaseInventory()
    {
        if (BaseInventoryManager.Instance == null) return;

        // Collect all resources from cells
        Dictionary<ResourceType, int> resourcesToTransfer = new Dictionary<ResourceType, int>();

        foreach (BaseInventoryCell cell in _inventoryCells)
        {
            if (!cell.IsEmpty())
            {
                ResourceType type = cell.ResourceType;
                int amount = cell.Amount;
                
                if (resourcesToTransfer.ContainsKey(type))
                {
                    resourcesToTransfer[type] += amount;
                }
                else
                {
                    resourcesToTransfer[type] = amount;
                }
            }
        }

        // Transfer to base inventory
        foreach (var kvp in resourcesToTransfer)
        {
            BaseInventoryManager.Instance.AddResource(kvp.Key, kvp.Value);
        }

        // Clear all cells
        foreach (BaseInventoryCell cell in _inventoryCells)
        {
            cell.Clear();
        }
    }
}

