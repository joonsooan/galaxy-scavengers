using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ModuleStation : MonoBehaviour
{
    [SerializeField] private ModuleData moduleData;
    
    public ModuleData ModuleData => moduleData;
    
    public static event Action<ModuleStation> OnModuleStationClicked;
    
    private void Awake()
    {
        if (moduleData == null)
        {
            Debug.LogWarning($"ModuleStation {name}: ModuleData is not assigned!");
        }
    }
    
    public void OnClicked()
    {
        OnModuleStationClicked?.Invoke(this);
    }
    
    // Check if we have enough resources in base inventory to craft a module
    public bool CanCraftModule(ModuleRecipe recipe)
    {
        if (BaseInventoryManager.Instance == null)
        {
            Debug.LogWarning("ModuleStation: BaseInventoryManager not available");
            return false;
        }
        
        // Check if we have all required ingredients
        foreach (ResourceCost ingredient in recipe.ingredients)
        {
            int availableAmount = BaseInventoryManager.Instance.GetResourceAmount(ingredient.resourceType);
            if (availableAmount < ingredient.amount)
            {
                return false;
            }
        }
        
        return true;
    }
    
    // Craft a module using resources from base inventory and add it to base inventory
    public bool CraftModule(ModuleRecipe recipe)
    {
        if (!CanCraftModule(recipe))
        {
            Debug.LogWarning($"ModuleStation: Cannot craft module {recipe.moduleName} - insufficient resources");
            return false;
        }
        
        if (BaseInventoryManager.Instance == null)
        {
            Debug.LogWarning("ModuleStation: BaseInventoryManager not available");
            return false;
        }
        
        // Consume resources from base inventory
        foreach (ResourceCost ingredient in recipe.ingredients)
        {
            if (!BaseInventoryManager.Instance.RemoveResource(ingredient.resourceType, ingredient.amount))
            {
                Debug.LogError($"ModuleStation: Failed to remove {ingredient.amount} {ingredient.resourceType} from base inventory");
                // Rollback would be needed here, but for simplicity we'll just fail
                return false;
            }
        }
        
        // Create the module and add it to base inventory
        Module newModule = new Module(recipe);
        BaseInventoryManager.Instance.AddModule(newModule);
        
        Debug.Log($"ModuleStation: Successfully crafted module {newModule.moduleName} (ID: {newModule.moduleId}) and added to base inventory");
        
        return true;
    }
}

