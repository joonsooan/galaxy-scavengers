using UnityEngine;

[System.Serializable]
public class ModuleRecipe
{
    [Header("Module Info")]
    public string moduleName;
    public string moduleDescription;
    public Sprite moduleIcon;
    
    [Header("Crafting Requirements")]
    public ResourceCost[] ingredients;
    
    [Header("Module Properties")]
    public ModuleType moduleType;
    public int moduleTier = 1;
}

public enum ModuleType
{
    Power,
    Defense,
    Offense,
    Utility,
    Production,
    Research
}

