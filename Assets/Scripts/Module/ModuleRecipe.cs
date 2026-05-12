using UnityEngine;

public enum ModuleType
{
    Default,
    Power,
    Defense,
    Offense,
    Utility,
    Production,
    Research
}

[CreateAssetMenu(fileName = "New Module Recipe", menuName = "Module/Module Recipe")]
public class ModuleRecipe : ScriptableObject
{
    [Header("Module Info")]
    public string moduleName;
    [TextArea(3, 10)] public string moduleDescription;
    public Sprite moduleIcon;

    [Header("Localization")]
    [SerializeField] private string localizationTable = "GameData";
    [SerializeField] private string moduleNameKey;
    [SerializeField] private string moduleDescriptionKey;
    
    [Header("Crafting Requirements")]
    public ResourceCost[] ingredients;
    
    [Header("Module Properties")]
    public ModuleType moduleType;
    public int moduleTier = 1;
    
    [Header("Module Effect")]
    public ModuleEffectData effectData;

    public string LocalizationTable => localizationTable;
    public string ModuleNameKey => moduleNameKey;
    public string ModuleDescriptionKey => moduleDescriptionKey;

    public string GetDisplayName()
    {
        string fallback = string.IsNullOrEmpty(moduleName) ? name : moduleName;
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

