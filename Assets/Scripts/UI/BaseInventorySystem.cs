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
    [SerializeField] private GameObject moduleGridContainer;
    [SerializeField] private Button sortButton;
    [SerializeField] private Button unloadToBaseButton;
    [SerializeField] private TMP_Text inventoryInfoText;

    [Header("Prefabs")]
    [SerializeField] private GameObject baseInventoryCellPrefab;
    [SerializeField] private GameObject moduleInventoryCellPrefab;

    [Header("Inventory Settings")]
    [SerializeField] private int inventoryWidth = 5;
    [SerializeField] private int inventoryHeight = 5;
    [SerializeField] private int defaultMaxStackAmount = 100;
    [SerializeField] private List<InventorySystem.ResourceStackData> customMaxStackAmounts = new List<InventorySystem.ResourceStackData>();

    private readonly List<BaseInventoryCell> _inventoryCells = new List<BaseInventoryCell>();
    private readonly Dictionary<ResourceType, int> _maxStackAmounts = new Dictionary<ResourceType, int>();
    private readonly List<ModuleInventoryCell> _moduleCells = new List<ModuleInventoryCell>();
    private readonly Dictionary<int, ModuleInventoryCell> _moduleCellSlots = new Dictionary<int, ModuleInventoryCell>(); // Maps slot index to module cell
    private BaseInventoryManager _baseInventoryManager;
    
    private int TotalGridSlots => inventoryWidth * inventoryHeight;

    private void Awake()
    {
        InitializeMaxStackAmounts();
    }

    private void Start()
    {
        InitializeInventory();
        InitializeModuleGrid();

        if (sortButton != null) {
            sortButton.onClick.AddListener(SortInventory);
        }

        if (unloadToBaseButton != null) {
            unloadToBaseButton.onClick.AddListener(UnloadAllToBaseInventory);
        }

        if (inventoryPanel != null) {
            HideInventoryPanel();
        }

        SubscribeToModuleEvents();

        StartCoroutine(RefreshInventoryAfterInitialization());
    }

    private void OnEnable()
    {
        SubscribeToModuleEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromModuleEvents();
    }

    private IEnumerator RefreshInventoryAfterInitialization()
    {
        yield return null;

        while (_baseInventoryManager == null) {
            _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
            yield return null;
        }

        RefreshInventoryGrid();
    }

    private void SubscribeToModuleEvents()
    {
        _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (_baseInventoryManager != null) {
            _baseInventoryManager.OnModuleAdded += OnModuleAdded;
            _baseInventoryManager.OnModuleRemoved += OnModuleRemoved;
            _baseInventoryManager.OnResourceChanged += OnResourceChanged;
        }
    }

    private void UnsubscribeFromModuleEvents()
    {
        if (_baseInventoryManager != null) {
            _baseInventoryManager.OnModuleAdded -= OnModuleAdded;
            _baseInventoryManager.OnModuleRemoved -= OnModuleRemoved;
            _baseInventoryManager.OnResourceChanged -= OnResourceChanged;
        }
    }

    private void OnResourceChanged(ResourceType type, int amount)
    {
        if (inventoryPanel != null && inventoryPanel.activeSelf) {
            RefreshResourcesOnly();
        }
    }

    private void OnModuleAdded(Module module)
    {
        if (inventoryPanel != null && inventoryPanel.activeSelf) {
            RefreshModulesOnly();
        }
    }

    private void OnModuleRemoved(Module module)
    {
        if (inventoryPanel != null && inventoryPanel.activeSelf) {
            RefreshModulesOnly();
        }
    }

    private void HideInventoryPanel()
    {
        inventoryPanel.SetActive(false);
    }

    private void InitializeMaxStackAmounts()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) {
            _maxStackAmounts[type] = defaultMaxStackAmount;
        }

        foreach (InventorySystem.ResourceStackData data in customMaxStackAmounts) {
            if (_maxStackAmounts.ContainsKey(data.resourceType)) {
                _maxStackAmounts[data.resourceType] = data.maxStackAmount;
            }
        }
    }

    private void InitializeInventory()
    {
        inventoryGridContainer.GetComponent<GridLayoutGroup>();
        int totalSlots = inventoryWidth * inventoryHeight;

        for (int i = 0; i < totalSlots; i++) {
            GameObject cellObj = Instantiate(baseInventoryCellPrefab, inventoryGridContainer.transform);
            BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();
            if (cell != null) {
                cell.Initialize(this);
                _inventoryCells.Add(cell);
            }
        }
    }

    public void ToggleInventory()
    {
        bool isActive = inventoryPanel != null && inventoryPanel.activeSelf;

        if (inventoryPanel != null) {
            inventoryPanel.SetActive(!isActive);

            if (!isActive) {
                RefreshInventoryGrid();
            }
        }
    }

    private void InitializeModuleGrid()
    {
        // Modules are now handled in the unified inventory grid
        // This method is kept for compatibility but does nothing
    }

    public GameObject GetInventoryPanel()
    {
        return inventoryPanel;
    }

    public GameObject GetInventoryResourceContainer()
    {
        return inventoryGridContainer;
    }

    public int GetMaxStackAmount(ResourceType type)
    {
        _maxStackAmounts.TryGetValue(type, out int maxStack);
        return maxStack;
    }

    private BaseInventoryCell FindFirstEmptyCell()
    {
        for (int i = 0; i < _inventoryCells.Count; i++) {
            if (!IsSlotUsedByModule(i) && _inventoryCells[i] != null && _inventoryCells[i].IsEmpty()) {
                return _inventoryCells[i];
            }
        }
        return null;
    }

    public List<BaseInventoryCell> GetAllEmptyCells()
    {
        return _inventoryCells.Where((cell, index) => !IsSlotUsedByModule(index) && cell != null && cell.IsEmpty()).ToList();
    }
    
    private int FindEmptySlotIndex()
    {
        for (int i = 0; i < _inventoryCells.Count; i++) {
            if (!IsSlotUsedByModule(i) && _inventoryCells[i] != null && _inventoryCells[i].IsEmpty()) {
                return i;
            }
        }
        return -1;
    }
    
    private bool IsSlotUsedByModule(int slotIndex)
    {
        return _moduleCellSlots.ContainsKey(slotIndex);
    }
    
    private void RestoreBaseInventoryCellsFromModules()
    {
        if (inventoryGridContainer == null || baseInventoryCellPrefab == null) {
            return;
        }

        List<int> slotsToRestore = new List<int>(_moduleCellSlots.Keys);
        slotsToRestore.Sort((a, b) => b.CompareTo(a)); // Restore from highest to lowest to maintain indices
        
        foreach (int slotIndex in slotsToRestore) {
            if (slotIndex < 0 || slotIndex >= _inventoryCells.Count) {
                continue;
            }
            
            ModuleInventoryCell moduleCell = _moduleCellSlots[slotIndex];
            if (moduleCell != null) {
                Transform moduleTransform = moduleCell.transform;
                Transform parent = moduleTransform.parent;
                int siblingIndex = moduleTransform.GetSiblingIndex();
                
                Destroy(moduleCell.gameObject);
                
                // Create new BaseInventoryCell at the same position
                GameObject cellObj = Instantiate(baseInventoryCellPrefab, parent);
                cellObj.transform.SetSiblingIndex(siblingIndex);
                BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();
                if (cell != null) {
                    cell.Initialize(this);
                    _inventoryCells[slotIndex] = cell;
                }
            }
        }
        
        _moduleCellSlots.Clear();
    }
    
    private int GetUsedSlotCount()
    {
        int resourceSlots = 0;
        for (int i = 0; i < _inventoryCells.Count; i++) {
            if (!IsSlotUsedByModule(i) && _inventoryCells[i] != null && !_inventoryCells[i].IsEmpty()) {
                resourceSlots++;
            }
        }
        int moduleSlots = _moduleCells.Count;
        return resourceSlots + moduleSlots;
    }
    
    public bool IsGridFull()
    {
        return GetUsedSlotCount() >= TotalGridSlots;
    }

    private void RefreshResourcesOnly()
    {
        _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        
        for (int i = 0; i < _inventoryCells.Count; i++) {
            if (!IsSlotUsedByModule(i) && _inventoryCells[i] != null) {
                _inventoryCells[i].Clear();
            }
        }

        Dictionary<ResourceType, int> baseResources = _baseInventoryManager.GetAllResources();

        List<KeyValuePair<ResourceType, int>> sortedResources = baseResources
            .Where(kvp => kvp.Value > 0)
            .OrderBy(kvp => kvp.Key)
            .ToList();

        int cellIndex = 0;
        
        foreach (KeyValuePair<ResourceType, int> resource in sortedResources) {
            ResourceType type = resource.Key;
            int totalAmount = resource.Value;
            int maxStack = GetMaxStackAmount(type);

            while (totalAmount > 0) {
                while (cellIndex < _inventoryCells.Count && (IsSlotUsedByModule(cellIndex) || _inventoryCells[cellIndex] == null)) {
                    cellIndex++;
                }
                
                if (cellIndex >= _inventoryCells.Count) {
                    int usedSlots = GetUsedSlotCount();
                    Debug.Log($"BaseInventorySystem: Inventory grid is full! Cannot add more resources. ({usedSlots}/{TotalGridSlots} slots used)");
                    break;
                }

                int amountForCell = Mathf.Min(totalAmount, maxStack);
                if (_inventoryCells[cellIndex] != null) {
                    _inventoryCells[cellIndex].SetResource(type, amountForCell);
                    totalAmount -= amountForCell;
                    cellIndex++;
                } else {
                    cellIndex++;
                }
            }
            
            if (cellIndex >= _inventoryCells.Count) {
                break;
            }
        }

        UpdateInventoryInfoText();
    }

    public void RefreshModulesOnly()
    {
        _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        
        RestoreBaseInventoryCellsFromModules();
        _moduleCells.Clear();

        List<Module> modules = _baseInventoryManager.GetAllModules();
        if (modules != null && modules.Count > 0) {
            int usedSlots = GetUsedSlotCount();
            
            foreach (Module module in modules) {
                int emptySlotIndex = FindEmptySlotIndex();
                
                if (emptySlotIndex == -1) {
                    Debug.Log($"BaseInventorySystem: Inventory grid is full! Cannot add more modules. ({usedSlots}/{TotalGridSlots} slots used)");
                    break;
                }

                BaseInventoryCell baseCell = _inventoryCells[emptySlotIndex];
                if (baseCell != null) {
                    Transform parent = baseCell.transform.parent;
                    int siblingIndex = baseCell.transform.GetSiblingIndex();
                    
                    Destroy(baseCell.gameObject);
                    _inventoryCells[emptySlotIndex] = null;
                    
                    GameObject cellObj = Instantiate(moduleInventoryCellPrefab, parent);
                    cellObj.transform.SetSiblingIndex(siblingIndex);
                    ModuleInventoryCell cell = cellObj.GetComponent<ModuleInventoryCell>();
                    if (cell != null) {
                        cell.Initialize(this);
                        cell.SetModule(module);
                        _moduleCells.Add(cell);
                        _moduleCellSlots[emptySlotIndex] = cell;
                    }
                }
            }
        }

        UpdateInventoryInfoText();
    }

    public void RefreshInventoryGrid()
    {
        RefreshResourcesOnly();
        RefreshModulesOnly();
    }

    private void UpdateInventoryInfoText()
    {
        if (inventoryInfoText == null) return;

        int totalItems = GetUsedSlotCount();
        int totalSlots = TotalGridSlots;
        
        inventoryInfoText.text = $"기지 창고 ({totalItems.ToString()}/{totalSlots})";
    }

    public bool TryAddResourceToInventory(ResourceType type, int amount, bool moveAll = false)
    {
        _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (_baseInventoryManager == null) {
            return false;
        }

        int availableAmount = _baseInventoryManager.GetResourceAmount(type);
        if (availableAmount <= 0) {
            return false;
        }

        int amountToMove = moveAll ? availableAmount : Mathf.Min(amount, availableAmount);
        int maxStack = GetMaxStackAmount(type);
        int remainingToMove = amountToMove;

        while (remainingToMove > 0) {
            if (IsGridFull()) {
                Debug.Log("Inventory is full!)");
                break;
            }
            
            BaseInventoryCell emptyCell = FindFirstEmptyCell();
            if (emptyCell == null) {
                Debug.Log("Inventory is full!)");
                break;
            }

            int amountForThisCell = Mathf.Min(remainingToMove, maxStack);
            if (_baseInventoryManager.RemoveResource(type, amountForThisCell)) {
                emptyCell.SetResource(type, amountForThisCell);
                remainingToMove -= amountForThisCell;
            }
            else {
                break;
            }
        }

        if (remainingToMove < amountToMove) {
            UpdateInventoryInfoText();
        }

        return remainingToMove < amountToMove;
    }

    public void ReturnResourceToBaseInventory(ResourceType type, int amount)
    {
        _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (_baseInventoryManager != null) {
            _baseInventoryManager.AddResource(type, amount);
            UpdateInventoryInfoText();
        }
    }

    public void ReturnAllResourcesOfType(ResourceType type)
    {
        _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (_baseInventoryManager == null) {
            return;
        }

        int totalAmount = 0;
        List<BaseInventoryCell> cellsToClear = new List<BaseInventoryCell>();

        foreach (BaseInventoryCell cell in _inventoryCells) {
            if (!cell.IsEmpty() && cell.ResourceType == type) {
                totalAmount += cell.Amount;
                cellsToClear.Add(cell);
            }
        }

        if (totalAmount > 0) {
            _baseInventoryManager.AddResource(type, totalAmount);

            foreach (BaseInventoryCell cell in cellsToClear) {
                cell.Clear();
            }

            UpdateInventoryInfoText();
        }
    }

    private void SortInventory()
    {
        List<BaseInventoryCell> filledCells = _inventoryCells.Where(cell => !cell.IsEmpty()).ToList();

        filledCells.Sort((a, b) => {
            int typeComparison = a.ResourceType.CompareTo(b.ResourceType);
            if (typeComparison != 0) {
                return typeComparison;
            }
            return b.Amount.CompareTo(a.Amount);
        });

        List<(ResourceType type, int amount)> cellData = filledCells
            .Select(cell => (cell.ResourceType, cell.Amount))
            .ToList();

        foreach (BaseInventoryCell cell in _inventoryCells) {
            cell.Clear();
        }

        for (int i = 0; i < cellData.Count && i < _inventoryCells.Count; i++) {
            _inventoryCells[i].SetResource(cellData[i].type, cellData[i].amount);
        }

        UpdateInventoryInfoText();
    }

    private void UnloadAllToBaseInventory()
    {
        _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (_baseInventoryManager == null) return;

        Dictionary<ResourceType, int> resourcesToTransfer = new Dictionary<ResourceType, int>();

        foreach (BaseInventoryCell cell in _inventoryCells) {
            if (!cell.IsEmpty()) {
                ResourceType type = cell.ResourceType;
                int amount = cell.Amount;

                if (resourcesToTransfer.ContainsKey(type)) {
                    resourcesToTransfer[type] += amount;
                }
                else {
                    resourcesToTransfer[type] = amount;
                }
            }
        }

        foreach (KeyValuePair<ResourceType, int> kvp in resourcesToTransfer) {
            _baseInventoryManager.AddResource(kvp.Key, kvp.Value);
        }

        foreach (BaseInventoryCell cell in _inventoryCells) {
            cell.Clear();
        }

        UpdateInventoryInfoText();
    }
}
