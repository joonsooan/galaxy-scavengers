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
}