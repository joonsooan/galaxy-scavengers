using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;

public enum ResourceGeneratorFuelRequirementMode
{
    AllListedCosts,
    AnySingleListedCost,
}

public class ResourceGenerator : Damageable, IPowerGridNode
{
    [Header("Values")]
    [SerializeField] private float generationInterval = 5f;
    [SerializeField] private int resourceAmount = 1;

    [Header("Power grid")]
    [Tooltip("NxN cells centered on building footprint (world-space centroid).")]
    [SerializeField] [Range(1, 50)] private int supplyRangeN = 5;
    [Tooltip("Shifts power grid coverage center on the tile grid (X/Y cells). Does not affect fuel pickup range.")]
    [SerializeField] private Vector2Int powerCoverageCellOffset;
    [SerializeField] private int electricityBufferMax = 20;
    [SerializeField] private int electricityBufferCurrent;
    [SerializeField] private ResourceCost[] fuelCostsPerProduction = { new ResourceCost { resourceType = ResourceType.Ferrite, amount = 1 } };
    [SerializeField] private ResourceGeneratorFuelRequirementMode fuelRequirementMode = ResourceGeneratorFuelRequirementMode.AllListedCosts;

    [Header("Gizmos")]
    [SerializeField] private bool showPowerCoverageGizmo = true;
    [SerializeField] private Color powerCoverageGizmoColor = new(1f, 0.92f, 0.2f, 1f);

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
    public Vector2Int PowerCoverageCellOffset => powerCoverageCellOffset;
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
            _electricityConsumptionManager = ElectricityConsumptionManager.Instance;
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

        if (!HasPositiveFuelCostEntry())
        {
            return true;
        }

        List<IStorage> fuelTargets = CollectFuelStoragesInSupplyRange();
        if (fuelTargets.Count == 0)
        {
            return false;
        }

        if (fuelRequirementMode == ResourceGeneratorFuelRequirementMode.AnySingleListedCost)
        {
            foreach (ResourceCost cost in fuelCostsPerProduction)
            {
                if (cost.amount <= 0) continue;
                int sum = GetTotalAmountInFuelTargets(fuelTargets, cost.resourceType);
                if (sum < cost.amount) continue;
                int need = cost.amount;
                foreach (IStorage storage in fuelTargets)
                {
                    if (storage == null || need <= 0) continue;
                    if (storage.TryWithdrawResource(cost.resourceType, need, out int withdrawn))
                    {
                        need -= withdrawn;
                    }
                }
                if (need <= 0)
                {
                    return true;
                }
            }

            return false;
        }

        foreach (ResourceCost cost in fuelCostsPerProduction)
        {
            if (cost.amount <= 0) continue;
            int sum = GetTotalAmountInFuelTargets(fuelTargets, cost.resourceType);

            if (sum < cost.amount)
            {
                return false;
            }
        }

        foreach (ResourceCost cost in fuelCostsPerProduction)
        {
            if (cost.amount <= 0) continue;
            int need = cost.amount;
            foreach (IStorage storage in fuelTargets)
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

    private static int GetTotalAmountInFuelTargets(List<IStorage> targets, ResourceType resourceType)
    {
        int sum = 0;
        foreach (IStorage storage in targets)
        {
            if (storage != null)
            {
                sum += storage.GetCurrentResourceAmount(resourceType);
            }
        }
        return sum;
    }

    private bool HasPositiveFuelCostEntry()
    {
        foreach (ResourceCost c in fuelCostsPerProduction)
        {
            if (c.amount > 0)
            {
                return true;
            }
        }
        return false;
    }

    public bool HasFuelAvailableInRange()
    {
        if (fuelCostsPerProduction == null || fuelCostsPerProduction.Length == 0)
        {
            return true;
        }

        if (!HasPositiveFuelCostEntry())
        {
            return true;
        }

        List<IStorage> fuelTargets = CollectFuelStoragesInSupplyRange();
        if (fuelTargets.Count == 0)
        {
            return false;
        }

        if (fuelRequirementMode == ResourceGeneratorFuelRequirementMode.AnySingleListedCost)
        {
            foreach (ResourceCost cost in fuelCostsPerProduction)
            {
                if (cost.amount <= 0) continue;
                if (GetTotalAmountInFuelTargets(fuelTargets, cost.resourceType) >= cost.amount)
                {
                    return true;
                }
            }
            return false;
        }

        foreach (ResourceCost cost in fuelCostsPerProduction)
        {
            if (cost.amount <= 0) continue;
            if (GetTotalAmountInFuelTargets(fuelTargets, cost.resourceType) < cost.amount)
            {
                return false;
            }
        }

        return true;
    }

    private List<IStorage> CollectFuelStoragesInSupplyRange()
    {
        HashSet<IStorage> set = new HashSet<IStorage>();
        BuildingManager bm = BuildingManager.Instance;
        if (bm == null || !bm.TryGetBuildingAnchorCells(transform, out _, out List<Vector3Int> occupied) ||
            occupied == null || occupied.Count == 0)
        {
            return new List<IStorage>();
        }

        BoundsInt coverage = PowerGridGeometry.ComputeSquareCoverageCenteredOnFootprint(bm.grid, occupied, supplyRangeN);
        foreach (Vector3Int cell in coverage.allPositionsWithin)
        {
            BuildingPiece piece = bm.GetPieceAt(cell);
            if (piece != null) {
                MainStructure mainStructure = piece.GetComponentInParent<MainStructure>();
                if (mainStructure != null && BuildingManager.IsBuildingProperlyPlaced(mainStructure.transform))
                {
                    set.Add(mainStructure);
                }
                Storage storage = piece.GetComponentInParent<Storage>();
                if (storage != null && BuildingManager.IsBuildingProperlyPlaced(storage.transform))
                {
                    set.Add(storage);
                }
            }
            else if (bm.TryGetMainStructureAtCell(cell, out MainStructure mainAtCell)) {
                set.Add(mainAtCell);
            }
        }

        return set.ToList();
    }

    public BoundsInt GetPowerCoverageBounds()
    {
        BuildingManager bm = BuildingManager.Instance;
        if (bm == null || !bm.TryGetBuildingAnchorCells(transform, out _, out List<Vector3Int> occupied) ||
            occupied == null || occupied.Count == 0)
        {
            return default;
        }

        return PowerGridGeometry.ComputeSquareCoverageCenteredOnFootprint(bm.grid, occupied, supplyRangeN, powerCoverageCellOffset);
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
        if (!this)
        {
            return;
        }

        if (electricityBufferCurrent <= 0 || ResourceManager.Instance == null)
        {
            return;
        }

        int spill = electricityBufferCurrent;
        int added = ResourceManager.Instance.AddGeneratedResource(ResourceType.Electricity, spill, transform.position);
        electricityBufferCurrent -= added;
    }

    public int TryConsumeBufferForPowerDelivery(int amountRequested)
    {
        if (amountRequested <= 0)
        {
            return 0;
        }
        int take = Mathf.Min(amountRequested, electricityBufferCurrent);
        electricityBufferCurrent -= take;
        return take;
    }

    private void OnDrawGizmos()
    {
        if (!showPowerCoverageGizmo) {
            return;
        }

        Grid grid = BuildingManager.Instance != null ? BuildingManager.Instance.grid : null;
        if (grid == null) {
            return;
        }

        BoundsInt b = GetPowerCoverageBounds();
        if (b.size.x <= 0 || b.size.y <= 0) {
            return;
        }

        PowerGridGeometry.DrawCoverageOutline(b, grid, powerCoverageGizmoColor);
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
