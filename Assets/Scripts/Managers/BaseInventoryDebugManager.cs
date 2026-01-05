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
    [SerializeField] private int transferPercentage = 100; // Percentage of resources to transfer (0-100)
    
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
        if (BaseInventoryManager.Instance == null)
        {
            Debug.LogWarning("BaseInventoryDebugManager: BaseInventoryManager not available");
            return;
        }
        
        // Set all test resources
        BaseInventoryManager.Instance.AddResource(ResourceType.Ferrite, testFerrite);
        BaseInventoryManager.Instance.AddResource(ResourceType.Aether, testAether);
        BaseInventoryManager.Instance.AddResource(ResourceType.Biomass, testBiomass);
        BaseInventoryManager.Instance.AddResource(ResourceType.CryoCrystal, testCryoCrystal);
        BaseInventoryManager.Instance.AddResource(ResourceType.AlloyPlate, testAlloyPlate);
        BaseInventoryManager.Instance.AddResource(ResourceType.CompositeFrame, testCompositeFrame);
        BaseInventoryManager.Instance.AddResource(ResourceType.EChip, testEChip);
        BaseInventoryManager.Instance.AddResource(ResourceType.BioCable, testBioCable);
        BaseInventoryManager.Instance.AddResource(ResourceType.PowerCube, testPowerCube);
        BaseInventoryManager.Instance.AddResource(ResourceType.BioFuel, testBioFuel);
        BaseInventoryManager.Instance.AddResource(ResourceType.CryoGel, testCryoGel);
        BaseInventoryManager.Instance.AddResource(ResourceType.Solana, testSolana);
        BaseInventoryManager.Instance.AddResource(ResourceType.Core, testCore);
        BaseInventoryManager.Instance.AddResource(ResourceType.Ammunition, testAmmunition);
        BaseInventoryManager.Instance.AddResource(ResourceType.HeavyPlating, testHeavyPlating);
        BaseInventoryManager.Instance.AddResource(ResourceType.Actuator, testActuator);
        BaseInventoryManager.Instance.AddResource(ResourceType.GenomeChip, testGenomeChip);
        BaseInventoryManager.Instance.AddResource(ResourceType.PatchKit, testPatchKit);
        BaseInventoryManager.Instance.AddResource(ResourceType.SensorUnit, testSensorUnit);
        BaseInventoryManager.Instance.AddResource(ResourceType.PlasmaCube, testPlasmaCube);
        BaseInventoryManager.Instance.AddResource(ResourceType.CryoConduit, testCryoConduit);
        BaseInventoryManager.Instance.AddResource(ResourceType.SeekerMissile, testSeekerMissile);
        BaseInventoryManager.Instance.AddResource(ResourceType.NexusData, testNexusData);
        BaseInventoryManager.Instance.AddResource(ResourceType.NeuralMatrix, testNeuralMatrix);
        
        // Refresh inventory grid to show the new resources
        BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        if (inventorySystem != null)
        {
            inventorySystem.RefreshInventoryGrid();
        }
        
        Debug.Log("BaseInventoryDebugManager: Test resources set");
    }
    
    private void ClearAllResources()
    {
        if (BaseInventoryManager.Instance == null)
        {
            Debug.LogWarning("BaseInventoryDebugManager: BaseInventoryManager not available");
            return;
        }
        
        BaseInventoryManager.Instance.ClearAllInventory();
        
        // Refresh inventory grid to clear the display
        BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        if (inventorySystem != null)
        {
            inventorySystem.RefreshInventoryGrid();
        }
        
        Debug.Log("BaseInventoryDebugManager: All resources cleared");
    }
    
    private void TransferFromGameInventory()
    {
        if (BaseInventoryManager.Instance == null)
        {
            Debug.LogWarning("BaseInventoryDebugManager: BaseInventoryManager not available");
            return;
        }
        
        Dictionary<ResourceType, int> resourcesToTransfer = new Dictionary<ResourceType, int>();
        
        // First, try to get resources from game scene InventorySystem (inventory cells)
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
            
            // Clear the game inventory cells after transferring
            if (resourcesToTransfer.Count > 0)
            {
                gameInventory.ClearAllInventoryCells();
            }
        }
        
        // Also get resources from ResourceManager (stored resources)
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
        
        // Transfer resources to base inventory
        int transferredCount = 0;
        int totalTransferred = 0;
        foreach (var kvp in resourcesToTransfer)
        {
            int amountToTransfer = transferAllResources 
                ? kvp.Value 
                : Mathf.RoundToInt(kvp.Value * (transferPercentage / 100f));
            
            if (amountToTransfer > 0)
            {
                BaseInventoryManager.Instance.AddResource(kvp.Key, amountToTransfer);
                transferredCount++;
                totalTransferred += amountToTransfer;
            }
        }
        
        Debug.Log($"BaseInventoryDebugManager: Transferred {transferredCount} resource types ({totalTransferred} total items) from game scene to base inventory");
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

