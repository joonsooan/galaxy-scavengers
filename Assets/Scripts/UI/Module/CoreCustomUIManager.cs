using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
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
    [SerializeField] private GameObject emptyModuleText;

    [Header("Core Detail Panel")]
    [SerializeField] private CoreDetailPanel coreDetailPanel;

    [SerializeField] private List<ModuleInventoryCell> moduleSelectionCells = new ();

    private CoreCustomizationManager _customizationManager;
    private BaseInventoryManager _inventoryManager;
    private BaseInventorySystem _baseInventorySystem;

    private void Start()
    {
        FindManagers();
        InitializeUI();
        SubscribeToEvents();
        InitializeSlots();
        ApplyEmptyModulePlaceholderText();
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
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
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
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        if (_customizationManager != null) {
            _customizationManager.OnModuleSlotChanged -= OnModuleSlotChanged;
        }

        if (_inventoryManager != null) {
            _inventoryManager.OnModuleAdded -= OnModuleInventoryChanged;
            _inventoryManager.OnModuleRemoved -= OnModuleInventoryChanged;
        }
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyEmptyModulePlaceholderText();
        RefreshSlots();
        RefreshModuleSelectionGrid();
        if (coreDetailPanel != null)
        {
            coreDetailPanel.UpdateModuleEffects();
        }
    }

    private void ApplyEmptyModulePlaceholderText()
    {
        if (emptyModuleText == null)
        {
            return;
        }

        TMP_Text tmp = emptyModuleText.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = GameLocalization.GetOrDefault("UI_Common", "placeholder.moduleDataAsset",
                "Module Data Scriptable object");
        }
    }

    private static readonly WaitForSeconds _wait01 = CoroutineCache.GetWaitForSeconds(0.1f);

    private IEnumerator WaitForModulesAndRefreshSlots()
    {
        if (_customizationManager == null) {
            yield break;
        }

        yield return _wait01;

        yield return new WaitUntil(() => {
            BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
            return inventoryManager != null;
        });

        yield return _wait01;

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
            StartCoroutine(RefreshOnPanelShown());
        }

        ShowShopUI();
    }

    private IEnumerator RefreshOnPanelShown()
    {
        yield return null;
        
        if (_inventoryManager == null || _customizationManager == null) {
            FindManagers();
        }
        
        RefreshModuleSelectionGrid();
        RefreshSlots();
        
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

    public void RefreshModuleSelectionGrid()
    {
        ClearModuleSelectionGrid();

        if (_inventoryManager == null || _customizationManager == null) {
            FindManagers();
        }

        if (_inventoryManager == null || _customizationManager == null) {
            UpdateEmptyModuleTextVisibility();
            return;
        }

        List<Module> allModules = _inventoryManager.GetAllModules();
        if (allModules == null) {
            allModules = new List<Module>();
        }

        HashSet<string> moduleIdsInSlots = new HashSet<string>();
        for (int i = 0; i < 3; i++) {
            Module slotModule = _customizationManager.GetModuleInSlot(i);
            if (slotModule != null && !string.IsNullOrEmpty(slotModule.moduleId)) {
                moduleIdsInSlots.Add(slotModule.moduleId);
            }
        }

        foreach (Module module in allModules) {
            if (module == null) continue;

            bool isInSlot = !string.IsNullOrEmpty(module.moduleId) && moduleIdsInSlots.Contains(module.moduleId);
            if (isInSlot) {
                continue;
            }

            CreateModuleCell(module);
        }

        UpdateEmptyModuleTextVisibility();
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
            moduleSelectionCells.Add(cell);
        }
    }

    private void ClearModuleSelectionGrid()
    {
        if (moduleSelectionGridContainer == null) return;

        foreach (Transform child in moduleSelectionGridContainer) {
            Destroy(child.gameObject);
        }

        moduleSelectionCells.Clear();
    }

    public void OnModuleCellClicked(Module module)
    {
        if (_customizationManager == null || _inventoryManager == null) {
            FindManagers();
        }
        
        int targetSlotIndex = FindEmptySlot();
        if (targetSlotIndex < 0) {
            Debug.LogWarning("CoreCustomUIManager: No empty slot available for module placement");
            return;
        }

        if (!_inventoryManager.RemoveModule(module)) {
            Debug.LogWarning($"CoreCustomUIManager: 인벤토리에서 모듈 '{module.moduleName}'을 제거할 수 없습니다.");
            return;
        }

        _customizationManager.SetModuleInSlot(targetSlotIndex, module);
        
        if (_baseInventorySystem != null) {
            _baseInventorySystem.ForceRefreshInventory();
        }

        RefreshSlots();
        RefreshModuleSelectionGrid();
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < 3; i++) {
            if (_customizationManager.GetModuleInSlot(i) == null) {
                return i;
            }
        }
        return -1;
    }

    private void OnModuleSlotChanged(int slotIndex, Module module)
    {
        RefreshSlots();

        if (IsPanelOpen()) {
            StartCoroutine(RefreshModuleSelectionGridDelayed());
        }

        coreDetailPanel.UpdateModuleEffects();
        if (_baseInventorySystem != null)
        {
            _baseInventorySystem.ForceRefreshInventory();
        }
    }

    private void OnModuleInventoryChanged(Module module)
    {
        if (IsPanelOpen()) {
            StartCoroutine(RefreshModuleSelectionGridDelayed());
        }

        UpdateEmptyModuleTextVisibility();
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
    }
    
    public void ShowShopUI()
    {
        if (moduleSelectionPanel != null)
        {
            moduleSelectionPanel.SetActive(true);
        }
        RefreshModuleSelectionGrid();
        UpdateEmptyModuleTextVisibility();
    }
    
    public void HideShopUI()
    {
        if (moduleSelectionPanel != null)
        {
            moduleSelectionPanel.SetActive(false);
        }
        UpdateEmptyModuleTextVisibility();
    }

    private void UpdateEmptyModuleTextVisibility()
    {
        if (emptyModuleText == null)
        {
            return;
        }

        bool isModulePanelVisible = moduleSelectionPanel != null && moduleSelectionPanel.activeInHierarchy;
        int moduleCount = 0;

        if (_inventoryManager == null)
        {
            FindManagers();
        }

        if (_inventoryManager != null)
        {
            List<Module> modules = _inventoryManager.GetAllModules();
            if (modules != null)
            {
                moduleCount = modules.Count;
            }
        }

        emptyModuleText.SetActive(isModulePanelVisible && moduleCount == 0);
    }
    
    public void ClearDetailPanel()
    {
        if (coreDetailPanel != null)
        {
            coreDetailPanel.ClearInfo();
        }
    }
}
