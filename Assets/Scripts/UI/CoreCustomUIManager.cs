using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CoreCustomUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject coreCustomPanel;
    [SerializeField] private CoreCustomizationSlot[] slotComponents;
    [SerializeField] private Button closeButton;

    [Header("Module Selection Panel")]
    [SerializeField] private GameObject moduleSelectionPanel;
    [SerializeField] private Transform moduleSelectionGridContainer;
    [SerializeField] private GameObject moduleSelectionCellPrefab;

    [Header("Core Detail Panel")]
    [SerializeField] private CoreDetailPanel coreDetailPanel;

    private readonly List<ModuleInventoryCell> _moduleSelectionCells = new List<ModuleInventoryCell>();

    private CoreCustomizationManager _customizationManager;
    private BaseInventoryManager _inventoryManager;
    private BaseInventorySystem _baseInventorySystem;

    // Events for UI updates
    public event Action OnModuleSelectionGridRefreshed;
    public event Action OnSlotsRefreshed;

    private void Start()
    {
        FindManagers();
        InitializeUI();
        SubscribeToEvents();
        InitializeSlots();
        StartCoroutine(WaitForModulesAndRefreshSlots());
    }

    private void FindManagers()
    {
        _customizationManager = FindFirstObjectByType<CoreCustomizationManager>();
        _inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        _baseInventorySystem = FindFirstObjectByType<BaseInventorySystem>();
    }

    private void InitializeUI()
    {
        if (coreCustomPanel != null) {
            coreCustomPanel.SetActive(false);
        }

        if (closeButton != null) {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }
    }

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        // Unsubscribe first to prevent duplicate subscriptions
        if (_customizationManager != null) {
            _customizationManager.OnModuleSlotChanged -= OnModuleSlotChanged;
            _customizationManager.OnModuleSlotChanged += OnModuleSlotChanged;
        }

        if (_inventoryManager != null) {
            _inventoryManager.OnModuleAdded -= OnModuleInventoryChanged;
            _inventoryManager.OnModuleRemoved -= OnModuleInventoryChanged;
            _inventoryManager.OnModuleAdded += OnModuleInventoryChanged;
            _inventoryManager.OnModuleRemoved += OnModuleInventoryChanged;
        }
    }

    private void OnDisable()
    {
        if (_customizationManager != null) {
            _customizationManager.OnModuleSlotChanged -= OnModuleSlotChanged;
        }

        if (_inventoryManager != null) {
            _inventoryManager.OnModuleAdded -= OnModuleInventoryChanged;
            _inventoryManager.OnModuleRemoved -= OnModuleInventoryChanged;
        }
    }

    private IEnumerator WaitForModulesAndRefreshSlots()
    {
        if (_customizationManager == null) {
            yield break;
        }

        yield return new WaitForSeconds(0.1f);

        yield return new WaitUntil(() => {
            BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
            return inventoryManager != null;
        });

        yield return new WaitForSeconds(0.1f);

        RefreshSlots();
        RefreshModuleSelectionGrid();
    }

    private void InitializeSlots()
    {
        if (slotComponents == null || _customizationManager == null) return;

        for (int i = 0; i < slotComponents.Length; i++) {
            if (slotComponents[i] != null) {
                slotComponents[i].Initialize(i, _customizationManager, this);
            }
        }
    }

    public void ShowPanel()
    {
        if (coreCustomPanel != null) {
            coreCustomPanel.SetActive(true);
            // Refresh when panel is shown to ensure latest modules are displayed
            // Use coroutine to ensure managers are found
            StartCoroutine(RefreshOnPanelShown());
        }
    }

    private IEnumerator RefreshOnPanelShown()
    {
        // Wait one frame to ensure everything is initialized
        yield return null;
        
        // Ensure managers are found
        if (_inventoryManager == null || _customizationManager == null) {
            FindManagers();
        }
        
        // Update module selection grid with current modules from base inventory
        RefreshModuleSelectionGrid();
        RefreshSlots();
        
        // Update core detail panel to show current module effects
        if (coreDetailPanel != null) {
            coreDetailPanel.UpdateModuleEffects();
        }
    }

    public bool IsPanelOpen()
    {
        return coreCustomPanel != null && coreCustomPanel.activeSelf;
    }

    private void HidePanel()
    {
        if (coreCustomPanel != null) {
            coreCustomPanel.SetActive(false);
        }
    }

    private void OnCloseButtonClicked()
    {
        HidePanel();
    }

    public void OnSlotClicked(int slotIndex)
    {
        // Slot clicking is no longer needed for module placement
        // Keeping method for potential future use
    }

    public void RefreshModuleSelectionGrid()
    {
        ClearModuleSelectionGrid();

        // Ensure managers are found
        if (_inventoryManager == null || _customizationManager == null) {
            FindManagers();
        }

        // Validate required references
        if (_inventoryManager == null || _customizationManager == null) {
            Debug.LogWarning("CoreCustomUIManager: Required managers not found for module selection grid refresh");
            OnModuleSelectionGridRefreshed?.Invoke();
            return;
        }

        if (moduleSelectionGridContainer == null || moduleSelectionCellPrefab == null) {
            Debug.LogWarning("CoreCustomUIManager: Module selection grid container or prefab is null");
            OnModuleSelectionGridRefreshed?.Invoke();
            return;
        }

        // Get all modules from inventory
        List<Module> allModules = _inventoryManager.GetAllModules();
        if (allModules == null) {
            allModules = new List<Module>();
        }

        // Collect module IDs from slots to exclude them from selection grid
        HashSet<string> moduleIdsInSlots = new HashSet<string>();
        for (int i = 0; i < 3; i++) {
            Module slotModule = _customizationManager.GetModuleInSlot(i);
            if (slotModule != null && !string.IsNullOrEmpty(slotModule.moduleId)) {
                moduleIdsInSlots.Add(slotModule.moduleId);
            }
        }

        // Create cells for modules not in slots
        foreach (Module module in allModules) {
            if (module == null) continue;

            // Check if module is in a slot by comparing moduleId
            bool isInSlot = !string.IsNullOrEmpty(module.moduleId) && moduleIdsInSlots.Contains(module.moduleId);
            if (isInSlot) {
                continue;
            }

            CreateModuleCell(module);
        }

        // Always invoke event to notify that grid has been refreshed (even if empty)
        OnModuleSelectionGridRefreshed?.Invoke();
    }

    private void CreateModuleCell(Module module)
    {
        if (moduleSelectionGridContainer == null || moduleSelectionCellPrefab == null) {
            return;
        }

        GameObject cellObj = Instantiate(moduleSelectionCellPrefab, moduleSelectionGridContainer);
        ModuleInventoryCell cell = cellObj.GetComponent<ModuleInventoryCell>();
        if (cell != null) {
            cell.Initialize(this);
            cell.SetModule(module);
            _moduleSelectionCells.Add(cell);
        }
    }

    private void ClearModuleSelectionGrid()
    {
        if (moduleSelectionGridContainer == null) return;

        foreach (Transform child in moduleSelectionGridContainer) {
            Destroy(child.gameObject);
        }

        _moduleSelectionCells.Clear();
    }

    public void OnModuleCellClicked(Module module)
    {
        // Validate managers
        if (_customizationManager == null || _inventoryManager == null) {
            FindManagers();
        }

        if (_customizationManager == null) {
            Debug.LogWarning("CoreCustomUIManager: Cannot set module - customization manager is null");
            return;
        }

        if (module == null) {
            Debug.LogWarning("CoreCustomUIManager: Cannot set null module to slot");
            return;
        }

        if (_inventoryManager == null) {
            Debug.LogError("CoreCustomUIManager: BaseInventoryManager를 찾을 수 없습니다.");
            return;
        }

        // Find first empty, unlocked slot
        int targetSlotIndex = FindEmptyUnlockedSlot();
        if (targetSlotIndex < 0) {
            Debug.LogWarning("CoreCustomUIManager: No empty, unlocked slot available for module placement");
            return;
        }

        // Remove module from inventory
        if (!_inventoryManager.RemoveModule(module)) {
            Debug.LogWarning($"CoreCustomUIManager: 인벤토리에서 모듈 '{module.moduleName}'을 제거할 수 없습니다.");
            return;
        }

        // Place module in slot
        _customizationManager.SetModuleInSlot(targetSlotIndex, module);
        
        // Force refresh BaseInventorySystem UI if it's open
        if (_baseInventorySystem != null) {
            _baseInventorySystem.ForceRefreshInventory();
        }

        // Refresh UI
        RefreshSlots();
        RefreshModuleSelectionGrid();
    }

    private int FindEmptyUnlockedSlot()
    {
        for (int i = 0; i < 3; i++) {
            if (!_customizationManager.IsSlotLocked(i) && _customizationManager.GetModuleInSlot(i) == null) {
                return i;
            }
        }
        return -1;
    }

    private void OnModuleSlotChanged(int slotIndex, Module module)
    {
        RefreshSlots();

        // Refresh module selection grid when slot changes
        if (IsPanelOpen()) {
            StartCoroutine(RefreshModuleSelectionGridDelayed());
        }

        // Update core detail panel when modules change
        if (coreDetailPanel != null) {
            coreDetailPanel.UpdateModuleEffects();
        }

        // Notify BaseInventorySystem to refresh if open
        if (_baseInventorySystem != null) {
            _baseInventorySystem.ForceRefreshInventory();
        }
    }

    private void OnModuleInventoryChanged(Module module)
    {
        // Refresh module selection grid when modules are added/removed
        // This ensures the UI updates when modules are created from ModuleStation
        if (IsPanelOpen()) {
            StartCoroutine(RefreshModuleSelectionGridDelayed());
        }
    }

    private IEnumerator RefreshModuleSelectionGridDelayed()
    {
        yield return null;
        RefreshModuleSelectionGrid();
    }

    private void RefreshSlots()
    {
        if (slotComponents == null) return;

        foreach (CoreCustomizationSlot slot in slotComponents) {
            if (slot != null) {
                slot.RefreshSlot();
            }
        }

        OnSlotsRefreshed?.Invoke();
    }
}
