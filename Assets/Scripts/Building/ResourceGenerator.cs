using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;

public class ResourceGenerator : Damageable, IPowerGridNode
{
    [Header("Values")]
    [SerializeField] private float generationInterval = 5f;
    [SerializeField] private int resourceAmount = 1;

    [Header("Power grid")]
    [SerializeField] private int supplyRangeN = 5;
    [SerializeField] private int electricityBufferMax = 20;
    [SerializeField] private int electricityBufferCurrent;
    [SerializeField] private ResourceCost[] fuelCostsPerProduction = { new ResourceCost { resourceType = ResourceType.Ferrite, amount = 1 } };

    [Header("VFX")]
    [SerializeField] private Vector3 resourceImageOffset = new(0f, 0.5f, 0f);

    private Coroutine _productionCoroutine;
    private bool _isConstructed;
    private ElectricityConsumptionManager _electricityConsumptionManager;
    private WaitForSeconds _generationIntervalWait;

    public float GenerationInterval => generationInterval;
    public int ResourceAmount => resourceAmount;
    public bool IsConstructed => _isConstructed;
    public int SupplyRangeN => supplyRangeN;
    public int ElectricityBufferMax => electricityBufferMax;
    public int ElectricityBufferCurrent => electricityBufferCurrent;

    protected override void Awake()
    {
        base.Awake();
        _generationIntervalWait = CoroutineCache.GetWaitForSeconds(generationInterval);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!BuildingManager.IsBuildingProperlyPlaced(transform))
        {
            return;
        }

        FindAndCacheElectricityManager();

        if (_electricityConsumptionManager != null && _isConstructed)
        {
            _electricityConsumptionManager.RegisterResourceGenerator(this);
        }

        ActivateComboCard();
    }

    protected override void OnDisable()
    {
        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.UnregisterResourceGenerator(this);
        }

        base.OnDisable();
    }

    private void FindAndCacheElectricityManager()
    {
        if (_electricityConsumptionManager == null)
        {
            _electricityConsumptionManager = FindFirstObjectByType<ElectricityConsumptionManager>();
        }
    }

    private void ActivateComboCard()
    {
        if (!_isConstructed)
        {
            return;
        }

        if (_productionCoroutine != null)
        {
            StopCoroutine(_productionCoroutine);
        }
        _productionCoroutine = StartCoroutine(ProduceResource());
    }

    public void SetConstructed()
    {
        _isConstructed = true;

        if (!BuildingManager.IsBuildingProperlyPlaced(transform))
        {
            ActivateComboCard();
            return;
        }

        FindAndCacheElectricityManager();
        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.RegisterResourceGenerator(this);
        }

        ActivateComboCard();
    }

    private IEnumerator ProduceResource()
    {
        while (true)
        {
            yield return _generationIntervalWait;
            GenerateResource();
        }
    }

    private void GenerateResource()
    {
        if (ResourceManager.Instance == null)
        {
            return;
        }

        FindAndCacheElectricityManager();
        if (_electricityConsumptionManager != null && _electricityConsumptionManager.IsElectricityStorageFull)
        {
            return;
        }

        if (!TryConsumeFuelFromStoragesInRange())
        {
            return;
        }

        int toAdd = resourceAmount;
        int roomInBuffer = Mathf.Max(0, electricityBufferMax - electricityBufferCurrent);
        int toBuffer = Mathf.Min(toAdd, roomInBuffer);
        electricityBufferCurrent += toBuffer;
        int remainder = toAdd - toBuffer;

        if (remainder > 0)
        {
            int added = ResourceManager.Instance.AddGeneratedResource(ResourceType.Electricity, remainder, transform.position);
            if (added < remainder && _electricityConsumptionManager != null && _electricityConsumptionManager.IsElectricityStorageFull)
            {
                electricityBufferCurrent = Mathf.Min(electricityBufferMax, electricityBufferCurrent + (remainder - added));
            }
        }

        ShowResourceImage();
    }

    private bool TryConsumeFuelFromStoragesInRange()
    {
        if (fuelCostsPerProduction == null || fuelCostsPerProduction.Length == 0)
        {
            return true;
        }

        List<Storage> storages = CollectStoragesInSupplyRange();
        if (storages.Count == 0)
        {
            return false;
        }

        foreach (ResourceCost cost in fuelCostsPerProduction)
        {
            if (cost.amount <= 0) continue;
            int sum = 0;
            foreach (Storage storage in storages)
            {
                if (storage != null)
                {
                    sum += storage.GetCurrentResourceAmount(cost.resourceType);
                }
            }

            if (sum < cost.amount)
            {
                return false;
            }
        }

        foreach (ResourceCost cost in fuelCostsPerProduction)
        {
            if (cost.amount <= 0) continue;
            int need = cost.amount;
            foreach (Storage storage in storages)
            {
                if (storage == null || need <= 0) continue;
                if (storage.TryWithdrawResource(cost.resourceType, need, out int withdrawn))
                {
                    need -= withdrawn;
                }
            }
        }

        return true;
    }

    public bool HasFuelAvailableInRange()
    {
        if (fuelCostsPerProduction == null || fuelCostsPerProduction.Length == 0)
        {
            return true;
        }

        List<Storage> storages = CollectStoragesInSupplyRange();
        if (storages.Count == 0)
        {
            return false;
        }

        foreach (ResourceCost cost in fuelCostsPerProduction)
        {
            if (cost.amount <= 0) continue;
            int sum = 0;
            foreach (Storage storage in storages)
            {
                if (storage != null)
                {
                    sum += storage.GetCurrentResourceAmount(cost.resourceType);
                }
            }

            if (sum < cost.amount)
            {
                return false;
            }
        }

        return true;
    }

    private List<Storage> CollectStoragesInSupplyRange()
    {
        HashSet<Storage> set = new HashSet<Storage>();
        BuildingManager bm = BuildingManager.Instance;
        if (bm == null || !bm.TryGetBuildingAnchorCells(transform, out Vector3Int anchor, out _))
        {
            return new List<Storage>();
        }

        BoundsInt coverage = PowerGridGeometry.ComputeSquareCoverage(anchor, supplyRangeN);
        foreach (Vector3Int cell in coverage.allPositionsWithin)
        {
            BuildingPiece piece = bm.GetPieceAt(cell);
            if (piece == null) continue;
            Storage storage = piece.GetComponentInParent<Storage>();
            if (storage != null && BuildingManager.IsBuildingProperlyPlaced(storage.transform))
            {
                set.Add(storage);
            }
        }

        return set.ToList();
    }

    public BoundsInt GetPowerCoverageBounds()
    {
        BuildingManager bm = BuildingManager.Instance;
        if (bm == null || !bm.TryGetBuildingAnchorCells(transform, out Vector3Int anchor, out _))
        {
            return default;
        }

        return PowerGridGeometry.ComputeSquareCoverage(anchor, supplyRangeN);
    }

    public bool IsActivePowerSource()
    {
        if (electricityBufferCurrent > 0)
        {
            return true;
        }

        return HasFuelAvailableInRange() &&
            (_electricityConsumptionManager == null || !_electricityConsumptionManager.IsElectricityStorageFull);
    }

    public void SpillElectricityBufferToNetwork()
    {
        if (electricityBufferCurrent <= 0 || ResourceManager.Instance == null)
        {
            return;
        }

        int spill = electricityBufferCurrent;
        int added = ResourceManager.Instance.AddGeneratedResource(ResourceType.Electricity, spill, transform.position);
        electricityBufferCurrent -= added;
    }

    private void ShowResourceImage()
    {
        GameObject imageObj = ObjectPooler.Instance.SpawnFromPool(
            "ResourceImage", transform.position + resourceImageOffset, Quaternion.identity);

        if (imageObj != null)
        {
            FloatingResourceImage floatingImage = imageObj.GetComponent<FloatingResourceImage>();
            if (floatingImage != null)
            {
                floatingImage.Play(ResourceType.Electricity);
            }
        }
    }
}
