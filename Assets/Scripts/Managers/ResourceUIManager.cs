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
        
        FindAndConnectUI();
        
        yield return null;
        
        UpdateAllResourceUI();
    }

    private void FindAndConnectUI()
    {
        if (ferritePanel == null)
            ferritePanel = GameObject.Find("Resource0_Panel") ?? GameObject.Find("FerritePanel");

        if (aetherPanel == null)
            aetherPanel = GameObject.Find("Resource1_Panel") ?? GameObject.Find("AetherPanel");

        if (biomassPanel == null)
            biomassPanel = GameObject.Find("Resource2_Panel") ?? GameObject.Find("BiomassPanel");

        if (cryoCrystalPanel == null)
            cryoCrystalPanel = GameObject.Find("Resource3_Panel") ?? GameObject.Find("CryoCrystalPanel");

        if (alloyPlatePanel == null)
            alloyPlatePanel = GameObject.Find("Resource4_Panel") ?? GameObject.Find("AlloyPlatePanel");

        if (compositeFramePanel == null)
            compositeFramePanel = GameObject.Find("Resource5_Panel") ?? GameObject.Find("CompositeFramePanel");

        if (eChipPanel == null)
            eChipPanel = GameObject.Find("Resource6_Panel") ?? GameObject.Find("EChipPanel");

        if (bioCablePanel == null)
            bioCablePanel = GameObject.Find("Resource7_Panel") ?? GameObject.Find("BioCablePanel");

        if (powerCubePanel == null)
            powerCubePanel = GameObject.Find("Resource8_Panel") ?? GameObject.Find("PowerCubePanel");

        if (bioFuelPanel == null)
            bioFuelPanel = GameObject.Find("Resource9_Panel") ?? GameObject.Find("BioFuelPanel");

        if (cryoGelPanel == null)
            cryoGelPanel = GameObject.Find("Resource10_Panel") ?? GameObject.Find("CryoGelPanel");

        if (solanaPanel == null)
            solanaPanel = GameObject.Find("Resource11_Panel") ?? GameObject.Find("SolanaPanel");

        if (corePanel == null)
            corePanel = GameObject.Find("Resource12_Panel") ?? GameObject.Find("CorePanel");

        if (ammunitionPanel == null)
            ammunitionPanel = GameObject.Find("Resource13_Panel") ?? GameObject.Find("AmmunitionPanel");

        if (heavyPlatingPanel == null)
            heavyPlatingPanel = GameObject.Find("Resource14_Panel") ?? GameObject.Find("HeavyPlatingPanel");

        if (actuatorPanel == null)
            actuatorPanel = GameObject.Find("Resource15_Panel") ?? GameObject.Find("ActuatorPanel");

        if (genomeChipPanel == null)
            genomeChipPanel = GameObject.Find("Resource16_Panel") ?? GameObject.Find("GenomeChipPanel");

        if (patchKitPanel == null)
            patchKitPanel = GameObject.Find("Resource17_Panel") ?? GameObject.Find("PatchKitPanel");

        if (sensorUnitPanel == null)
            sensorUnitPanel = GameObject.Find("Resource18_Panel") ?? GameObject.Find("SensorUnitPanel");

        if (plasmaCubePanel == null)
            plasmaCubePanel = GameObject.Find("Resource19_Panel") ?? GameObject.Find("PlasmaCubePanel");

        if (cryoConduitPanel == null)
            cryoConduitPanel = GameObject.Find("Resource20_Panel") ?? GameObject.Find("CryoConduitPanel");

        if (seekerMissilePanel == null)
            seekerMissilePanel = GameObject.Find("Resource21_Panel") ?? GameObject.Find("SeekerMissilePanel");

        if (nexusDataPanel == null)
            nexusDataPanel = GameObject.Find("Resource22_Panel") ?? GameObject.Find("NexusDataPanel");

        if (neuralMatrixPanel == null)
            neuralMatrixPanel = GameObject.Find("Resource23_Panel") ?? GameObject.Find("NeuralMatrixPanel");
    }

    private void OnResourceAmountChanged(ResourceType type, int amount)
    {
        UpdateResourceUI(type);
    }

    private void UpdateResourceUI(ResourceType type)
    {
        if (!IsUIConnected()) return;

        GameObject resourcePanel = GetResourcePanel(type);
        if (resourcePanel != null) {
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
    }

    private void UpdateAllResourceUI()
    {
        if (!IsUIConnected()) return;

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

    private bool IsUIConnected()
    {
        return ferritePanel != null && aetherPanel != null && biomassPanel != null && cryoCrystalPanel != null;
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

