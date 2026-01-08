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
    private GridLayoutGroup _inventoryGrid;
    private BaseInventoryManager _baseInventoryManager;

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
            RefreshInventoryGrid();
        }
    }

    private void OnModuleAdded(Module module)
    {
        LoadModulesToGrid();
    }

    private void OnModuleRemoved(Module module)
    {
        LoadModulesToGrid();
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
        if (inventoryGridContainer == null || baseInventoryCellPrefab == null) {
            Debug.LogError("InventoryGridContainer or BaseInventoryCellPrefab is not assigned!");
            return;
        }

        _inventoryGrid = inventoryGridContainer.GetComponent<GridLayoutGroup>();
        if (_inventoryGrid == null) {
            Debug.LogError("InventoryGridContainer must have a GridLayoutGroup component!");
            return;
        }

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
                RefreshModuleGrid();
            }
        }
    }

    private void InitializeModuleGrid()
    {
        if (moduleGridContainer == null || moduleInventoryCellPrefab == null) {
            return;
        }

        moduleGridContainer.GetComponent<GridLayoutGroup>();
    }

    private void LoadModulesToGrid()
    {
        RefreshModuleGrid();
    }

    public void RefreshModuleGrid()
    {
        if (moduleGridContainer == null || moduleInventoryCellPrefab == null) {
            return;
        }

        _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (_baseInventoryManager == null) {
            return;
        }

        foreach (Transform child in moduleGridContainer.transform) {
            Destroy(child.gameObject);
        }
        _moduleCells.Clear();

        List<Module> modules = _baseInventoryManager.GetAllModules();

        foreach (Module module in modules) {
            GameObject cellObj = Instantiate(moduleInventoryCellPrefab, moduleGridContainer.transform);
            ModuleInventoryCell cell = cellObj.GetComponent<ModuleInventoryCell>();
            if (cell != null) {
                cell.Initialize(this);
                cell.SetModule(module);
                _moduleCells.Add(cell);
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

    private BaseInventoryCell FindFirstEmptyCell()
    {
        return _inventoryCells.FirstOrDefault(cell => cell.IsEmpty());
    }

    public List<BaseInventoryCell> GetAllEmptyCells()
    {
        return _inventoryCells.Where(cell => cell.IsEmpty()).ToList();
    }

    private void LoadBaseInventoryToCells()
    {
        RefreshInventoryGrid();
    }

    public void RefreshInventoryGrid()
    {
        _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (_baseInventoryManager == null) {
            Debug.LogWarning("BaseInventorySystem: BaseInventoryManager not available");
            return;
        }

        if (_inventoryCells == null || _inventoryCells.Count == 0) {
            Debug.LogWarning("BaseInventorySystem: Inventory cells not initialized");
            return;
        }

        Dictionary<ResourceType, int> baseResources = _baseInventoryManager.GetAllResources();

        List<KeyValuePair<ResourceType, int>> sortedResources = baseResources
            .Where(kvp => kvp.Value > 0)
            .OrderBy(kvp => kvp.Key)
            .ToList();

        foreach (BaseInventoryCell cell in _inventoryCells) {
            cell.Clear();
        }

        int cellIndex = 0;
        foreach (KeyValuePair<ResourceType, int> resource in sortedResources) {
            if (cellIndex >= _inventoryCells.Count) break;

            ResourceType type = resource.Key;
            int totalAmount = resource.Value;
            int maxStack = GetMaxStackAmount(type);

            while (totalAmount > 0 && cellIndex < _inventoryCells.Count) {
                int amountForCell = Mathf.Min(totalAmount, maxStack);
                _inventoryCells[cellIndex].SetResource(type, amountForCell);
                totalAmount -= amountForCell;
                cellIndex++;
            }
        }

        UpdateInventoryInfoText();
    }

    private void UpdateInventoryInfoText()
    {
        if (inventoryInfoText == null) return;

        int nonEmptyCellCount = _inventoryCells.Count(cell => !cell.IsEmpty());
        inventoryInfoText.text = $"기지 창고 ({nonEmptyCellCount.ToString()}/{_inventoryCells.Count})";
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
            BaseInventoryCell emptyCell = FindFirstEmptyCell();
            if (emptyCell == null) {
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

    public void SortInventory()
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

    public void UnloadAllToBaseInventory()
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
