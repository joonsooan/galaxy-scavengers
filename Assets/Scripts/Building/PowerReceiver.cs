using UnityEngine;

public class PowerReceiver : Damageable, IPowerGridNode
{
    [SerializeField] private int supplyRangeN = 5;

    private ElectricityConsumptionManager _electricityConsumptionManager;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!BuildingManager.IsBuildingProperlyPlaced(transform))
        {
            return;
        }

        FindAndCacheElectricityManager();
        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.RegisterPowerReceiver(this);
        }
    }

    protected override void OnDisable()
    {
        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.UnregisterPowerReceiver(this);
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
        return false;
    }
}
