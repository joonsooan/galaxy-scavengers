using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResourceTransferManager : MonoBehaviour
{
    public static ResourceTransferManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Transfer all resources from game scene inventory to base inventory
    public void TransferGameInventoryToBase(InventorySystem gameInventory)
    {
        if (gameInventory == null || BaseInventoryManager.Instance == null)
        {
            Debug.LogWarning("ResourceTransferManager: Cannot transfer - missing inventory systems");
            return;
        }

        // Get all inventory cells from game inventory
        // We need to access the private cells, so we'll use a public method
        // For now, we'll create a method in InventorySystem to get all resources
        Dictionary<ResourceType, int> resourcesToTransfer = gameInventory.GetAllResourcesFromInventory();

        if (resourcesToTransfer == null || resourcesToTransfer.Count == 0)
        {
            Debug.Log("ResourceTransferManager: No resources to transfer");
            return;
        }

        // Transfer each resource type to base inventory
        foreach (var kvp in resourcesToTransfer)
        {
            if (kvp.Value > 0)
            {
                BaseInventoryManager.Instance.AddResource(kvp.Key, kvp.Value);
            }
        }

        // Clear the game inventory
        gameInventory.ClearAllInventoryCells();

        Debug.Log($"ResourceTransferManager: Transferred {resourcesToTransfer.Count} resource types to base inventory");
    }

    // Transfer specific resource type from game scene to base scene
    public void TransferResourceTypeToBase(ResourceType type, int amount)
    {
        if (BaseInventoryManager.Instance == null)
        {
            Debug.LogWarning("ResourceTransferManager: BaseInventoryManager not available");
            return;
        }

        BaseInventoryManager.Instance.AddResource(type, amount);
    }
}

