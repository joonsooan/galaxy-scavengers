using System.Collections.Generic;
using UnityEngine;

public class MainStructure : BaseStorage, IClickable
{
    [Header("Production Settings")]
    [SerializeField] private List<UnitData> producibleUnits;
    [Header("Aether Capacity")]
    [SerializeField] private int baseAetherCapacity = 100;
    
    private bool _isProducing;
    private ElectricityConsumptionManager _electricityConsumptionManager;
    
    public int BaseAetherCapacity => baseAetherCapacity;

    protected override void Start()
    {
        base.Start();
        
        // Don't register ghost/preview buildings
        if (IsProperlyPlacedBuilding())
        {
            FindAndCacheElectricityManager();
            if (_electricityConsumptionManager != null)
            {
                _electricityConsumptionManager.RegisterMainStructure(this);
            }

            if (TargetManager.Instance != null)
            {
                TargetManager.Instance.RegisterTarget(this);
            }
        }
        
        if (GetComponent<BuildingHoverTrigger>() == null)
        {
            gameObject.AddComponent<BuildingHoverTrigger>();
        }
    }
    
    private bool IsProperlyPlacedBuilding()
    {
        if (BuildingManager.Instance == null) return true;
        return BuildingManager.IsBuildingProperlyPlaced(transform);
    }
    
    protected override void OnDisable()
    {
        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.UnregisterMainStructure(this);
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
    
    public void OnClicked()
    {
    }
    
    public void AddResourceToStorageOnly(ResourceType type, int amount)
    {
        int totalAmount = GetTotalCurrentAmount();
        bool wasFull = totalAmount >= maxStorageAmount;
        
        if (wasFull) 
        {
            return;
        }

        int canAddAmount = Mathf.Min(amount, maxStorageAmount - totalAmount);
        currentResources[type] += canAddAmount;
        
        InvokeResourceChanged(type);
    }
    
    public void InitializeStorage(ResourceType type, int amount)
    {
        if (currentResources.ContainsKey(type))
        {
            currentResources[type] = Mathf.Min(amount, maxStorageAmount);
        }
    }
    
    public void UpdateStorageUI()
    {
        RefreshAllResourcesUI();
    }
    
    protected override void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver(transform);
        }

        base.OnDestroy();
    }
}