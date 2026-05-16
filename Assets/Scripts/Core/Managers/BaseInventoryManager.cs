using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BaseInventoryManager : MonoBehaviour
{
    private const string BaseInventoryPrefix = "BaseInventory_";
    
    private readonly Dictionary<ResourceType, int> _baseInventory = new();
    private readonly List<Module> _baseModules = new();

    public event Action<ResourceType, int> OnResourceChanged;
    public event Action<Module> OnModuleAdded;
    public event Action<Module> OnModuleRemoved;

    private void Start()
    {
        InitializeInventory();
    }

    private void InitializeInventory()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            _baseInventory[type] = 0;
        }
        
        LoadInventory();
    }

    public int GetResourceAmount(ResourceType type)
    {
        _baseInventory.TryGetValue(type, out int amount);
        return amount;
    }

    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        
        _baseInventory[type] = _baseInventory.GetValueOrDefault(type, 0) + amount;
        SaveResource(type);
        OnResourceChanged?.Invoke(type, _baseInventory[type]);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveAllInventory();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveAllInventory();
        }
    }

    public bool RemoveResource(ResourceType type, int amount)
    {
        if (amount <= 0) return false;
        
        int currentAmount = GetResourceAmount(type);
        if (currentAmount < amount)
        {
            return false;
        }
        
        _baseInventory[type] = currentAmount - amount;
        SaveResource(type);
        OnResourceChanged?.Invoke(type, _baseInventory[type]);
        return true;
    }

    public Dictionary<ResourceType, int> GetAllResources()
    {
        return new Dictionary<ResourceType, int>(_baseInventory);
    }

    private void SaveResource(ResourceType type)
    {
        string key = BaseInventoryPrefix + type;
        PlayerPrefs.SetInt(key, _baseInventory[type]);
        PlayerPrefs.Save();
    }

    private void LoadInventory()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            string key = BaseInventoryPrefix + type;
            if (PlayerPrefs.HasKey(key))
            {
                _baseInventory[type] = PlayerPrefs.GetInt(key);
            }
        }
        
        LoadModules();
    }

    private void SaveAllInventory()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            SaveResource(type);
        }
    }

    public void ClearAllInventory()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            int previousAmount = _baseInventory.GetValueOrDefault(type, 0);
            _baseInventory[type] = 0;
            string key = BaseInventoryPrefix + type;
            PlayerPrefs.DeleteKey(key);
            
            if (previousAmount > 0)
            {
                OnResourceChanged?.Invoke(type, 0);
            }
        }
        
        _baseModules.Clear();
        string modulesKey = BaseInventoryPrefix + "Modules";
        PlayerPrefs.DeleteKey(modulesKey);
        PlayerPrefs.Save();
    }
    
    public void AddModule(Module module)
    {
        if (module == null) return;
        
        _baseModules.Add(module);
        SaveModules();
        OnModuleAdded?.Invoke(module);
    }
    
    public bool RemoveModule(Module module)
    {
        if (module == null) return false;
        
        bool removed = _baseModules.Remove(module);
        if (removed)
        {
            SaveModules();
            OnModuleRemoved?.Invoke(module);
        }
        return removed;
    }
    
    public List<Module> GetAllModules()
    {
        return new List<Module>(_baseModules);
    }
    
    public int GetModuleCount()
    {
        return _baseModules.Count;
    }
    
    private void SaveModules()
    {
        string modulesKey = BaseInventoryPrefix + "Modules";
        List<string> moduleNames = _baseModules.Select(m => m.moduleName).ToList();
        List<ModuleSaveData> modules = _baseModules.Select(ModuleSaveData.FromModule).ToList();
        string json = JsonUtility.ToJson(new ModuleNameList { moduleNames = moduleNames, modules = modules });
        PlayerPrefs.SetString(modulesKey, json);
        PlayerPrefs.Save();
    }
    
    private void LoadModules()
    {
        string modulesKey = BaseInventoryPrefix + "Modules";
        if (!PlayerPrefs.HasKey(modulesKey))
        {
            // Debug.Log("BaseInventoryManager: 저장된 모듈이 없습니다.");
            return;
        }
        
        string json = PlayerPrefs.GetString(modulesKey);
        ModuleNameList moduleNameList = JsonUtility.FromJson<ModuleNameList>(json);
        
        if (moduleNameList == null)
        {
            Debug.LogWarning("BaseInventoryManager: 모듈 리스트를 파싱할 수 없습니다.");
            return;
        }
        
        Dictionary<string, ModuleRecipe> recipeMap = GetAllModuleRecipes();
        
        if (moduleNameList.modules != null && moduleNameList.modules.Count > 0)
        {
            foreach (ModuleSaveData savedModule in moduleNameList.modules)
            {
                if (TryCreateModuleFromSaveData(savedModule, recipeMap, out Module module))
                {
                    _baseModules.Add(module);
                }
            }

            return;
        }

        if (moduleNameList.moduleNames == null)
        {
            return;
        }

        foreach (string moduleName in moduleNameList.moduleNames)
        {
            if (recipeMap.TryGetValue(moduleName, out ModuleRecipe recipe))
            {
                Module module = new Module(recipe);
                _baseModules.Add(module);
            }
        }
    }

    private static bool TryCreateModuleFromSaveData(ModuleSaveData savedModule, Dictionary<string, ModuleRecipe> recipeMap,
        out Module module)
    {
        module = null;
        if (savedModule == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(savedModule.recipeAssetName)
            && recipeMap.TryGetValue(savedModule.recipeAssetName, out ModuleRecipe recipe))
        {
            module = new Module(recipe);
            RestoreSavedModuleIdentity(module, savedModule);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(savedModule.moduleName)
            && recipeMap.TryGetValue(savedModule.moduleName, out recipe))
        {
            module = new Module(recipe);
            RestoreSavedModuleIdentity(module, savedModule);
            return true;
        }

        return false;
    }

    private static void RestoreSavedModuleIdentity(Module module, ModuleSaveData savedModule)
    {
        if (!string.IsNullOrWhiteSpace(savedModule.moduleId))
        {
            module.moduleId = savedModule.moduleId;
        }
    }
    
    private Dictionary<string, ModuleRecipe> GetAllModuleRecipes()
    {
        Dictionary<string, ModuleRecipe> recipeMap = new Dictionary<string, ModuleRecipe>();
        
        // Load ModuleRecipe ScriptableObjects directly from Resources
        ModuleRecipe[] allRecipes = Resources.LoadAll<ModuleRecipe>("");
        foreach (ModuleRecipe recipe in allRecipes)
        {
            if (recipe == null)
            {
                continue;
            }

            AddRecipeLookup(recipeMap, recipe);
        }
        
        // Also load from ModuleData (for backward compatibility and module stations)
        ModuleData[] allModuleData = Resources.LoadAll<ModuleData>("");
        foreach (ModuleData moduleData in allModuleData)
        {
            if (moduleData.Recipes != null)
            {
                foreach (ModuleRecipe recipe in moduleData.Recipes)
                {
                    AddRecipeLookup(recipeMap, recipe);
                }
            }
        }
        
        // Also check ModuleStations in the scene
        ModuleStation[] moduleStations = FindObjectsByType<ModuleStation>(FindObjectsSortMode.None);
        foreach (ModuleStation station in moduleStations)
        {
            if (station.ModuleData != null && station.ModuleData.Recipes != null)
            {
                foreach (ModuleRecipe recipe in station.ModuleData.Recipes)
                {
                    AddRecipeLookup(recipeMap, recipe);
                }
            }
        }
        
        return recipeMap;
    }

    private static void AddRecipeLookup(Dictionary<string, ModuleRecipe> recipeMap, ModuleRecipe recipe)
    {
        if (recipe == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(recipe.name) && !recipeMap.ContainsKey(recipe.name))
        {
            recipeMap[recipe.name] = recipe;
        }

        if (!string.IsNullOrEmpty(recipe.moduleName) && !recipeMap.ContainsKey(recipe.moduleName))
        {
            recipeMap[recipe.moduleName] = recipe;
        }
    }
    
    [System.Serializable]
    private class ModuleNameList
    {
        public List<string> moduleNames;
        public List<ModuleSaveData> modules;
    }

    [System.Serializable]
    private class ModuleSaveData
    {
        public string recipeAssetName;
        public string moduleName;
        public string moduleId;

        public static ModuleSaveData FromModule(Module module)
        {
            return new ModuleSaveData
            {
                recipeAssetName = module.recipeAssetName,
                moduleName = module.moduleName,
                moduleId = module.moduleId
            };
        }
    }
}

