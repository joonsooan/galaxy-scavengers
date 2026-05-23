using UnityEngine;

public class Storage : BaseStorage
{
    public override bool TryAddResource(ResourceType type, int amount)
    {
        if (type == ResourceType.Electricity) {
            return false;
        }
        return base.TryAddResource(type, amount);
    }

    public override void ForceAddResource(ResourceType type, int amount)
    {
        if (type == ResourceType.Electricity) return;
        base.ForceAddResource(type, amount);
    }

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
