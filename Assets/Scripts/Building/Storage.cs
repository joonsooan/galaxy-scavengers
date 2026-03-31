using UnityEngine;

public class Storage : BaseStorage
{
    protected override void Start()
    {
        base.Start();

        if (IsProperlyPlacedBuilding())
        {
            if (GetComponent<BuildingHoverTrigger>() == null)
            {
                gameObject.AddComponent<BuildingHoverTrigger>();
            }
        }
    }

    private bool IsProperlyPlacedBuilding()
    {
        return BuildingManager.IsBuildingProperlyPlaced(transform);
    }
}
