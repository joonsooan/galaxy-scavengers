using UnityEngine;

public class Storage : BaseStorage
{
    [Header("Aether Capacity")]
    [SerializeField] private int aetherCapacity = 50;
    
    private AetherConsumptionManager _aetherConsumptionManager;
    
    public int AetherCapacity => aetherCapacity;
    
    protected override void OnEnable()
    {
        base.OnEnable();
        
        // Don't register ghost/preview buildings
        if (!IsProperlyPlacedBuilding())
        {
            return;
        }
        
        FindAndCacheAetherManager();
        if (_aetherConsumptionManager != null)
        {
            _aetherConsumptionManager.RegisterStorage(this);
        }
    }
    
    protected override void OnDisable()
    {
        if (_aetherConsumptionManager != null)
        {
            _aetherConsumptionManager.UnregisterStorage(this);
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
    
    private bool IsProperlyPlacedBuilding()
    {
        return BuildingManager.IsBuildingProperlyPlaced(transform);
    }
}