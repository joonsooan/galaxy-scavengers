using UnityEngine;

public class Battery : BaseStorage
{
    [Header("Aether Capacity")]
    [SerializeField] private int aetherCapacity = 100;
    
    private AetherConsumptionManager _aetherConsumptionManager;
    
    public int AetherCapacity => aetherCapacity;
    
    protected override void OnEnable()
    {
        base.OnEnable();
        
        if (!BuildingManager.IsBuildingProperlyPlaced(transform))
        {
            return;
        }
        
        FindAndCacheAetherManager();
        if (_aetherConsumptionManager != null)
        {
            _aetherConsumptionManager.RegisterBattery(this);
        }
    }
    
    protected override void OnDisable()
    {
        if (_aetherConsumptionManager != null)
        {
            _aetherConsumptionManager.UnregisterBattery(this);
        }
        
        base.OnDisable();
    }
    
    private void FindAndCacheAetherManager()
    {
        if (_aetherConsumptionManager == null)
        {
            _aetherConsumptionManager = FindFirstObjectByType<AetherConsumptionManager>();
        }
    }
    
    public override bool TryAddResource(ResourceType type, int amount)
    {
        if (type != ResourceType.Aether) return false;
        return base.TryAddResource(type, amount);
    }

    public override bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn)
    {
        if (type != ResourceType.Aether)
        {
            amountWithdrawn = 0;
            return false;
        }
        return base.TryWithdrawResource(type, amountToWithdraw, out amountWithdrawn);
    }

    public override bool HasEnoughResources(ResourceCost[] costs)
    {
        foreach (var cost in costs)
        {
            if (cost.resourceType != ResourceType.Aether) return false;
        }
        return base.HasEnoughResources(costs);
    }
}