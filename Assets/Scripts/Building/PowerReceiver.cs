using System.Collections.Generic;
using UnityEngine;

/// <summary>Full-building power pole: use <see cref="BuildingData"/> with <see cref="BuildingType.PowerReceiver"/>, not a building piece.</summary>
public class PowerReceiver : Damageable, IPowerGridNode
{
    [Header("Power grid")]
    [SerializeField] private int supplyRangeN = 5;

    [Header("Gizmos")]
    [SerializeField] private bool showPowerCoverageGizmo = true;
    [SerializeField] private Color powerCoverageGizmoColor = new(0.95f, 0.35f, 1f, 1f);

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
            _electricityConsumptionManager = ElectricityConsumptionManager.Instance;
        }
    }

    /// <summary>Called from <see cref="BuildingManager"/> when a full building is finished (same pattern as <see cref="ResourceGenerator.SetConstructed"/>).</summary>
    public void SetConstructed()
    {
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
        return false;
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
