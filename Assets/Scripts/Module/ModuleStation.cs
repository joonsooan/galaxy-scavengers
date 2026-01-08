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
    
    public bool CanCraftModule(ModuleRecipe recipe)
    {
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager == null)
        {
            Debug.LogWarning("ModuleStation: BaseInventoryManager not available");
            return false;
        }
        
        foreach (ResourceCost ingredient in recipe.ingredients)
        {
            int availableAmount = inventoryManager.GetResourceAmount(ingredient.resourceType);
            if (availableAmount < ingredient.amount)
            {
                return false;
            }
        }
        
        return true;
    }
    
    public bool CraftModule(ModuleRecipe recipe)
    {
        if (!CanCraftModule(recipe))
        {
            Debug.LogWarning($"ModuleStation: Cannot craft module {recipe.moduleName} - insufficient resources");
            return false;
        }
        
        // Check if inventory grid is full
        BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        if (inventorySystem != null && inventorySystem.IsGridFull())
        {
            Debug.Log($"ModuleStation: Cannot craft module {recipe.moduleName} - inventory grid is full!");
            return false;
        }
        
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager == null)
        {
            Debug.LogWarning("ModuleStation: BaseInventoryManager not available");
            return false;
        }
        
        foreach (ResourceCost ingredient in recipe.ingredients)
        {
            if (!inventoryManager.RemoveResource(ingredient.resourceType, ingredient.amount))
            {
                Debug.LogError($"ModuleStation: Failed to remove {ingredient.amount} {ingredient.resourceType} from base inventory");
                return false;
            }
        }
        
        Module newModule = new Module(recipe);
        inventoryManager.AddModule(newModule);
        
        Debug.Log($"ModuleStation: Successfully crafted module {newModule.moduleName} (ID: {newModule.moduleId}) and added to base inventory");
        
        return true;
    }
}

