using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ResourceType
{
    Ferrite,
    Aether,
    Biomass,
    CryoCrystal,
    AlloyPlate,
    CompositeFrame,
    EChip,
    BioCable,
    PowerCube,
    BioFuel,
    CryoGel,
    Solana,
    Core,
    Ammunition,
    HeavyPlating,
    Actuator,
    GenomeChip,
    PatchKit,
    SensorUnit,
    PlasmaCube,
    CryoConduit,
    SeekerMissile,
    NexusData,
    NeuralMatrix
}

[Serializable]
public class ResourceStats
{
    public ResourceType resourceType;
    public int amountToMine = 100;
    public float timeToMinePerUnit = 0.1f;
}

public class ResourceManager : MonoBehaviour
{
    [Header("Base Resource Start Values")]
    [SerializeField] private int ferriteInitialAmount;
    [SerializeField] private int aetherInitialAmount;
    [SerializeField] private int biomassInitialAmount;
    [SerializeField] private int cryoCrystalInitialAmount;

    [Header("1 Crafted Resource Start Values")]
    [SerializeField] private int alloyPlateInitialAmount;
    [SerializeField] private int compositeFrameInitialAmount;
    [SerializeField] private int eChipInitialAmount;
    [SerializeField] private int bioCableInitialAmount;
    [SerializeField] private int powerCubeInitialAmount;
    [SerializeField] private int bioFuelInitialAmount;
    [SerializeField] private int cryoGelInitialAmount;
    [SerializeField] private int solanaInitialAmount;
    [SerializeField] private int coreInitialAmount;
    [SerializeField] private int ammunitionInitialAmount;

    [Header("2 Crafted Resource Start Values")]
    [SerializeField] private int heavyPlatingInitialAmount;
    [SerializeField] private int actuatorInitialAmount;
    [SerializeField] private int genomeChipInitialAmount;
    [SerializeField] private int patchKitInitialAmount;
    [SerializeField] private int sensorUnitInitialAmount;
    [SerializeField] private int plasmaCubeInitialAmount;
    [SerializeField] private int cryoConduitInitialAmount;
    [SerializeField] private int seekerMissileInitialAmount;
    [SerializeField] private int nexusDataInitialAmount;
    [SerializeField] private int neuralMatrixInitialAmount;

    [Header("Resource Icons")]
    [SerializeField] private List<Sprite> resourceIcons;

    [Header("Resource Stats")]
    [SerializeField] private List<ResourceStats> resourceStatsList;

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

    private readonly HashSet<ResourceNode> _allResources = new ();
    private readonly List<IStorage> _allStorages = new ();
    private readonly Dictionary<ResourceType, int> _resourceCounts = new ();
    private readonly Dictionary<ResourceType, ResourceStats> _resourceStats = new ();
    private MainStructure _mainStructure;
    public static ResourceManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.C)) {
            AddCheatResources();
        }
#endif
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public static event Action OnNewStorageAdded;
    public static event Action<IStorage> OnStorageRemoved;
    public static event Action<ResourceType, int> OnResourceAmountChanged;
    public static event Action<ResourceNode> OnResourceNodeAdded;
    public static event Action<ResourceNode> OnResourceNodeRemoved;

    private void Initialize()
    {
        InitializeResourceStats();
        ResetResourceCount();
    }

    private void InitializeResourceStats()
    {
        _resourceStats.Clear();
        foreach (ResourceStats stats in resourceStatsList) {
            _resourceStats[stats.resourceType] = stats;
        }
    }
    
    private void SetResource(ResourceType type, int amount)
    {
        _resourceCounts[type] = amount;
        OnResourceAmountChanged?.Invoke(type, amount);
    }

    private void ResetResourceCount()
    {
        _resourceCounts.Clear();

        // 기본 자원
        SetResource(ResourceType.Ferrite, ferriteInitialAmount);
        SetResource(ResourceType.Aether, aetherInitialAmount);
        SetResource(ResourceType.Biomass, biomassInitialAmount);
        SetResource(ResourceType.CryoCrystal, cryoCrystalInitialAmount);

        // 1차 자원
        SetResource(ResourceType.AlloyPlate, alloyPlateInitialAmount);
        SetResource(ResourceType.CompositeFrame, compositeFrameInitialAmount);
        SetResource(ResourceType.EChip, eChipInitialAmount);
        SetResource(ResourceType.BioCable, bioCableInitialAmount);
        SetResource(ResourceType.PowerCube, powerCubeInitialAmount);
        SetResource(ResourceType.BioFuel, bioFuelInitialAmount);
        SetResource(ResourceType.CryoGel, cryoGelInitialAmount);
        SetResource(ResourceType.Solana, solanaInitialAmount);
        SetResource(ResourceType.Core, coreInitialAmount);
        SetResource(ResourceType.Ammunition, ammunitionInitialAmount);

        // 2차 자원
        SetResource(ResourceType.HeavyPlating, heavyPlatingInitialAmount);
        SetResource(ResourceType.Actuator, actuatorInitialAmount);
        SetResource(ResourceType.GenomeChip, genomeChipInitialAmount);
        SetResource(ResourceType.PatchKit, patchKitInitialAmount);
        SetResource(ResourceType.SensorUnit, sensorUnitInitialAmount);
        SetResource(ResourceType.PlasmaCube, plasmaCubeInitialAmount);
        SetResource(ResourceType.CryoConduit, cryoConduitInitialAmount);
        SetResource(ResourceType.SeekerMissile, seekerMissileInitialAmount);
        SetResource(ResourceType.NexusData, nexusDataInitialAmount);
        SetResource(ResourceType.NeuralMatrix, neuralMatrixInitialAmount);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene") {
            _mainStructure = null;
            _allStorages.Clear();
            _allResources.Clear();
            
            StartCoroutine(DelayedSceneInitialization());
        }
    }
    
    private IEnumerator DelayedSceneInitialization()
    {
        yield return null;
        
        FindAndConnectUI();
        ResetResourceCount();
        
        yield return null;
        
        RecalculateResourceCountsFromStorages();
        UpdateAllResourceUI();
    }
    
    private void RecalculateResourceCountsFromStorages()
    {
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            int totalAmount = 0;
            foreach (IStorage storage in _allStorages)
            {
                totalAmount += storage.GetCurrentResourceAmount(type);
            }
            SetResource(type, totalAmount);
        }
    }

    public void AddResource(ResourceType type, int amount)
    {
        int currentAmount = GetResourceAmount(type);
        SetResource(type, currentAmount + amount);
        UpdateResourceUI(type);
    }

    public bool RemoveResource(ResourceType type, int amount)
    {
        int currentAmount = GetResourceAmount(type);
        if (currentAmount < amount)
        {
            return false;
        }
        SetResource(type, currentAmount - amount);
        UpdateResourceUI(type);
        return true;
    }

    public bool SpendResources(ResourceCost[] costs)
    {
        if (!HasEnoughResources(costs)) {
            Debug.Log("Not Enough Resources");
            return false;
        }

        Dictionary<ResourceType, int> requiredResources = new Dictionary<ResourceType, int>();
        foreach (ResourceCost cost in costs) {
            if (requiredResources.ContainsKey(cost.resourceType)) {
                requiredResources[cost.resourceType] += cost.amount;
            }
            else {
                requiredResources.Add(cost.resourceType, cost.amount);
            }
        }

        Dictionary<ResourceType, int> availableInStorages = new Dictionary<ResourceType, int>();
        List<IStorage> storages = GetAllStorages();
        foreach (ResourceType type in requiredResources.Keys) {
            int totalInStorages = 0;
            foreach (IStorage storage in storages) {
                totalInStorages += storage.GetCurrentResourceAmount(type);
            }
            availableInStorages[type] = totalInStorages;
        }

        foreach (KeyValuePair<ResourceType, int> req in requiredResources) {
            if (GetResourceAmount(req.Key) < req.Value) {
                Debug.Log($"Not Enough Resources for {req.Key} after final check.");
                return false;
            }
        }

        foreach (ResourceCost cost in costs) {
            int remainingToWithdraw = cost.amount;
            foreach (IStorage storage in storages) {
                if (remainingToWithdraw <= 0) break;
                if (storage.TryWithdrawResource(cost.resourceType, remainingToWithdraw, out int amountWithdrawn)) {
                    remainingToWithdraw -= amountWithdrawn;
                }
            }
            
            UpdateResourceUI(cost.resourceType);
        }
        
        RecalculateResourceCountsFromStorages();

        return true;
    }

    public bool HasEnoughResources(ResourceCost[] costs)
    {
        foreach (ResourceCost cost in costs) {
            if (_resourceCounts.GetValueOrDefault(cost.resourceType) < cost.amount) {
                return false;
            }
        }
        return true;
    }

    public int GetResourceAmount(ResourceType type)
    {
        _resourceCounts.TryGetValue(type, out int value);
        return value;
    }

    public ResourceStats GetResourceStats(ResourceType type)
    {
        _resourceStats.TryGetValue(type, out ResourceStats stats);
        return stats;
    }

    public void AddStorage(IStorage storage)
    {
        if (!_allStorages.Contains(storage)) {
            _allStorages.Add(storage);
            OnNewStorageAdded?.Invoke();
        }
    }

    public void RemoveStorage(IStorage storage)
    {
        if (_allStorages.Remove(storage)) {
            OnStorageRemoved?.Invoke(storage);
        }
    }

    public List<IStorage> GetAllStorages()
    {
        return _allStorages;
    }

    public void RegisterMainStructure(MainStructure mainStructure)
    {
        _mainStructure = mainStructure;
        InitializeMainStructureStorage();
    }

    private void InitializeMainStructureStorage()
    {
        if (_mainStructure == null) return;

        // 기본 자원
        _mainStructure.InitializeStorage(ResourceType.Ferrite, ferriteInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.Aether, aetherInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.Biomass, biomassInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.CryoCrystal, cryoCrystalInitialAmount);

        // 1차 자원
        _mainStructure.InitializeStorage(ResourceType.AlloyPlate, alloyPlateInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.CompositeFrame, compositeFrameInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.EChip, eChipInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.BioCable, bioCableInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.PowerCube, powerCubeInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.BioFuel, bioFuelInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.CryoGel, cryoGelInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.Solana, solanaInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.Core, coreInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.Ammunition, ammunitionInitialAmount);

        // 2차 자원
        _mainStructure.InitializeStorage(ResourceType.HeavyPlating, heavyPlatingInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.Actuator, actuatorInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.GenomeChip, genomeChipInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.PatchKit, patchKitInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.SensorUnit, sensorUnitInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.PlasmaCube, plasmaCubeInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.CryoConduit, cryoConduitInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.SeekerMissile, seekerMissileInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.NexusData, nexusDataInitialAmount);
        _mainStructure.InitializeStorage(ResourceType.NeuralMatrix, neuralMatrixInitialAmount);

        _mainStructure.UpdateStorageUI();
    }

    public void AddResourceNode(ResourceNode node)
    {
        if (_allResources.Add(node))
        {
            OnResourceNodeAdded?.Invoke(node);
        }
    }

    public void RemoveResourceNode(ResourceNode node)
    {
        if (_allResources.Remove(node))
        {
            OnResourceNodeRemoved?.Invoke(node);
        }
    }

    public List<ResourceNode> GetAllResources()
    {
        return new List<ResourceNode>(_allResources);
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

    private void UpdateResourceUI(ResourceType type)
    {
        if (!IsUIConnected()) return;

        GameObject resourcePanel = GetResourcePanel(type);
        if (resourcePanel != null) {
            int amount = _resourceCounts.GetValueOrDefault(type, 0);
            
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

    public Sprite GetResourceIcon(ResourceType type)
    {
        int index = (int)type;
        return index >= 0 && index < resourceIcons.Count ? resourceIcons[index] : null;
    }

    private void AddCheatResources()
    {
        const int cheatAmount = 999999;
        Debug.Log($"<color=orange>CHEAT ACTIVATED:</color> All resources set to {cheatAmount}.");

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) {
            SetResource(type, cheatAmount);
            _mainStructure?.InitializeStorage(type, cheatAmount);
        }

        UpdateAllResourceUI();
    }

    public IStorage FindClosestStorageWithResource(Vector3 position, ResourceType type, int minAmount)
    {
        IStorage closestStorage = null;
        float minDistance = float.MaxValue;

        foreach (IStorage storage in _allStorages) {
            if (storage.GetCurrentResourceAmount(type) >= minAmount) {
                float dist = Vector3.Distance(position, storage.GetPosition());
                if (dist < minDistance) {
                    minDistance = dist;
                    closestStorage = storage;
                }
            }
        }
        return closestStorage;
    }
}
