using System;
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
    [SerializeField] private TMP_Text ferriteNumber;
    [SerializeField] private TMP_Text aetherNumber;
    [SerializeField] private TMP_Text biomassNumber;
    [SerializeField] private TMP_Text cryoCrystalNumber;
    [SerializeField] private TMP_Text alloyPlateNumber;
    [SerializeField] private TMP_Text compositeFrameNumber;
    [SerializeField] private TMP_Text eChipNumber;
    [SerializeField] private TMP_Text bioCableNumber;
    [SerializeField] private TMP_Text powerCubeNumber;
    [SerializeField] private TMP_Text bioFuelNumber;
    [SerializeField] private TMP_Text cryoGelNumber;
    [SerializeField] private TMP_Text solanaNumber;
    [SerializeField] private TMP_Text coreNumber;
    [SerializeField] private TMP_Text ammunitionNumber;
    [SerializeField] private TMP_Text heavyPlatingNumber;
    [SerializeField] private TMP_Text actuatorNumber;
    [SerializeField] private TMP_Text genomeChipNumber;
    [SerializeField] private TMP_Text patchKitNumber;
    [SerializeField] private TMP_Text sensorUnitNumber;
    [SerializeField] private TMP_Text plasmaCubeNumber;
    [SerializeField] private TMP_Text cryoConduitNumber;
    [SerializeField] private TMP_Text seekerMissileNumber;
    [SerializeField] private TMP_Text nexusDataNumber;
    [SerializeField] private TMP_Text neuralMatrixNumber;

    private readonly List<ResourceNode> _allResources = new List<ResourceNode>();
    private readonly List<IStorage> _allStorages = new List<IStorage>();
    private readonly Dictionary<ResourceType, int> _resourceCounts = new Dictionary<ResourceType, int>();
    private readonly Dictionary<ResourceType, ResourceStats> _resourceStats = new Dictionary<ResourceType, ResourceStats>();
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
            FindAndConnectUI();
            ResetResourceCount();
            UpdateAllResourceUI();
        }
    }

    public void AddResource(ResourceType type, int amount)
    {
        int currentAmount = GetResourceAmount(type);
        SetResource(type, currentAmount + amount);
        UpdateResourceUI(type);
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
            int currentAmount = GetResourceAmount(cost.resourceType);
            SetResource(cost.resourceType, currentAmount - cost.amount);

            int remainingToWithdraw = cost.amount;
            foreach (IStorage storage in storages) {
                if (remainingToWithdraw <= 0) break;
                if (storage.TryWithdrawResource(cost.resourceType, remainingToWithdraw, out int amountWithdrawn)) {
                    remainingToWithdraw -= amountWithdrawn;
                }
            }

            UpdateResourceUI(cost.resourceType);
        }

        return true;
    }

    public bool SpendResources(ResourceType type, int amount)
    {
        return SpendResources(new[] { new ResourceCost { resourceType = type, amount = amount } });
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
        if (!_allResources.Contains(node)) {
            _allResources.Add(node);
        }
    }

    public void RemoveResourceNode(ResourceNode node)
    {
        _allResources.Remove(node);
    }

    public List<ResourceNode> GetAllResources()
    {
        return _allResources;
    }

    private void FindAndConnectUI()
    {
        if (ferriteNumber == null)
            ferriteNumber = GameObject.Find("Resource0_txt")?.GetComponent<TMP_Text>();

        if (aetherNumber == null)
            aetherNumber = GameObject.Find("Resource1_txt")?.GetComponent<TMP_Text>();

        if (biomassNumber == null)
            biomassNumber = GameObject.Find("Resource2_txt")?.GetComponent<TMP_Text>();

        if (cryoCrystalNumber == null)
            cryoCrystalNumber = GameObject.Find("Resource3_txt")?.GetComponent<TMP_Text>();

        // 1 Crafted Resource UI (Resource4_txt ~ Resource13_txt)
        if (alloyPlateNumber == null)
            alloyPlateNumber = GameObject.Find("Resource4_txt")?.GetComponent<TMP_Text>();

        if (compositeFrameNumber == null)
            compositeFrameNumber = GameObject.Find("Resource5_txt")?.GetComponent<TMP_Text>();

        if (eChipNumber == null)
            eChipNumber = GameObject.Find("Resource6_txt")?.GetComponent<TMP_Text>();

        if (bioCableNumber == null)
            bioCableNumber = GameObject.Find("Resource7_txt")?.GetComponent<TMP_Text>();

        if (powerCubeNumber == null)
            powerCubeNumber = GameObject.Find("Resource8_txt")?.GetComponent<TMP_Text>();

        if (bioFuelNumber == null)
            bioFuelNumber = GameObject.Find("Resource9_txt")?.GetComponent<TMP_Text>();

        if (cryoGelNumber == null)
            cryoGelNumber = GameObject.Find("Resource10_txt")?.GetComponent<TMP_Text>();

        if (solanaNumber == null)
            solanaNumber = GameObject.Find("Resource11_txt")?.GetComponent<TMP_Text>();

        if (coreNumber == null)
            coreNumber = GameObject.Find("Resource12_txt")?.GetComponent<TMP_Text>();

        if (ammunitionNumber == null)
            ammunitionNumber = GameObject.Find("Resource13_txt")?.GetComponent<TMP_Text>();

        // 2 Crafted Resource UI (Resource14_txt ~ Resource23_txt)
        if (heavyPlatingNumber == null)
            heavyPlatingNumber = GameObject.Find("Resource14_txt")?.GetComponent<TMP_Text>();

        if (actuatorNumber == null)
            actuatorNumber = GameObject.Find("Resource15_txt")?.GetComponent<TMP_Text>();

        if (genomeChipNumber == null)
            genomeChipNumber = GameObject.Find("Resource16_txt")?.GetComponent<TMP_Text>();

        if (patchKitNumber == null)
            patchKitNumber = GameObject.Find("Resource17_txt")?.GetComponent<TMP_Text>();

        if (sensorUnitNumber == null)
            sensorUnitNumber = GameObject.Find("Resource18_txt")?.GetComponent<TMP_Text>();

        if (plasmaCubeNumber == null)
            plasmaCubeNumber = GameObject.Find("Resource19_txt")?.GetComponent<TMP_Text>();

        if (cryoConduitNumber == null)
            cryoConduitNumber = GameObject.Find("Resource20_txt")?.GetComponent<TMP_Text>();

        if (seekerMissileNumber == null)
            seekerMissileNumber = GameObject.Find("Resource21_txt")?.GetComponent<TMP_Text>();

        if (nexusDataNumber == null)
            nexusDataNumber = GameObject.Find("Resource22_txt")?.GetComponent<TMP_Text>();

        if (neuralMatrixNumber == null)
            neuralMatrixNumber = GameObject.Find("Resource23_txt")?.GetComponent<TMP_Text>();
    }

    private void UpdateResourceUI(ResourceType type)
    {
        if (!IsUIConnected()) return;

        TMP_Text resourceText = GetResourceText(type);
        if (resourceText != null) {
            resourceText.text = _resourceCounts[type].ToString();
        }
    }

    public void UpdateAllResourceUI()
    {
        if (!IsUIConnected()) return;

        if (ferriteNumber != null) ferriteNumber.text = _resourceCounts[ResourceType.Ferrite].ToString();
        if (aetherNumber != null) aetherNumber.text = _resourceCounts[ResourceType.Aether].ToString();
        if (biomassNumber != null) biomassNumber.text = _resourceCounts[ResourceType.Biomass].ToString();
        if (cryoCrystalNumber != null) cryoCrystalNumber.text = _resourceCounts[ResourceType.CryoCrystal].ToString();
        if (alloyPlateNumber != null) alloyPlateNumber.text = _resourceCounts[ResourceType.AlloyPlate].ToString();
        if (compositeFrameNumber != null) compositeFrameNumber.text = _resourceCounts[ResourceType.CompositeFrame].ToString();
        if (eChipNumber != null) eChipNumber.text = _resourceCounts[ResourceType.EChip].ToString();
        if (bioCableNumber != null) bioCableNumber.text = _resourceCounts[ResourceType.BioCable].ToString();
        if (powerCubeNumber != null) powerCubeNumber.text = _resourceCounts[ResourceType.PowerCube].ToString();
        if (bioFuelNumber != null) bioFuelNumber.text = _resourceCounts[ResourceType.BioFuel].ToString();
        if (cryoGelNumber != null) cryoGelNumber.text = _resourceCounts[ResourceType.CryoGel].ToString();
        if (solanaNumber != null) solanaNumber.text = _resourceCounts[ResourceType.Solana].ToString();
        if (coreNumber != null) coreNumber.text = _resourceCounts[ResourceType.Core].ToString();
        if (ammunitionNumber != null) ammunitionNumber.text = _resourceCounts[ResourceType.Ammunition].ToString();
        if (heavyPlatingNumber != null) heavyPlatingNumber.text = _resourceCounts[ResourceType.HeavyPlating].ToString();
        if (actuatorNumber != null) actuatorNumber.text = _resourceCounts[ResourceType.Actuator].ToString();
        if (genomeChipNumber != null) genomeChipNumber.text = _resourceCounts[ResourceType.GenomeChip].ToString();
        if (patchKitNumber != null) patchKitNumber.text = _resourceCounts[ResourceType.PatchKit].ToString();
        if (sensorUnitNumber != null) sensorUnitNumber.text = _resourceCounts[ResourceType.SensorUnit].ToString();
        if (plasmaCubeNumber != null) plasmaCubeNumber.text = _resourceCounts[ResourceType.PlasmaCube].ToString();
        if (cryoConduitNumber != null) cryoConduitNumber.text = _resourceCounts[ResourceType.CryoConduit].ToString();
        if (seekerMissileNumber != null) seekerMissileNumber.text = _resourceCounts[ResourceType.SeekerMissile].ToString();
        if (nexusDataNumber != null) nexusDataNumber.text = _resourceCounts[ResourceType.NexusData].ToString();
        if (neuralMatrixNumber != null) neuralMatrixNumber.text = _resourceCounts[ResourceType.NeuralMatrix].ToString();
    }

    // private void UpdateSolanaUI()
    // {
    //     int requiredAmount = GameManager.Instance != null ? GameManager.Instance.GetRequiredAmountForCurrentQuota() : 0;
    //
    //     // solanaNumber.text = $"{_resourceCounts[ResourceType.Solana]} / {requiredAmount}";
    // }

    private bool IsUIConnected()
    {
        return ferriteNumber != null && aetherNumber != null && biomassNumber != null && cryoCrystalNumber != null;
    }

    private TMP_Text GetResourceText(ResourceType type)
    {
        return type switch {
            ResourceType.Ferrite => ferriteNumber,
            ResourceType.Aether => aetherNumber,
            ResourceType.Biomass => biomassNumber,
            ResourceType.CryoCrystal => cryoCrystalNumber,
            ResourceType.AlloyPlate => alloyPlateNumber,
            ResourceType.CompositeFrame => compositeFrameNumber,
            ResourceType.EChip => eChipNumber,
            ResourceType.BioCable => bioCableNumber,
            ResourceType.PowerCube => powerCubeNumber,
            ResourceType.BioFuel => bioFuelNumber,
            ResourceType.CryoGel => cryoGelNumber,
            ResourceType.Solana => solanaNumber,
            ResourceType.Core => coreNumber,
            ResourceType.Ammunition => ammunitionNumber,
            ResourceType.HeavyPlating => heavyPlatingNumber,
            ResourceType.Actuator => actuatorNumber,
            ResourceType.GenomeChip => genomeChipNumber,
            ResourceType.PatchKit => patchKitNumber,
            ResourceType.SensorUnit => sensorUnitNumber,
            ResourceType.PlasmaCube => plasmaCubeNumber,
            ResourceType.CryoConduit => cryoConduitNumber,
            ResourceType.SeekerMissile => seekerMissileNumber,
            ResourceType.NexusData => nexusDataNumber,
            ResourceType.NeuralMatrix => neuralMatrixNumber,
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
