using System.Collections.Generic;
using UnityEngine;

public class Platform : Damageable
{
    private readonly List<Vector3Int> _occupiedCells = new List<Vector3Int>();

    public IReadOnlyList<Vector3Int> OccupiedCells => _occupiedCells;

    protected virtual void Start()
    {
        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
        {
            if (BuildingManager.Instance.TryGetBuildingAnchorCells(transform, out _, out List<Vector3Int> occupied) &&
                occupied != null && occupied.Count > 0)
            {
                _occupiedCells.AddRange(occupied);
            }
            else
            {
                Vector3Int cell = BuildingManager.Instance.grid.WorldToCell(transform.position);
                _occupiedCells.Add(cell);
            }
        }

        if (IsProperlyPlacedBuilding())
        {
            if (GetComponent<BuildingHoverTrigger>() == null)
            {
                gameObject.AddComponent<BuildingHoverTrigger>();
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        PlatformRegistry.Register(this);
    }

    protected override void OnDisable()
    {
        PlatformRegistry.Unregister(this);
        base.OnDisable();
    }

    private bool IsProperlyPlacedBuilding()
    {
        if (BuildingManager.Instance == null) return true;
        return BuildingManager.IsBuildingProperlyPlaced(transform);
    }

    public bool IsConnectedToMainStructure()
    {
        return PlatformRegistry.IsConnectedToMainStructure(this);
    }
}
