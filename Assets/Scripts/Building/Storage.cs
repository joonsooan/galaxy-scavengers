using UnityEngine;

public class Storage : BaseStorage
{
    [Header("Aether Capacity")]
    [SerializeField] private int aetherCapacity = 50;
    
    private ElectricityConsumptionManager _electricityConsumptionManager;
    
    public int AetherCapacity => aetherCapacity;
    
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
    
    protected override void OnEnable()
    {
        base.OnEnable();
        
        if (!IsProperlyPlacedBuilding())
        {
            return;
        }
        
        FindAndCacheElectricityManager();
        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.RegisterStorage(this);
        }
    }
    
    protected override void OnDisable()
    {
        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.UnregisterStorage(this);
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
    
    private bool IsProperlyPlacedBuilding()
    {
        return BuildingManager.IsBuildingProperlyPlaced(transform);
    }
}