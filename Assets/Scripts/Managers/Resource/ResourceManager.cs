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
    
    [Header("Tutorial Base Resource Start Values")]
    [SerializeField] private int tutorialFerriteInitialAmount;
    [SerializeField] private int tutorialAetherInitialAmount;
    [SerializeField] private int tutorialBiomassInitialAmount;
    [SerializeField] private int tutorialCryoCrystalInitialAmount;

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

    [Header("Resource Metadata")]
    [SerializeField] private List<string> resourceDisplayNames;
    [TextArea(3, 10)]
    [SerializeField] private List<string> resourceDescriptions;

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
        if (IsLoadingScreenActive()) {
            return;
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F1)) {
            AddCheatResources();
        }
#endif
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Forward events from ResourceDataManager for backward compatibility
        ResourceDataManager.OnNewStorageAdded += ForwardOnNewStorageAdded;
        ResourceDataManager.OnStorageRemoved += ForwardOnStorageRemoved;
        ResourceDataManager.OnResourceAmountChanged += ForwardOnResourceAmountChanged;
        ResourceDataManager.OnResourceNodeAdded += ForwardOnResourceNodeAdded;
        ResourceDataManager.OnResourceNodeRemoved += ForwardOnResourceNodeRemoved;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        ResourceDataManager.OnNewStorageAdded -= ForwardOnNewStorageAdded;
        ResourceDataManager.OnStorageRemoved -= ForwardOnStorageRemoved;
        ResourceDataManager.OnResourceAmountChanged -= ForwardOnResourceAmountChanged;
        ResourceDataManager.OnResourceNodeAdded -= ForwardOnResourceNodeAdded;
        ResourceDataManager.OnResourceNodeRemoved -= ForwardOnResourceNodeRemoved;
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null) {
            return false;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        if (loadingScreen == null) {
            return false;
        }

        return loadingScreen.gameObject.activeSelf;
    }

    // Forward events from ResourceDataManager for backward compatibility
    public static event Action OnNewStorageAdded;
    public static event Action<IStorage> OnStorageRemoved;
    public static event Action<ResourceType, int> OnResourceAmountChanged;
    public static event Action<ResourceNode> OnResourceNodeAdded;
    public static event Action<ResourceNode> OnResourceNodeRemoved;

    private void ForwardOnNewStorageAdded() => OnNewStorageAdded?.Invoke();
    private void ForwardOnStorageRemoved(IStorage storage) => OnStorageRemoved?.Invoke(storage);
    private void ForwardOnResourceAmountChanged(ResourceType type, int amount) => OnResourceAmountChanged?.Invoke(type, amount);
    private void ForwardOnResourceNodeAdded(ResourceNode node) => OnResourceNodeAdded?.Invoke(node);
    private void ForwardOnResourceNodeRemoved(ResourceNode node) => OnResourceNodeRemoved?.Invoke(node);

    private void Initialize()
    {
        if (ResourceDataManager.Instance == null) {
            Debug.LogError("ResourceDataManager not found! Please ensure ResourceDataManager is in the scene.");
            return;
        }

        // Initialize resource stats in ResourceDataManager
        if (resourceStatsList != null && resourceStatsList.Count > 0) {
            ResourceDataManager.Instance.InitializeResourceStats(resourceStatsList);
        }
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
        
        // Wait for ResourceDataManager to initialize
        while (ResourceDataManager.Instance == null) {
            yield return null;
        }
        
        // Initialize main structure if registered
        MainStructure mainStructure = ResourceDataManager.Instance.GetMainStructure();
        if (mainStructure != null) {
            InitializeMainStructureStorage(mainStructure);
        }
    }

    private void InitializeMainStructureStorage(MainStructure mainStructure)
    {
        if (mainStructure == null) return;

        // 기본 자원
        mainStructure.InitializeStorage(ResourceType.Ferrite, ferriteInitialAmount);
        mainStructure.InitializeStorage(ResourceType.Aether, aetherInitialAmount);
        mainStructure.InitializeStorage(ResourceType.Biomass, biomassInitialAmount);
        mainStructure.InitializeStorage(ResourceType.CryoCrystal, cryoCrystalInitialAmount);

        // 1차 자원
        mainStructure.InitializeStorage(ResourceType.AlloyPlate, alloyPlateInitialAmount);
        mainStructure.InitializeStorage(ResourceType.CompositeFrame, compositeFrameInitialAmount);
        mainStructure.InitializeStorage(ResourceType.EChip, eChipInitialAmount);
        mainStructure.InitializeStorage(ResourceType.BioCable, bioCableInitialAmount);
        mainStructure.InitializeStorage(ResourceType.PowerCube, powerCubeInitialAmount);
        mainStructure.InitializeStorage(ResourceType.BioFuel, bioFuelInitialAmount);
        mainStructure.InitializeStorage(ResourceType.CryoGel, cryoGelInitialAmount);
        mainStructure.InitializeStorage(ResourceType.Solana, solanaInitialAmount);
        mainStructure.InitializeStorage(ResourceType.Core, coreInitialAmount);
        mainStructure.InitializeStorage(ResourceType.Ammunition, ammunitionInitialAmount);

        // 2차 자원
        mainStructure.InitializeStorage(ResourceType.HeavyPlating, heavyPlatingInitialAmount);
        mainStructure.InitializeStorage(ResourceType.Actuator, actuatorInitialAmount);
        mainStructure.InitializeStorage(ResourceType.GenomeChip, genomeChipInitialAmount);
        mainStructure.InitializeStorage(ResourceType.PatchKit, patchKitInitialAmount);
        mainStructure.InitializeStorage(ResourceType.SensorUnit, sensorUnitInitialAmount);
        mainStructure.InitializeStorage(ResourceType.PlasmaCube, plasmaCubeInitialAmount);
        mainStructure.InitializeStorage(ResourceType.CryoConduit, cryoConduitInitialAmount);
        mainStructure.InitializeStorage(ResourceType.SeekerMissile, seekerMissileInitialAmount);
        mainStructure.InitializeStorage(ResourceType.NexusData, nexusDataInitialAmount);
        mainStructure.InitializeStorage(ResourceType.NeuralMatrix, neuralMatrixInitialAmount);

        mainStructure.UpdateStorageUI();
    }

    public void ApplyTutorialStartResources()
    {
        if (ResourceDataManager.Instance == null) return;
        MainStructure mainStructure = ResourceDataManager.Instance.GetMainStructure();
        if (mainStructure == null) return;

        // Tutorial start should ignore default base start values.
        mainStructure.InitializeStorage(ResourceType.Ferrite, tutorialFerriteInitialAmount);
        mainStructure.InitializeStorage(ResourceType.Aether, tutorialAetherInitialAmount);
        mainStructure.InitializeStorage(ResourceType.Biomass, tutorialBiomassInitialAmount);
        mainStructure.InitializeStorage(ResourceType.CryoCrystal, tutorialCryoCrystalInitialAmount);
        mainStructure.UpdateStorageUI();
    }

    // Delegate all data operations to ResourceDataManager
    public void AddResource(ResourceType type, int amount)
    {
        ResourceDataManager.Instance?.AddResource(type, amount);
    }

    public int AddGeneratedResource(ResourceType type, int amount, Vector3 sourcePosition)
    {
        if (amount <= 0) return 0;
        if (ResourceDataManager.Instance == null) return 0;

        if (type != ResourceType.Aether)
        {
            AddResource(type, amount);
            return amount;
        }

        int remaining = amount;
        List<IStorage> storages = GetAllStorages();
        if (storages == null || storages.Count == 0)
        {
            return 0;
        }

        List<IStorage> batteryPriority = new List<IStorage>();
        List<IStorage> storageAndMainPriority = new List<IStorage>();

        for (int i = 0; i < storages.Count; i++)
        {
            IStorage storage = storages[i];
            if (storage == null) continue;

            Component component = storage as Component;
            if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy) continue;

            if (storage is Battery)
            {
                batteryPriority.Add(storage);
            }
            else if (storage is Storage || storage is MainStructure)
            {
                storageAndMainPriority.Add(storage);
            }
        }

        StoreResourceInPriority(type, sourcePosition, batteryPriority, ref remaining);
        StoreResourceInPriority(type, sourcePosition, storageAndMainPriority, ref remaining);

        return amount - remaining;
    }

    public bool RemoveResource(ResourceType type, int amount)
    {
        return ResourceDataManager.Instance != null && ResourceDataManager.Instance.RemoveResource(type, amount);
    }

    public bool SpendResources(ResourceCost[] costs)
    {
        return ResourceDataManager.Instance != null && ResourceDataManager.Instance.SpendResources(costs);
    }

    public bool HasEnoughResources(ResourceCost[] costs)
    {
        return ResourceDataManager.Instance != null && ResourceDataManager.Instance.HasEnoughResources(costs);
    }

    public int GetResourceAmount(ResourceType type)
    {
        return ResourceDataManager.Instance != null ? ResourceDataManager.Instance.GetResourceAmount(type) : 0;
    }

    public ResourceStats GetResourceStats(ResourceType type)
    {
        return ResourceDataManager.Instance?.GetResourceStats(type);
    }

    public void AddStorage(IStorage storage)
    {
        ResourceDataManager.Instance?.AddStorage(storage);
    }

    public void RemoveStorage(IStorage storage)
    {
        ResourceDataManager.Instance?.RemoveStorage(storage);
    }

    public List<IStorage> GetAllStorages()
    {
        return ResourceDataManager.Instance != null ? ResourceDataManager.Instance.GetAllStorages() : new List<IStorage>();
    }

    public void RegisterMainStructure(MainStructure mainStructure)
    {
        if (ResourceDataManager.Instance != null) {
            ResourceDataManager.Instance.RegisterMainStructure(mainStructure);
            InitializeMainStructureStorage(mainStructure);
        }
    }

    public void AddResourceNode(ResourceNode node)
    {
        ResourceDataManager.Instance?.AddResourceNode(node);
    }

    public void RemoveResourceNode(ResourceNode node)
    {
        ResourceDataManager.Instance?.RemoveResourceNode(node);
    }

    public List<ResourceNode> GetAllResources()
    {
        return ResourceDataManager.Instance != null ? ResourceDataManager.Instance.GetAllResources() : new List<ResourceNode>();
    }

    public Sprite GetResourceIcon(ResourceType type)
    {
        int index = (int)type;
        return index >= 0 && index < resourceIcons.Count ? resourceIcons[index] : null;
    }

    public string GetResourceDisplayName(ResourceType type)
    {
        int index = (int)type;
        if (resourceDisplayNames != null && index >= 0 && index < resourceDisplayNames.Count && !string.IsNullOrEmpty(resourceDisplayNames[index]))
        {
            return resourceDisplayNames[index];
        }
        return type.ToString();
    }

    public string GetResourceDescription(ResourceType type)
    {
        int index = (int)type;
        if (resourceDescriptions != null && index >= 0 && index < resourceDescriptions.Count)
        {
            return resourceDescriptions[index] ?? string.Empty;
        }
        return string.Empty;
    }

    private void AddCheatResources()
    {
        if (ResourceDataManager.Instance == null) return;

        const int cheatAmount = 999999;
        Debug.Log($"<color=orange>CHEAT ACTIVATED:</color> All resources set to {cheatAmount}.");

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) {
            ResourceDataManager.Instance.AddResource(type, cheatAmount);
            MainStructure mainStructure = ResourceDataManager.Instance.GetMainStructure();
            mainStructure?.InitializeStorage(type, cheatAmount);
        }
    }

    public IStorage FindClosestStorageWithResource(Vector3 position, ResourceType type, int minAmount)
    {
        return ResourceDataManager.Instance?.FindClosestStorageWithResource(position, type, minAmount);
    }

    private static void StoreResourceInPriority(ResourceType type, Vector3 sourcePosition, List<IStorage> targets, ref int remaining)
    {
        if (targets == null || targets.Count == 0 || remaining <= 0) return;

        while (remaining > 0 && targets.Count > 0)
        {
            int nearestIndex = -1;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < targets.Count; i++)
            {
                IStorage storage = targets[i];
                if (storage == null) continue;
                float distance = Vector3.Distance(sourcePosition, storage.GetPosition());
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex < 0) break;

            IStorage selected = targets[nearestIndex];
            targets.RemoveAt(nearestIndex);
            if (selected == null) continue;

            int before = selected.GetCurrentResourceAmount(type);
            bool added = selected.TryAddResource(type, remaining);
            if (!added) continue;

            int after = selected.GetCurrentResourceAmount(type);
            int consumed = Mathf.Max(0, after - before);
            if (consumed <= 0) continue;

            remaining -= consumed;
        }
    }
}
