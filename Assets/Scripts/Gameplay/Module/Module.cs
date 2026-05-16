using UnityEngine;

[System.Serializable]
public class Module
{
    public string moduleName;
    public string moduleDescription;
    public string moduleIconPath;
    public ModuleType moduleType;
    public int moduleTier;
    public string moduleId;
    public string recipeAssetName;
    public string localizationTable = "GameData";
    public string moduleNameKey;
    public string moduleDescriptionKey;
    
    [System.NonSerialized]
    public Sprite moduleIcon;
    
    [System.NonSerialized]
    public ModuleEffectData effectData;
    
    public Module(ModuleRecipe recipe)
    {
        moduleName = recipe.moduleName;
        moduleDescription = recipe.moduleDescription;
        moduleIcon = recipe.moduleIcon;
        moduleIconPath = recipe.moduleIcon != null ? recipe.moduleIcon.name : "";
        moduleType = recipe.moduleType;
        moduleTier = recipe.moduleTier;
        moduleId = System.Guid.NewGuid().ToString();
        recipeAssetName = recipe.name;
        localizationTable = recipe.LocalizationTable;
        moduleNameKey = recipe.ModuleNameKey;
        moduleDescriptionKey = recipe.ModuleDescriptionKey;
        effectData = recipe.effectData;
    }
    
    public Module(Module other)
    {
        moduleName = other.moduleName;
        moduleDescription = other.moduleDescription;
        moduleIcon = other.moduleIcon;
        moduleIconPath = other.moduleIconPath;
        moduleType = other.moduleType;
        moduleTier = other.moduleTier;
        moduleId = System.Guid.NewGuid().ToString();
        recipeAssetName = other.recipeAssetName;
        localizationTable = other.localizationTable;
        moduleNameKey = other.moduleNameKey;
        moduleDescriptionKey = other.moduleDescriptionKey;
        effectData = other.effectData;
    }
    
    public Module() { }

    public string GetDisplayName()
    {
        string fallback = string.IsNullOrEmpty(moduleName) ? recipeAssetName : moduleName;
        if (string.IsNullOrWhiteSpace(moduleNameKey))
        {
            return fallback;
        }

        return GameLocalization.GetOrDefault(localizationTable, moduleNameKey, fallback);
    }

    public string GetDescription()
    {
        string fallback = moduleDescription ?? string.Empty;
        if (string.IsNullOrWhiteSpace(moduleDescriptionKey))
        {
            return fallback;
        }

        return GameLocalization.GetOrDefault(localizationTable, moduleDescriptionKey, fallback);
    }
}

