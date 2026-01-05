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
    
    [System.NonSerialized]
    public Sprite moduleIcon;
    
    public Module(ModuleRecipe recipe)
    {
        moduleName = recipe.moduleName;
        moduleDescription = recipe.moduleDescription;
        moduleIcon = recipe.moduleIcon;
        moduleIconPath = recipe.moduleIcon != null ? recipe.moduleIcon.name : "";
        moduleType = recipe.moduleType;
        moduleTier = recipe.moduleTier;
        moduleId = System.Guid.NewGuid().ToString();
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
    }
    
    public Module() { }
}

