using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BaseInventoryDebugManager : MonoBehaviour
{
    [Header("Debug Panel")]
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private Button toggleDebugPanelButton;
    [SerializeField] private Button transferFromGameButton;
    [SerializeField] private Button setTestResourcesButton;
    [SerializeField] private Button clearAllResourcesButton;
    
    [Header("Test Resource Amounts")]
    [SerializeField] private int testFerrite = 100;
    [SerializeField] private int testAether = 100;
    [SerializeField] private int testBiomass = 100;
    [SerializeField] private int testCryoCrystal = 100;
    [SerializeField] private int testAlloyPlate = 50;
    [SerializeField] private int testCompositeFrame = 50;
    [SerializeField] private int testEChip = 50;
    [SerializeField] private int testBioCable = 50;
    [SerializeField] private int testPowerCube = 50;
    [SerializeField] private int testBioFuel = 50;
    [SerializeField] private int testCryoGel = 50;
    [SerializeField] private int testSolana = 50;
    [SerializeField] private int testCore = 10;
    [SerializeField] private int testAmmunition = 50;
    [SerializeField] private int testHeavyPlating = 25;
    [SerializeField] private int testActuator = 25;
    [SerializeField] private int testGenomeChip = 25;
    [SerializeField] private int testPatchKit = 25;
    [SerializeField] private int testSensorUnit = 25;
    [SerializeField] private int testPlasmaCube = 25;
    [SerializeField] private int testCryoConduit = 25;
    [SerializeField] private int testSeekerMissile = 25;
    [SerializeField] private int testNexusData = 25;
    [SerializeField] private int testNeuralMatrix = 25;
    
    [Header("Transfer Settings")]
    [SerializeField] private bool transferAllResources = true;
    [SerializeField] private int transferPercentage = 100;
    
    private void Start()
    {
        if (toggleDebugPanelButton != null)
        {
            toggleDebugPanelButton.onClick.AddListener(ToggleDebugPanel);
        }
        
        if (transferFromGameButton != null)
        {
            transferFromGameButton.onClick.AddListener(TransferFromGameInventory);
        }
        
        if (setTestResourcesButton != null)
        {
            setTestResourcesButton.onClick.AddListener(SetTestResources);
        }
        
        if (clearAllResourcesButton != null)
        {
            clearAllResourcesButton.onClick.AddListener(ClearAllResources);
        }
        
        if (debugPanel != null)
        {
            debugPanel.SetActive(false);
        }
    }
    
    private void ToggleDebugPanel()
    {
        if (debugPanel != null)
        {
            debugPanel.SetActive(!debugPanel.activeSelf);
        }
    }
    
    private void SetTestResources()
    {
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        int resourceIconCount = 0;
        BaseResourceDataManager resourceDataManager = FindFirstObjectByType<BaseResourceDataManager>();
        if (resourceDataManager != null)
        {
            resourceIconCount = resourceDataManager.GetResourceIconCount();
        }
        
        if (resourceIconCount == 0)
        {
            Debug.LogWarning("BaseInventoryDebugManager: No resource icons found in BaseResourceDataManager");
            return;
        }
        
        ResourceType[] allResourceTypes = (ResourceType[])Enum.GetValues(typeof(ResourceType));
        int[] testAmounts = {
            testFerrite, testAether, testBiomass, testCryoCrystal,
            testAlloyPlate, testCompositeFrame, testEChip, testBioCable,
            testPowerCube, testBioFuel, testCryoGel, testSolana,
            testCore, testAmmunition, testHeavyPlating, testActuator,
            testGenomeChip, testPatchKit, testSensorUnit, testPlasmaCube,
            testCryoConduit, testSeekerMissile, testNexusData, testNeuralMatrix
        };
        
        int countToSet = Mathf.Min(resourceIconCount, allResourceTypes.Length, testAmounts.Length);
        
        for (int i = 0; i < countToSet; i++)
        {
            if (i < allResourceTypes.Length && i < testAmounts.Length)
            {
                inventoryManager.AddResource(allResourceTypes[i], testAmounts[i]);
            }
        }
        
        BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        if (inventorySystem != null)
        {
            inventorySystem.ForceRefreshInventory();
        }
        
        CoreCustomUIManager coreCustomUIManager = FindFirstObjectByType<CoreCustomUIManager>();
        if (coreCustomUIManager != null)
        {
            coreCustomUIManager.RefreshModuleSelectionGrid();
        }
    }
    
    private void ClearAllResources()
    {
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
       
        inventoryManager.ClearAllInventory();
        
        BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        if (inventorySystem != null)
        {
            inventorySystem.ForceRefreshInventory();
        }
        
        CoreCustomUIManager coreCustomUIManager = FindFirstObjectByType<CoreCustomUIManager>();
        if (coreCustomUIManager != null)
        {
            coreCustomUIManager.RefreshModuleSelectionGrid();
        }
    }
    
    private void TransferFromGameInventory()
    {
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        
        Dictionary<ResourceType, int> resourcesToTransfer = new Dictionary<ResourceType, int>();
        
        InventorySystem gameInventory = FindFirstObjectByType<InventorySystem>();
        if (gameInventory != null)
        {
            Dictionary<ResourceType, int> inventoryResources = gameInventory.GetAllResourcesFromInventory();
            foreach (var kvp in inventoryResources)
            {
                if (kvp.Value > 0)
                {
                    resourcesToTransfer[kvp.Key] = kvp.Value;
                }
            }
            
            if (resourcesToTransfer.Count > 0)
            {
                gameInventory.ClearAllInventoryCells();
            }
        }
        
        if (ResourceManager.Instance != null)
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int amount = ResourceManager.Instance.GetResourceAmount(type);
                if (amount > 0)
                {
                    if (resourcesToTransfer.ContainsKey(type))
                    {
                        resourcesToTransfer[type] += amount;
                    }
                    else
                    {
                        resourcesToTransfer[type] = amount;
                    }
                }
            }
        }
        
        if (resourcesToTransfer.Count == 0)
        {
            Debug.Log("BaseInventoryDebugManager: No resources available in game scene");
            return;
        }
        
        int transferredCount = 0;
        int totalTransferred = 0;
        foreach (var kvp in resourcesToTransfer)
        {
            int amountToTransfer = transferAllResources 
                ? kvp.Value 
                : Mathf.RoundToInt(kvp.Value * (transferPercentage / 100f));
            
            if (amountToTransfer > 0)
            {
                inventoryManager.AddResource(kvp.Key, amountToTransfer);
                transferredCount++;
                totalTransferred += amountToTransfer;
            }
        }
        
        Debug.Log($"BaseInventoryDebugManager: Transferred {transferredCount} resource types ({totalTransferred} total items) from game scene to base inventory");
        
        BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        if (inventorySystem != null)
        {
            inventorySystem.ForceRefreshInventory();
        }
        
        CoreCustomUIManager coreCustomUIManager = FindFirstObjectByType<CoreCustomUIManager>();
        if (coreCustomUIManager != null)
        {
            coreCustomUIManager.RefreshModuleSelectionGrid();
        }
    }
    
#if UNITY_EDITOR
    [ContextMenu("Set Test Resources")]
    private void SetTestResourcesContextMenu()
    {
        SetTestResources();
    }
    
    [ContextMenu("Clear All Resources")]
    private void ClearAllResourcesContextMenu()
    {
        ClearAllResources();
    }
    
    [ContextMenu("Transfer From Game Scene")]
    private void TransferFromGameInventoryContextMenu()
    {
        TransferFromGameInventory();
    }
#endif
}

