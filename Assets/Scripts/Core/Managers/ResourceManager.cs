using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum ResourceType
{
    None = -1,
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
    NeuralMatrix,
    Electricity
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

    [Header("Electricity (power grid)")]
    [SerializeField] private int electricityInitialAmount;
    [SerializeField] private int tutorialElectricityInitialAmount;

    [Header("Cheat Resource Amounts (F1)")]
    [SerializeField] private int cheatFerriteAmount;
    [SerializeField] private int cheatAetherAmount;
    [SerializeField] private int cheatBiomassAmount;
    [SerializeField] private int cheatCryoCrystalAmount;
    [SerializeField] private int cheatAlloyPlateAmount;
    [SerializeField] private int cheatCompositeFrameAmount;
    [SerializeField] private int cheatEChipAmount;
    [SerializeField] private int cheatBioCableAmount;
    [SerializeField] private int cheatPowerCubeAmount;
    [SerializeField] private int cheatBioFuelAmount;
    [SerializeField] private int cheatCryoGelAmount;
    [SerializeField] private int cheatSolanaAmount;
    [SerializeField] private int cheatCoreAmount;
    [SerializeField] private int cheatAmmunitionAmount;
    [SerializeField] private int cheatHeavyPlatingAmount;
    [SerializeField] private int cheatActuatorAmount;
    [SerializeField] private int cheatGenomeChipAmount;
    [SerializeField] private int cheatPatchKitAmount;
    [SerializeField] private int cheatSensorUnitAmount;
    [SerializeField] private int cheatPlasmaCubeAmount;
    [SerializeField] private int cheatCryoConduitAmount;
    [SerializeField] private int cheatSeekerMissileAmount;
    [SerializeField] private int cheatNexusDataAmount;
    [SerializeField] private int cheatNeuralMatrixAmount;
    [SerializeField] private int cheatElectricityAmount;

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
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (IsLoadingScreenActive())
        {
            return;
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F1))
        {
            AddCheatResources();
        }
#endif
    }

    private void OnEnable()
    {
        // Forward events from ResourceDataManager for backward compatibility
        ResourceDataManager.OnNewStorageAdded += ForwardOnNewStorageAdded;
        ResourceDataManager.OnStorageRemoved += ForwardOnStorageRemoved;
        ResourceDataManager.OnStorageSpaceFreed += ForwardOnStorageSpaceFreed;
        ResourceDataManager.OnResourceAmountChanged += ForwardOnResourceAmountChanged;
        ResourceDataManager.OnResourceNodeAdded += ForwardOnResourceNodeAdded;
        ResourceDataManager.OnResourceNodeRemoved += ForwardOnResourceNodeRemoved;
    }

    private void OnDisable()
    {
        ResourceDataManager.OnNewStorageAdded -= ForwardOnNewStorageAdded;
        ResourceDataManager.OnStorageRemoved -= ForwardOnStorageRemoved;
        ResourceDataManager.OnStorageSpaceFreed -= ForwardOnStorageSpaceFreed;
        ResourceDataManager.OnResourceAmountChanged -= ForwardOnResourceAmountChanged;
        ResourceDataManager.OnResourceNodeAdded -= ForwardOnResourceNodeAdded;
        ResourceDataManager.OnResourceNodeRemoved -= ForwardOnResourceNodeRemoved;
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null)
        {
            return false;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        if (loadingScreen == null)
        {
            return false;
        }

        return loadingScreen.gameObject.activeSelf;
    }

    // Forward events from ResourceDataManager for backward compatibility
    public static event Action OnNewStorageAdded;
    public static event Action<IStorage> OnStorageRemoved;
    public static event Action<IStorage, int> OnStorageSpaceFreed;
    public static event Action<ResourceType, int> OnResourceAmountChanged;
    public static event Action<ResourceNode> OnResourceNodeAdded;
    public static event Action<ResourceNode> OnResourceNodeRemoved;

    private void ForwardOnNewStorageAdded() => OnNewStorageAdded?.Invoke();
    private void ForwardOnStorageRemoved(IStorage storage) => OnStorageRemoved?.Invoke(storage);
    private void ForwardOnStorageSpaceFreed(IStorage storage, int availableCapacity) => OnStorageSpaceFreed?.Invoke(storage, availableCapacity);
    private void ForwardOnResourceAmountChanged(ResourceType type, int amount) => OnResourceAmountChanged?.Invoke(type, amount);
    private void ForwardOnResourceNodeAdded(ResourceNode node) => OnResourceNodeAdded?.Invoke(node);
    private void ForwardOnResourceNodeRemoved(ResourceNode node) => OnResourceNodeRemoved?.Invoke(node);

    private void Initialize()
    {
        if (ResourceDataManager.Instance == null)
        {
            Debug.LogError("ResourceDataManager not found! Please ensure ResourceDataManager is in the scene.");
            return;
        }

        if (resourceStatsList != null && resourceStatsList.Count > 0)
        {
            ResourceDataManager.Instance.InitializeResourceStats(resourceStatsList);
        }
    }

    private int GetInitialAmount(ResourceType type)
    {
        return type switch
        {
            ResourceType.Ferrite => ferriteInitialAmount,
            ResourceType.Aether => aetherInitialAmount,
            ResourceType.Biomass => biomassInitialAmount,
            ResourceType.CryoCrystal => cryoCrystalInitialAmount,
            ResourceType.AlloyPlate => alloyPlateInitialAmount,
            ResourceType.CompositeFrame => compositeFrameInitialAmount,
            ResourceType.EChip => eChipInitialAmount,
            ResourceType.BioCable => bioCableInitialAmount,
            ResourceType.PowerCube => powerCubeInitialAmount,
            ResourceType.BioFuel => bioFuelInitialAmount,
            ResourceType.CryoGel => cryoGelInitialAmount,
            ResourceType.Solana => solanaInitialAmount,
            ResourceType.Core => coreInitialAmount,
            ResourceType.Ammunition => ammunitionInitialAmount,
            ResourceType.HeavyPlating => heavyPlatingInitialAmount,
            ResourceType.Actuator => actuatorInitialAmount,
            ResourceType.GenomeChip => genomeChipInitialAmount,
            ResourceType.PatchKit => patchKitInitialAmount,
            ResourceType.SensorUnit => sensorUnitInitialAmount,
            ResourceType.PlasmaCube => plasmaCubeInitialAmount,
            ResourceType.CryoConduit => cryoConduitInitialAmount,
            ResourceType.SeekerMissile => seekerMissileInitialAmount,
            ResourceType.NexusData => nexusDataInitialAmount,
            ResourceType.NeuralMatrix => neuralMatrixInitialAmount,
            _ => 0
        };
    }

    private void InitializeMainStructureStorage(MainStructure mainStructure)
    {
        if (mainStructure == null) return;

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (type == ResourceType.None || type == ResourceType.Electricity) continue;
            mainStructure.InitializeStorage(type, GetInitialAmount(type));
        }

        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.RecalculateResourceCountsFromStorages();
        }
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
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.RecalculateResourceCountsFromStorages();
        }
        mainStructure.UpdateStorageUI();
    }

    public void AddResource(ResourceType type, int amount)
    {
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.AddResource(type, amount);
        }
    }

    public void DistributeRefundedResource(ResourceType type, int amount, Vector3 sourcePosition)
    {
        if (amount <= 0) return;
        if (ResourceDataManager.Instance == null) return;

        int remaining = amount;
        List<IStorage> storages = GetAllStorages();
        if (storages == null || storages.Count == 0)
        {
            AddResource(type, amount);
            return;
        }

        List<IStorage> mainAndStoragePriority = new List<IStorage>();
        List<IStorage> otherPriority = new List<IStorage>();

        for (int i = 0; i < storages.Count; i++)
        {
            IStorage storage = storages[i];
            if (storage == null) continue;

            Component component = storage as Component;
            if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy) continue;

            if (storage is MainStructure || storage is Storage)
            {
                mainAndStoragePriority.Add(storage);
            }
            else
            {
                otherPriority.Add(storage);
            }
        }

        StoreResourceInPriority(type, sourcePosition, mainAndStoragePriority, ref remaining);
        StoreResourceInPriority(type, sourcePosition, otherPriority, ref remaining);

        if (remaining > 0)
        {
            MainStructure main = ResourceDataManager.Instance.GetMainStructure();
            if (main != null)
            {
                main.ForceAddResource(type, remaining);
            }
            else
            {
                bool forceAdded = false;
                foreach (IStorage storage in storages)
                {
                    if (storage is BaseStorage baseStorage && !(storage is Battery))
                    {
                        baseStorage.ForceAddResource(type, remaining);
                        forceAdded = true;
                        break;
                    }
                }
                if (!forceAdded)
                {
                    AddResource(type, remaining);
                }
            }
        }
    }

    public int AddGeneratedResource(ResourceType type, int amount, Vector3 sourcePosition)
    {
        if (amount <= 0) return 0;
        if (ResourceDataManager.Instance == null) return 0;

        if (type == ResourceType.Electricity)
        {
            return DistributeElectricityToBatteriesOnly(amount, sourcePosition);
        }

        if (type != ResourceType.Aether)
        {
            AddResource(type, amount);
            return amount;
        }

        return DistributeToBatteryThenMainStorage(type, amount, sourcePosition);
    }

    private int DistributeElectricityToBatteriesOnly(int amount, Vector3 sourcePosition)
    {
        int remaining = amount;
        List<IStorage> storages = GetAllStorages();
        if (storages == null || storages.Count == 0)
        {
            return 0;
        }

        List<IStorage> batteriesOnly = new List<IStorage>();
        for (int i = 0; i < storages.Count; i++)
        {
            IStorage storage = storages[i];
            if (storage == null) continue;
            Component component = storage as Component;
            if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy) continue;
            if (storage is Battery)
            {
                batteriesOnly.Add(storage);
            }
        }

        StoreResourceInPriority(ResourceType.Electricity, sourcePosition, batteriesOnly, ref remaining);
        return amount - remaining;
    }

    private int DistributeToBatteryThenMainStorage(ResourceType type, int amount, Vector3 sourcePosition)
    {
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

    public int TryWithdrawElectricityFromStoragesInOrder(int amount, List<IStorage> storagesOrdered)
    {
        if (amount <= 0 || storagesOrdered == null || storagesOrdered.Count == 0)
        {
            return 0;
        }

        int remaining = amount;
        int totalWithdrawn = 0;
        for (int i = 0; i < storagesOrdered.Count; i++)
        {
            if (remaining <= 0)
            {
                break;
            }

            IStorage storage = storagesOrdered[i];
            if (storage == null)
            {
                continue;
            }

            if (storage.TryWithdrawResource(ResourceType.Electricity, remaining, out int withdrawn) && withdrawn > 0)
            {
                totalWithdrawn += withdrawn;
                remaining -= withdrawn;
            }
        }

        return totalWithdrawn;
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
        return ResourceDataManager.Instance != null ? ResourceDataManager.Instance.GetResourceStats(type) : null;
    }

    public void AddStorage(IStorage storage)
    {
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.AddStorage(storage);
        }
    }

    public void RemoveStorage(IStorage storage)
    {
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.RemoveStorage(storage);
        }
    }

    public void ReserveStorageCapacity(IStorage storage, int amount)
    {
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.ReserveCapacity(storage, amount);
        }
    }

    public void ReleaseStorageCapacity(IStorage storage, int amount)
    {
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.ReleaseCapacity(storage, amount);
        }
    }

    public int GetAvailableStorageCapacity(IStorage storage)
    {
        return ResourceDataManager.Instance != null ? ResourceDataManager.Instance.GetAvailableCapacity(storage) : 0;
    }

    public void NotifyStorageSpaceFreed(IStorage storage, int availableCapacity)
    {
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.NotifyStorageSpaceFreed(storage, availableCapacity);
        }
    }

    public List<IStorage> GetAllStorages()
    {
        return ResourceDataManager.Instance != null ? ResourceDataManager.Instance.GetAllStorages() : new List<IStorage>();
    }

    public void RegisterMainStructure(MainStructure mainStructure)
    {
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.AddStorage(mainStructure);
            ResourceDataManager.Instance.RegisterMainStructure(mainStructure);
            if (!BaseCarryOverManager.HasCarriedOverData)
            {
                InitializeMainStructureStorage(mainStructure);
            }
        }
    }

    public void AddResourceNode(ResourceNode node)
    {
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.AddResourceNode(node);
        }
    }

    public void RemoveResourceNode(ResourceNode node)
    {
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.RemoveResourceNode(node);
        }
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
        string nameKey = $"resourceType.{type}.name";
        string localized = GameLocalization.Get("Resource", nameKey);
        if (localized != nameKey)
        {
            return localized;
        }

        string legacyKey = $"resourceType.{type}";
        localized = GameLocalization.Get("Resource", legacyKey);
        if (localized != legacyKey)
        {
            return localized;
        }

        int index = (int)type;
        if (resourceDisplayNames != null && index >= 0 && index < resourceDisplayNames.Count && !string.IsNullOrEmpty(resourceDisplayNames[index]))
        {
            return resourceDisplayNames[index];
        }
        return type.ToString();
    }

    public string GetResourceDescription(ResourceType type)
    {
        string descKey = $"resourceType.{type}.desc";
        string localized = GameLocalization.Get("Resource", descKey);
        if (localized != descKey)
        {
            return localized;
        }

        string legacyKey = $"resourceType.{type}.description";
        localized = GameLocalization.Get("Resource", legacyKey);
        if (localized != legacyKey)
        {
            return localized;
        }

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

        Debug.Log("<color=orange>CHEAT ACTIVATED:</color> Applied inspector-configured cheat resources.");

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            int cheatAmount = GetCheatAmount(type);
            if (cheatAmount == 0)
            {
                continue;
            }

            ResourceDataManager.Instance.AddResource(type, cheatAmount);
            MainStructure mainStructure = ResourceDataManager.Instance.GetMainStructure();
            if (mainStructure != null)
            {
                int current = mainStructure.GetCurrentResourceAmount(type);
                mainStructure.InitializeStorage(type, current + cheatAmount);
            }
        }
    }

    private int GetCheatAmount(ResourceType type)
    {
        return type switch
        {
            ResourceType.Ferrite => cheatFerriteAmount,
            ResourceType.Aether => cheatAetherAmount,
            ResourceType.Biomass => cheatBiomassAmount,
            ResourceType.CryoCrystal => cheatCryoCrystalAmount,
            ResourceType.AlloyPlate => cheatAlloyPlateAmount,
            ResourceType.CompositeFrame => cheatCompositeFrameAmount,
            ResourceType.EChip => cheatEChipAmount,
            ResourceType.BioCable => cheatBioCableAmount,
            ResourceType.PowerCube => cheatPowerCubeAmount,
            ResourceType.BioFuel => cheatBioFuelAmount,
            ResourceType.CryoGel => cheatCryoGelAmount,
            ResourceType.Solana => cheatSolanaAmount,
            ResourceType.Core => cheatCoreAmount,
            ResourceType.Ammunition => cheatAmmunitionAmount,
            ResourceType.HeavyPlating => cheatHeavyPlatingAmount,
            ResourceType.Actuator => cheatActuatorAmount,
            ResourceType.GenomeChip => cheatGenomeChipAmount,
            ResourceType.PatchKit => cheatPatchKitAmount,
            ResourceType.SensorUnit => cheatSensorUnitAmount,
            ResourceType.PlasmaCube => cheatPlasmaCubeAmount,
            ResourceType.CryoConduit => cheatCryoConduitAmount,
            ResourceType.SeekerMissile => cheatSeekerMissileAmount,
            ResourceType.NexusData => cheatNexusDataAmount,
            ResourceType.NeuralMatrix => cheatNeuralMatrixAmount,
            ResourceType.Electricity => cheatElectricityAmount,
            _ => 0
        };
    }

    public IStorage FindClosestStorageWithResource(Vector3 position, ResourceType type, int minAmount)
    {
        return ResourceDataManager.Instance != null ? ResourceDataManager.Instance.FindClosestStorageWithResource(position, type, minAmount) : null;
    }

    public IStorage GetBestDepositTarget(ResourceType type, Vector3 fromPosition, IStorage excludeStorage = null)
    {
        return ResourceDataManager.Instance != null ? ResourceDataManager.Instance.GetBestDepositTarget(type, fromPosition, excludeStorage) : null;
    }

    public bool TryClaimRedistributionTask(Vector3 fromPosition, out RedistributionTask task)
    {
        task = null;
        return ResourceDataManager.Instance != null && ResourceDataManager.Instance.TryClaimRedistributionTask(fromPosition, out task);
    }

    public void CompleteRedistributionTask(RedistributionTask task)
    {
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.CompleteRedistributionTask(task);
        }
    }

    public void CancelRedistributionTask(RedistributionTask task)
    {
        if (ResourceDataManager.Instance != null)
        {
            ResourceDataManager.Instance.CancelRedistributionTask(task);
        }
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
