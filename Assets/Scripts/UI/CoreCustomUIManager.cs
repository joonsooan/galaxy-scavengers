using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    [SerializeField] private Button closeModuleSelectionButton;

    private readonly List<ModuleInventoryCell> _moduleSelectionCells = new List<ModuleInventoryCell>();

    private CoreCustomizationManager _customizationManager;
    private BaseInventoryManager _inventoryManager;
    private int _currentSelectedSlotIndex = -1;

    private void Start()
    {
        _customizationManager = FindFirstObjectByType<CoreCustomizationManager>();
        _inventoryManager = FindFirstObjectByType<BaseInventoryManager>();

        if (coreCustomPanel != null) {
            coreCustomPanel.SetActive(false);
        }

        if (closeButton != null) {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        if (closeModuleSelectionButton != null) {
            closeModuleSelectionButton.onClick.RemoveAllListeners();
            closeModuleSelectionButton.onClick.AddListener(HideModuleSelectionPanel);
        }

        InitializeSlots();
        StartCoroutine(WaitForModulesAndRefreshSlots());
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

    private void OnEnable()
    {
        if (_customizationManager != null) {
            _customizationManager.OnModuleSlotChanged += OnModuleSlotChanged;
        }

        if (_inventoryManager != null) {
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

    private void InitializeSlots()
    {
        if (slotComponents == null || _customizationManager == null) return;

        for (int i = 0; i < slotComponents.Length; i++) {
            if (slotComponents[i] != null) {
                slotComponents[i].Initialize(i, _customizationManager, this);
            }
        }
    }

    public void ShowCoreCustomPanel()
    {
        if (coreCustomPanel != null) {
            coreCustomPanel.SetActive(true);
        }

        if (moduleSelectionPanel != null) {
            moduleSelectionPanel.SetActive(true);
        }

        RefreshSlots();
        RefreshModuleSelectionGrid();
    }

    public void HideCoreCustomPanel()
    {
        HidePanel();
    }

    private void HidePanel()
    {
        if (coreCustomPanel != null) {
            coreCustomPanel.SetActive(false);
        }

        _currentSelectedSlotIndex = -1;
    }

    private void OnCloseButtonClicked()
    {
        HidePanel();
    }

    public void OnSlotClicked(int slotIndex)
    {
        _currentSelectedSlotIndex = slotIndex;
    }

    public void HideModuleSelectionPanel()
    {
        _currentSelectedSlotIndex = -1;
        if (coreCustomPanel != null && coreCustomPanel.activeSelf) {
            if (moduleSelectionPanel != null) {
                moduleSelectionPanel.SetActive(true);
            }
        }
    }

    private void RefreshModuleSelectionGrid()
    {
        ClearModuleSelectionGrid();

        if (_inventoryManager == null || _customizationManager == null || moduleSelectionGridContainer == null || moduleSelectionCellPrefab == null) {
            return;
        }

        List<Module> allModules = _inventoryManager.GetAllModules();
        if (allModules == null || allModules.Count == 0) {
            return;
        }

        List<Module> modulesInSlots = new List<Module>();
        for (int i = 0; i < 3; i++) {
            Module slotModule = _customizationManager.GetModuleInSlot(i);
            if (slotModule != null) {
                modulesInSlots.Add(slotModule);
            }
        }

        foreach (Module module in allModules) {
            if (module == null) continue;

            bool isInSlot = modulesInSlots.Contains(module);
            if (isInSlot) {
                continue;
            }

            GameObject cellObj = Instantiate(moduleSelectionCellPrefab, moduleSelectionGridContainer);
            ModuleInventoryCell cell = cellObj.GetComponent<ModuleInventoryCell>();
            if (cell != null) {
                cell.Initialize(this);
                cell.SetModule(module);
                _moduleSelectionCells.Add(cell);
            }
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
        if (_customizationManager == null || _currentSelectedSlotIndex < 0) {
            Debug.LogWarning("CoreCustomUIManager: Cannot set module - customization manager is null or no slot selected");
            return;
        }

        if (module == null) {
            Debug.LogWarning("CoreCustomUIManager: Cannot set null module to slot");
            return;
        }

        if (_inventoryManager == null) {
            _inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        }

        if (_inventoryManager == null) {
            Debug.LogError("CoreCustomUIManager: BaseInventoryManager를 찾을 수 없습니다.");
            return;
        }

        Module previousModule = _customizationManager.GetModuleInSlot(_currentSelectedSlotIndex);

        if (previousModule != null) {
            _inventoryManager.AddModule(previousModule);
            Debug.Log($"CoreCustomUIManager: 이전 모듈 '{previousModule.moduleName}'을 인벤토리로 반환했습니다.");
        }

        if (!_inventoryManager.RemoveModule(module)) {
            Debug.LogWarning($"CoreCustomUIManager: 인벤토리에서 모듈 '{module.moduleName}'을 제거할 수 없습니다.");
            return;
        }

        Debug.Log($"CoreCustomUIManager: 인벤토리에서 모듈 '{module.moduleName}'을 제거하고 슬롯 {_currentSelectedSlotIndex}에 설정합니다.");
        _customizationManager.SetModuleInSlot(_currentSelectedSlotIndex, module);
        
        Module setModule = _customizationManager.GetModuleInSlot(_currentSelectedSlotIndex);
        if (setModule != null) {
            Debug.Log($"CoreCustomUIManager: 성공적으로 모듈 '{setModule.moduleName}'을 슬롯 {_currentSelectedSlotIndex}에 설정했습니다.");
        } else {
            Debug.LogWarning($"CoreCustomUIManager: 슬롯 {_currentSelectedSlotIndex}에 모듈 설정에 실패했습니다.");
        }
        
        RefreshSlots();
        RefreshModuleSelectionGrid();
    }

    private void OnModuleSlotChanged(int slotIndex, Module module)
    {
        RefreshSlots();
        
        if (_inventoryManager == null) {
            _inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        }
        
        StartCoroutine(RefreshModuleSelectionGridDelayed());
    }

    private IEnumerator RefreshModuleSelectionGridDelayed()
    {
        yield return null;
        RefreshModuleSelectionGrid();
    }

    private void OnModuleInventoryChanged(Module module)
    {
        if (coreCustomPanel != null && coreCustomPanel.activeSelf) {
            RefreshModuleSelectionGrid();
        }
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
}
