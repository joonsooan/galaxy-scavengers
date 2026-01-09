using UnityEngine;

[System.Serializable]
public class ModuleRecipe
{
    [Header("Module Info")]
    public string moduleName;
    [TextArea(3, 10)] public string moduleDescription;
    public Sprite moduleIcon;
    
    [Header("Crafting Requirements")]
    public ResourceCost[] ingredients;
    
    [Header("Module Properties")]
    public ModuleType moduleType;
    public int moduleTier = 1;
    
    [Header("Module Effect")]
    public ModuleEffectData effectData;
}

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

