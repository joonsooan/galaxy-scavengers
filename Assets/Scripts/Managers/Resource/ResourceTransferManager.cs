using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResourceTransferManager : MonoBehaviour
{
    public static ResourceTransferManager Instance { get; private set; }
    public static event Action OnResourceTransferCompleted;

    private Dictionary<ResourceType, int> _pendingResources = null;

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "BaseScene")
        {
            StartCoroutine(TransferPendingResourcesWhenReady());
        }
    }

    private IEnumerator TransferPendingResourcesWhenReady()
    {
        BaseInventoryManager inventoryManager = null;
        
        while (inventoryManager == null)
        {
            inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
            yield return null;
        }

        yield return null;

        if (_pendingResources != null && _pendingResources.Count > 0)
        {
            TransferPendingResources();
        }
    }

    public void StoreGameInventoryForTransfer(InventorySystem gameInventory)
    {
        Dictionary<ResourceType, int> resourcesToTransfer = gameInventory.GetAllResourcesFromInventory();

        if (resourcesToTransfer == null || resourcesToTransfer.Count == 0)
        {
            Debug.Log("ResourceTransferManager: No resources to transfer");
            return;
        }

        _pendingResources = resourcesToTransfer;
        gameInventory.ClearAllInventoryCells();

        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager != null)
        {
            TransferPendingResources();
        }
    }

    private void TransferPendingResources()
    {
        if (_pendingResources == null || _pendingResources.Count == 0)
        {
            return;
        }

        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        
        foreach (var kvp in _pendingResources)
        {
            if (kvp.Value > 0)
            {
                inventoryManager.AddResource(kvp.Key, kvp.Value);
            }
        }

        int resourceCount = _pendingResources.Count;
        _pendingResources = null;

        Debug.Log($"ResourceTransferManager: Transferred {resourceCount} resource types to base inventory");

        BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        if (inventorySystem != null)
        {
            inventorySystem.ForceRefreshInventory();
        }
        
        // Check if modules are placed on core
        if (CoreCustomizationManager.Instance != null)
        {
            List<Module> activeModules = CoreCustomizationManager.Instance.GetActiveModules();
            // The module placement event is already fired by CoreCustomizationManager when modules are set
            // Here we just fire the resource transfer completion event
        }
        
        OnResourceTransferCompleted?.Invoke();
    }
}

