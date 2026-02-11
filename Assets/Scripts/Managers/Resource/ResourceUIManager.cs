using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResourceUIManager : MonoBehaviour
{
    [Header("Resource Stats UI")]
    [SerializeField] private GameObject ferritePanel;
    [SerializeField] private GameObject aetherPanel;
    [SerializeField] private GameObject biomassPanel;
    [SerializeField] private GameObject cryoCrystalPanel;
    [SerializeField] private GameObject alloyPlatePanel;
    [SerializeField] private GameObject compositeFramePanel;
    [SerializeField] private GameObject eChipPanel;
    [SerializeField] private GameObject bioCablePanel;
    [SerializeField] private GameObject powerCubePanel;
    [SerializeField] private GameObject bioFuelPanel;
    [SerializeField] private GameObject cryoGelPanel;
    [SerializeField] private GameObject solanaPanel;
    [SerializeField] private GameObject corePanel;
    [SerializeField] private GameObject ammunitionPanel;
    [SerializeField] private GameObject heavyPlatingPanel;
    [SerializeField] private GameObject actuatorPanel;
    [SerializeField] private GameObject genomeChipPanel;
    [SerializeField] private GameObject patchKitPanel;
    [SerializeField] private GameObject sensorUnitPanel;
    [SerializeField] private GameObject plasmaCubePanel;
    [SerializeField] private GameObject cryoConduitPanel;
    [SerializeField] private GameObject seekerMissilePanel;
    [SerializeField] private GameObject nexusDataPanel;
    [SerializeField] private GameObject neuralMatrixPanel;

    private void OnEnable()
    {
        ResourceDataManager.OnResourceAmountChanged += OnResourceAmountChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        ResourceDataManager.OnResourceAmountChanged -= OnResourceAmountChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene") {
            StartCoroutine(DelayedSceneInitialization());
        }
    }
    
    private IEnumerator DelayedSceneInitialization()
    {
        yield return null;
        yield return null;
        UpdateAllResourceUI();
    }

    private void OnResourceAmountChanged(ResourceType type, int amount)
    {
        UpdateResourceUI(type);
    }

    private void UpdateResourceUI(ResourceType type)
    {
        GameObject resourcePanel = GetResourcePanel(type);
        if (resourcePanel == null) return;

        ResourceDataManager dataManager = ResourceDataManager.Instance;
        if (dataManager == null) return;
        
        int amount = dataManager.GetResourceAmount(type);
        
        TMP_Text resourceText = resourcePanel.GetComponentInChildren<TMP_Text>();
        if (resourceText != null) {
            resourceText.text = amount.ToString();
        }
        
        bool shouldShow = amount > 0;
        resourcePanel.SetActive(shouldShow);
    }

    private void UpdateAllResourceUI()
    {
        UpdateResourceUI(ResourceType.Ferrite);
        UpdateResourceUI(ResourceType.Aether);
        UpdateResourceUI(ResourceType.Biomass);
        UpdateResourceUI(ResourceType.CryoCrystal);
        UpdateResourceUI(ResourceType.AlloyPlate);
        UpdateResourceUI(ResourceType.CompositeFrame);
        UpdateResourceUI(ResourceType.EChip);
        UpdateResourceUI(ResourceType.BioCable);
        UpdateResourceUI(ResourceType.PowerCube);
        UpdateResourceUI(ResourceType.BioFuel);
        UpdateResourceUI(ResourceType.CryoGel);
        UpdateResourceUI(ResourceType.Solana);
        UpdateResourceUI(ResourceType.Core);
        UpdateResourceUI(ResourceType.Ammunition);
        UpdateResourceUI(ResourceType.HeavyPlating);
        UpdateResourceUI(ResourceType.Actuator);
        UpdateResourceUI(ResourceType.GenomeChip);
        UpdateResourceUI(ResourceType.PatchKit);
        UpdateResourceUI(ResourceType.SensorUnit);
        UpdateResourceUI(ResourceType.PlasmaCube);
        UpdateResourceUI(ResourceType.CryoConduit);
        UpdateResourceUI(ResourceType.SeekerMissile);
        UpdateResourceUI(ResourceType.NexusData);
        UpdateResourceUI(ResourceType.NeuralMatrix);
    }

    private GameObject GetResourcePanel(ResourceType type)
    {
        return type switch {
            ResourceType.Ferrite => ferritePanel,
            ResourceType.Aether => aetherPanel,
            ResourceType.Biomass => biomassPanel,
            ResourceType.CryoCrystal => cryoCrystalPanel,
            ResourceType.AlloyPlate => alloyPlatePanel,
            ResourceType.CompositeFrame => compositeFramePanel,
            ResourceType.EChip => eChipPanel,
            ResourceType.BioCable => bioCablePanel,
            ResourceType.PowerCube => powerCubePanel,
            ResourceType.BioFuel => bioFuelPanel,
            ResourceType.CryoGel => cryoGelPanel,
            ResourceType.Solana => solanaPanel,
            ResourceType.Core => corePanel,
            ResourceType.Ammunition => ammunitionPanel,
            ResourceType.HeavyPlating => heavyPlatingPanel,
            ResourceType.Actuator => actuatorPanel,
            ResourceType.GenomeChip => genomeChipPanel,
            ResourceType.PatchKit => patchKitPanel,
            ResourceType.SensorUnit => sensorUnitPanel,
            ResourceType.PlasmaCube => plasmaCubePanel,
            ResourceType.CryoConduit => cryoConduitPanel,
            ResourceType.SeekerMissile => seekerMissilePanel,
            ResourceType.NexusData => nexusDataPanel,
            ResourceType.NeuralMatrix => neuralMatrixPanel,
            _ => null
        };
    }
}

