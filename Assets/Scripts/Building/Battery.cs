using System.Collections.Generic;
using UnityEngine;

public class Battery : BaseStorage, IPowerGridNode
{
    [Header("Power grid")]
    [SerializeField] private int supplyRangeN = 5;

    [Header("Gizmos")]
    [SerializeField] private bool showPowerCoverageGizmo = true;
    [SerializeField] private Color powerCoverageGizmoColor = new(0.2f, 0.85f, 1f, 1f);

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
            _electricityConsumptionManager.RegisterBattery(this);
        }
    }

    protected override void OnDisable()
    {
        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.UnregisterBattery(this);
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

    public override bool TryAddResource(ResourceType type, int amount)
    {
        if (type != ResourceType.Electricity) return false;
        return base.TryAddResource(type, amount);
    }

    public override bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn)
    {
        if (type != ResourceType.Electricity)
        {
            amountWithdrawn = 0;
            return false;
        }
        return base.TryWithdrawResource(type, amountToWithdraw, out amountWithdrawn);
    }

    public override bool HasEnoughResources(ResourceCost[] costs)
    {
        foreach (ResourceCost cost in costs)
        {
            if (cost.resourceType != ResourceType.Electricity) return false;
        }
        return base.HasEnoughResources(costs);
    }

    public BoundsInt GetPowerCoverageBounds()
    {
        BuildingManager bm = BuildingManager.Instance;
        if (bm == null || !bm.TryGetBuildingAnchorCells(transform, out _, out List<Vector3Int> occupied) ||
            occupied == null || occupied.Count == 0)
        {
            return default;
        }

        return PowerGridGeometry.ComputeSquareCoverageCenteredOnFootprint(occupied, supplyRangeN);
    }

    public bool IsActivePowerSource()
    {
        return GetCurrentResourceAmount(ResourceType.Electricity) > 0;
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
}
