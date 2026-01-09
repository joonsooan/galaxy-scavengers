using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoreCustomizationManager : MonoBehaviour
{
    public static CoreCustomizationManager Instance { get; private set; }

    private const string SelectedModulesKey = "CoreCustomization_SelectedModules";
    private const int MaxSlots = 3;

    private Module[] _selectedModules = new Module[MaxSlots];

    public event Action<int, Module> OnModuleSlotChanged;

    public IReadOnlyList<Module> SelectedModules => _selectedModules;

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartCoroutine(LoadSelectedModulesDelayed());
    }

    private IEnumerator LoadSelectedModulesDelayed()
    {
        BaseInventoryManager inventoryManager = null;
        int maxWaitFrames = 60;
        int waitFrames = 0;

        while (inventoryManager == null && waitFrames < maxWaitFrames) {
            inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
            if (inventoryManager == null) {
                yield return null;
                waitFrames++;
            }
        }

        yield return null;
        LoadSelectedModules();
        
        for (int i = 0; i < MaxSlots; i++) {
            if (_selectedModules[i] != null) {
                OnModuleSlotChanged?.Invoke(i, _selectedModules[i]);
            }
        }
    }

    public void SetModuleInSlot(int slotIndex, Module module)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots) {
            Debug.LogWarning($"CoreCustomizationManager: Invalid slot index {slotIndex}");
            return;
        }

        _selectedModules[slotIndex] = module;
        SaveSelectedModules();
        OnModuleSlotChanged?.Invoke(slotIndex, module);
    }

    public Module GetModuleInSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots) {
            return null;
        }
        return _selectedModules[slotIndex];
    }

    public void RemoveModuleFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots) {
            return;
        }

        Module moduleToRemove = _selectedModules[slotIndex];
        
        if (moduleToRemove == null) {
            return;
        }

        _selectedModules[slotIndex] = null;
        SaveSelectedModules();
        
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager == null) {
            Debug.LogError("CoreCustomizationManager: BaseInventoryManager를 찾을 수 없습니다. 모듈을 인벤토리로 반환할 수 없습니다.");
            OnModuleSlotChanged?.Invoke(slotIndex, null);
            return;
        }

        inventoryManager.AddModule(moduleToRemove);
        
        List<Module> modulesAfterAdd = inventoryManager.GetAllModules();
        if (modulesAfterAdd.Contains(moduleToRemove)) {
            Debug.Log($"CoreCustomizationManager: 모듈 '{moduleToRemove.moduleName}'을 슬롯 {slotIndex}에서 제거하고 인벤토리로 반환했습니다.");
        } else {
            Debug.LogError($"CoreCustomizationManager: 인벤토리가 가득 차서 모듈 '{moduleToRemove.moduleName}'을 반환할 수 없습니다.");
        }
        
        OnModuleSlotChanged?.Invoke(slotIndex, null);
    }

    public List<Module> GetActiveModules()
    {
        List<Module> activeModules = new List<Module>();
        foreach (Module module in _selectedModules) {
            if (module != null) {
                activeModules.Add(module);
            }
        }
        return activeModules;
    }

    private void SaveSelectedModules()
    {
        List<string> moduleNames = new List<string>();
        foreach (Module module in _selectedModules) {
            moduleNames.Add(module != null ? module.moduleName : "");
        }

        string json = JsonUtility.ToJson(new SelectedModulesData { moduleNames = moduleNames });
        PlayerPrefs.SetString(SelectedModulesKey, json);
        PlayerPrefs.Save();
    }

    private void LoadSelectedModules()
    {
        if (!PlayerPrefs.HasKey(SelectedModulesKey)) {
            return;
        }

        string json = PlayerPrefs.GetString(SelectedModulesKey);
        SelectedModulesData data = JsonUtility.FromJson<SelectedModulesData>(json);

        if (data == null || data.moduleNames == null) {
            return;
        }

        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager == null) {
            Debug.LogWarning("CoreCustomizationManager: BaseInventoryManager를 찾을 수 없습니다.");
            return;
        }

        List<Module> allModules = inventoryManager.GetAllModules();
        Dictionary<string, Module> moduleMap = new Dictionary<string, Module>();
        foreach (Module module in allModules) {
            if (!moduleMap.ContainsKey(module.moduleName)) {
                moduleMap[module.moduleName] = module;
            }
        }

        for (int i = 0; i < Mathf.Min(data.moduleNames.Count, MaxSlots); i++) {
            if (!string.IsNullOrEmpty(data.moduleNames[i]) && moduleMap.TryGetValue(data.moduleNames[i], out Module module)) {
                _selectedModules[i] = module;
            } else {
                _selectedModules[i] = null;
            }
        }
    }

    [System.Serializable]
    private class SelectedModulesData
    {
        public List<string> moduleNames;
    }
}
