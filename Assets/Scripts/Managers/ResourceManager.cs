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
    [SerializeField] private int solanaInitialAmount;

    [Header("1 Crafted Resource Start Values")]
    [SerializeField] private int alloyPlateInitialAmount;
    [SerializeField] private int compositeFrameInitialAmount;
    [SerializeField] private int eChipInitialAmount;
    [SerializeField] private int bioCableInitialAmount;
    [SerializeField] private int powerCubeInitialAmount;
    [SerializeField] private int bioFuelInitialAmount;
    [SerializeField] private int cryoGelInitialAmount;
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

    [Header("Resource UI")]
    [SerializeField] private TMP_Text ferriteNumber;
    [SerializeField] private TMP_Text aetherNumber;
    [SerializeField] private TMP_Text biomassNumber;
    [SerializeField] private TMP_Text cryoCrystalNumber;
    [SerializeField] private TMP_Text solanaNumber;

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

    private void ResetResourceCount()
    {
        _resourceCounts.Clear();

        // 기본 자원
        _resourceCounts[ResourceType.Ferrite] = ferriteInitialAmount;
        _resourceCounts[ResourceType.Aether] = aetherInitialAmount;
        _resourceCounts[ResourceType.Biomass] = biomassInitialAmount;
        _resourceCounts[ResourceType.CryoCrystal] = cryoCrystalInitialAmount;

        // 1차 자원
        _resourceCounts[ResourceType.AlloyPlate] = alloyPlateInitialAmount;
        _resourceCounts[ResourceType.CompositeFrame] = compositeFrameInitialAmount;
        _resourceCounts[ResourceType.EChip] = eChipInitialAmount;
        _resourceCounts[ResourceType.BioCable] = bioCableInitialAmount;
        _resourceCounts[ResourceType.PowerCube] = powerCubeInitialAmount;
        _resourceCounts[ResourceType.BioFuel] = bioFuelInitialAmount;
        _resourceCounts[ResourceType.CryoGel] = cryoGelInitialAmount;
        _resourceCounts[ResourceType.Solana] = solanaInitialAmount;
        _resourceCounts[ResourceType.Core] = coreInitialAmount;
        _resourceCounts[ResourceType.Ammunition] = ammunitionInitialAmount;

        // 2차 자원
        _resourceCounts[ResourceType.HeavyPlating] = heavyPlatingInitialAmount;
        _resourceCounts[ResourceType.Actuator] = actuatorInitialAmount;
        _resourceCounts[ResourceType.GenomeChip] = genomeChipInitialAmount;
        _resourceCounts[ResourceType.PatchKit] = patchKitInitialAmount;
        _resourceCounts[ResourceType.SensorUnit] = sensorUnitInitialAmount;
        _resourceCounts[ResourceType.PlasmaCube] = plasmaCubeInitialAmount;
        _resourceCounts[ResourceType.CryoConduit] = cryoConduitInitialAmount;
        _resourceCounts[ResourceType.SeekerMissile] = seekerMissileInitialAmount;
        _resourceCounts[ResourceType.NexusData] = nexusDataInitialAmount;
        _resourceCounts[ResourceType.NeuralMatrix] = neuralMatrixInitialAmount;
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
        if (_resourceCounts.ContainsKey(type)) {
            _resourceCounts[type] += amount;
        }
        else {
            _resourceCounts[type] = amount;
        }
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
            _resourceCounts[cost.resourceType] -= cost.amount;

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
        ferriteNumber = GameObject.Find("Resource0_txt")?.GetComponent<TMP_Text>();
        aetherNumber = GameObject.Find("Resource1_txt")?.GetComponent<TMP_Text>();
        biomassNumber = GameObject.Find("Resource2_txt")?.GetComponent<TMP_Text>();
        cryoCrystalNumber = GameObject.Find("Resource3_txt")?.GetComponent<TMP_Text>();
        solanaNumber = GameObject.Find("Resource4_txt")?.GetComponent<TMP_Text>();
    }

    private void UpdateResourceUI(ResourceType type)
    {
        if (!IsUIConnected()) return;

        if (type == ResourceType.Solana) {
            // UpdateSolanaUI();
        }
        else {
            TMP_Text resourceText = GetResourceText(type);
            if (resourceText != null) {
                resourceText.text = _resourceCounts[type].ToString();
            }
        }
    }

    public void UpdateAllResourceUI()
    {
        if (!IsUIConnected()) return;

        if (ferriteNumber != null) ferriteNumber.text = _resourceCounts[ResourceType.Ferrite].ToString();
        if (aetherNumber != null) aetherNumber.text = _resourceCounts[ResourceType.Aether].ToString();
        if (biomassNumber != null) biomassNumber.text = _resourceCounts[ResourceType.Biomass].ToString();
        if (cryoCrystalNumber != null) cryoCrystalNumber.text = _resourceCounts[ResourceType.CryoCrystal].ToString();

        // UpdateSolanaUI();
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
            _resourceCounts[type] = cheatAmount;

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
