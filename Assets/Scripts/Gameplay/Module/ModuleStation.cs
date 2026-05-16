using System;
using UnityEngine;

public class ModuleStation : MonoBehaviour
{
    [SerializeField] private ModuleData moduleData;

    public ModuleData ModuleData {
        get {
            return moduleData;
        }
    }

    public static event Action<ModuleStation> OnModuleStationClicked;

    public void OnClicked()
    {
        OnModuleStationClicked?.Invoke(this);
    }

    public bool CanCraftModule(ModuleRecipe recipe)
    {
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();

        foreach (ResourceCost ingredient in recipe.ingredients) {
            int availableAmount = inventoryManager.GetResourceAmount(ingredient.resourceType);
            if (availableAmount < ingredient.amount) {
                return false;
            }
        }

        return true;
    }

    public bool CraftModule(ModuleRecipe recipe)
    {
        if (!CanCraftModule(recipe)) {
            Debug.LogWarning($"ModuleStation: Cannot craft module {recipe.moduleName} - insufficient resources");
            return false;
        }

        BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        if (inventorySystem != null && inventorySystem.IsGridFull()) {
            Debug.Log($"ModuleStation: Cannot craft module {recipe.moduleName} - inventory grid is full!");
            return false;
        }

        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager == null) {
            Debug.LogWarning("ModuleStation: BaseInventoryManager not available");
            return false;
        }

        foreach (ResourceCost ingredient in recipe.ingredients) {
            if (!inventoryManager.RemoveResource(ingredient.resourceType, ingredient.amount)) {
                Debug.LogError($"ModuleStation: Failed to remove {ingredient.amount} {ingredient.resourceType} from base inventory");
                return false;
            }
        }

        Module newModule = new Module(recipe);
        inventoryManager.AddModule(newModule);

        return true;
    }
}
