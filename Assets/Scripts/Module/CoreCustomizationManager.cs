using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoreCustomizationManager : MonoBehaviour
{
    public static CoreCustomizationManager Instance { get; private set; }

    private const string SelectedModulesKey = "CoreCustomization_SelectedModules";
    private const string UnlockedSlotCountKey = "CoreCustomization_UnlockedSlotCount";
    private const int MaxSlots = 3;
    private readonly Module[] _selectedModules = new Module[MaxSlots];

    [SerializeField] private int unlockedSlotCount;

    public event Action<int, Module> OnModuleSlotChanged;
    public event Action<int> OnUnlockedSlotCountChanged;
    public static event Action<Module, int> OnModulePlacedOnCore;

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
        LoadUnlockedSlotCount();
        StartCoroutine(LoadSelectedModulesDelayed());
        SetUnlockedSlotCount(unlockedSlotCount);
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

        if (slotIndex >= unlockedSlotCount) {
            Debug.LogWarning($"CoreCustomizationManager: Cannot set module in slot {slotIndex} - slot is locked (only {unlockedSlotCount} slots unlocked)");
            return;
        }

        _selectedModules[slotIndex] = module;
        SaveSelectedModules();
        OnModuleSlotChanged?.Invoke(slotIndex, module);
        
        if (module != null)
        {
            OnModulePlacedOnCore?.Invoke(module, slotIndex);
        }
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

        if (slotIndex >= unlockedSlotCount) {
            Debug.LogWarning($"CoreCustomizationManager: Cannot remove module from slot {slotIndex} - slot is locked (only {unlockedSlotCount} slots unlocked)");
            return;
        }

        Module moduleToRemove = _selectedModules[slotIndex];
        
        if (moduleToRemove == null) {
            return;
        }

        _selectedModules[slotIndex] = null;
        SaveSelectedModules();
        
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager != null) {
            inventoryManager.AddModule(moduleToRemove);
            
            List<Module> modulesAfterAdd = inventoryManager.GetAllModules();
            if (modulesAfterAdd.Contains(moduleToRemove)) {
                Debug.Log($"CoreCustomizationManager: 모듈 '{moduleToRemove.moduleName}'을 슬롯 {slotIndex}에서 제거하고 인벤토리로 반환했습니다.");
            } else {
                Debug.LogError($"CoreCustomizationManager: 인벤토리가 가득 차서 모듈 '{moduleToRemove.moduleName}'을 반환할 수 없습니다.");
            }
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

    public bool IsSlotLocked(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots) {
            return true;
        }
        return slotIndex >= unlockedSlotCount;
    }

    public void SetUnlockedSlotCount(int count)
    {
        int newCount = Mathf.Clamp(count, 0, MaxSlots);
        unlockedSlotCount = newCount;
        SaveUnlockedSlotCount();
        OnUnlockedSlotCountChanged?.Invoke(unlockedSlotCount);
    }

    private void SaveSelectedModules()
    {
        List<ModuleSlotData> slotDataList = new List<ModuleSlotData>();
        foreach (Module module in _selectedModules) {
            if (module != null) {
                slotDataList.Add(new ModuleSlotData {
                    moduleName = module.moduleName,
                    moduleId = module.moduleId,
                    moduleType = module.moduleType,
                    moduleTier = module.moduleTier,
                    moduleIconPath = module.moduleIconPath
                });
            } else {
                slotDataList.Add(new ModuleSlotData { moduleName = "" });
            }
        }

        string json = JsonUtility.ToJson(new SelectedModulesData { slotDataList = slotDataList });
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

        if (data == null) {
            return;
        }

        // Try to load from new format first (with slotDataList)
        if (data.slotDataList != null && data.slotDataList.Count > 0) {
            LoadSelectedModulesFromSlotData(data.slotDataList);
            return;
        }

        // Fallback to old format (moduleNames) for backward compatibility
        if (data.moduleNames != null && data.moduleNames.Count > 0) {
            LoadSelectedModulesFromNames(data.moduleNames);
        }
    }

    private void LoadSelectedModulesFromSlotData(List<ModuleSlotData> slotDataList)
    {
        Dictionary<string, ModuleRecipe> recipeMap = GetAllModuleRecipes();

        for (int i = 0; i < Mathf.Min(slotDataList.Count, MaxSlots); i++) {
            ModuleSlotData slotData = slotDataList[i];
            if (string.IsNullOrEmpty(slotData.moduleName)) {
                _selectedModules[i] = null;
                continue;
            }

            // Try to find existing module in inventory first
            BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
            Module existingModule = null;
            if (inventoryManager != null) {
                List<Module> allModules = inventoryManager.GetAllModules();
                foreach (Module module in allModules) {
                    if (module.moduleName == slotData.moduleName && 
                        (!string.IsNullOrEmpty(slotData.moduleId) && module.moduleId == slotData.moduleId || string.IsNullOrEmpty(slotData.moduleId))) {
                        existingModule = module;
                        break;
                    }
                }
            }

            if (existingModule != null) {
                _selectedModules[i] = existingModule;
            } else if (recipeMap.TryGetValue(slotData.moduleName, out ModuleRecipe recipe)) {
                // Recreate module from recipe if not found in inventory
                Module recreatedModule = new Module(recipe);
                _selectedModules[i] = recreatedModule;
                // Debug.Log($"CoreCustomizationManager: Recreated module '{slotData.moduleName}' for slot {i} from recipe");
            } else {
                _selectedModules[i] = null;
                Debug.LogWarning($"CoreCustomizationManager: Could not find recipe for module '{slotData.moduleName}' in slot {i}");
            }
        }
    }

    private void LoadSelectedModulesFromNames(List<string> moduleNames)
    {
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

        Dictionary<string, ModuleRecipe> recipeMap = GetAllModuleRecipes();

        for (int i = 0; i < Mathf.Min(moduleNames.Count, MaxSlots); i++) {
            if (!string.IsNullOrEmpty(moduleNames[i])) {
                if (moduleMap.TryGetValue(moduleNames[i], out Module module)) {
                    _selectedModules[i] = module;
                } else if (recipeMap.TryGetValue(moduleNames[i], out ModuleRecipe recipe)) {
                    // Recreate from recipe if not in inventory
                    Module recreatedModule = new Module(recipe);
                    _selectedModules[i] = recreatedModule;
                    Debug.Log($"CoreCustomizationManager: Recreated module '{moduleNames[i]}' for slot {i} from recipe (backward compatibility)");
                } else {
                    _selectedModules[i] = null;
                }
            } else {
                _selectedModules[i] = null;
            }
        }
    }

    private Dictionary<string, ModuleRecipe> GetAllModuleRecipes()
    {
        Dictionary<string, ModuleRecipe> recipeMap = new Dictionary<string, ModuleRecipe>();

        // Load ModuleRecipe ScriptableObjects directly from Resources
        ModuleRecipe[] allRecipes = Resources.LoadAll<ModuleRecipe>("");
        foreach (ModuleRecipe recipe in allRecipes) {
            if (recipe != null && !string.IsNullOrEmpty(recipe.moduleName) && !recipeMap.ContainsKey(recipe.moduleName)) {
                recipeMap[recipe.moduleName] = recipe;
            }
        }

        // Also load from ModuleData (for backward compatibility and module stations)
        ModuleData[] allModuleData = Resources.LoadAll<ModuleData>("");
        foreach (ModuleData moduleData in allModuleData) {
            if (moduleData.Recipes != null) {
                foreach (ModuleRecipe recipe in moduleData.Recipes) {
                    if (recipe != null && !string.IsNullOrEmpty(recipe.moduleName) && !recipeMap.ContainsKey(recipe.moduleName)) {
                        recipeMap[recipe.moduleName] = recipe;
                    }
                }
            }
        }

        // Also check ModuleStations in the scene
        ModuleStation[] moduleStations = FindObjectsByType<ModuleStation>(FindObjectsSortMode.None);
        foreach (ModuleStation station in moduleStations) {
            if (station.ModuleData != null && station.ModuleData.Recipes != null) {
                foreach (ModuleRecipe recipe in station.ModuleData.Recipes) {
                    if (recipe != null && !string.IsNullOrEmpty(recipe.moduleName) && !recipeMap.ContainsKey(recipe.moduleName)) {
                        recipeMap[recipe.moduleName] = recipe;
                    }
                }
            }
        }

        return recipeMap;
    }

    private void SaveUnlockedSlotCount()
    {
        PlayerPrefs.SetInt(UnlockedSlotCountKey, unlockedSlotCount);
        PlayerPrefs.Save();
    }

    private void LoadUnlockedSlotCount()
    {
        // if (PlayerPrefs.HasKey(UnlockedSlotCountKey)) {
        //     unlockedSlotCount = PlayerPrefs.GetInt(UnlockedSlotCountKey, MaxSlots);
        // }
        unlockedSlotCount = Mathf.Clamp(unlockedSlotCount, 0, MaxSlots);
    }

    public void DeleteCurrentCoreModules()
    {
        for (int i = 0; i < MaxSlots; i++) {
            if (_selectedModules[i] != null) {
                _selectedModules[i] = null;
            }
        }
        SaveSelectedModules();
        
        // Invoke events for all cleared slots
        for (int i = 0; i < MaxSlots; i++) {
            OnModuleSlotChanged?.Invoke(i, null);
        }
        
        Debug.Log("CoreCustomizationManager: All core modules deleted");
    }

    [System.Serializable]
    private class SelectedModulesData
    {
        public List<string> moduleNames;
        public List<ModuleSlotData> slotDataList;
    }

    [Serializable]
    private class ModuleSlotData
    {
        public string moduleName;
        public string moduleId;
        public ModuleType moduleType;
        public int moduleTier;
        public string moduleIconPath;
    }
}
