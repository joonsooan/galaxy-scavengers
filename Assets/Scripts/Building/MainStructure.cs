using System.Collections.Generic;
using UnityEngine;

public class MainStructure : BaseStorage, IClickable
{
    [Header("Production Settings")]
    [SerializeField] private List<UnitData> producibleUnits;
    [Header("Aether Capacity")]
    [SerializeField] private int baseAetherCapacity = 100;
    
    private bool _isProducing;
    private AetherConsumptionManager _aetherConsumptionManager;
    
    public int BaseAetherCapacity => baseAetherCapacity;

    protected override void Start()
    {
        base.Start();
        
        // Don't register ghost/preview buildings
        if (IsProperlyPlacedBuilding())
        {
            FindAndCacheAetherManager();
            if (_aetherConsumptionManager != null)
            {
                _aetherConsumptionManager.RegisterMainStructure(this);
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
        if (_aetherConsumptionManager != null)
        {
            _aetherConsumptionManager.UnregisterMainStructure(this);
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
        base.OnDestroy();
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }
    }
}