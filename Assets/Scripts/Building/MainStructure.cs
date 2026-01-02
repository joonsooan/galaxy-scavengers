using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainStructure : BaseStorage, IClickable
{
    [Header("Production Settings")]
    [SerializeField] private List<UnitData> producibleUnits;
    
    private bool _isProducing;
    private InventorySystem _inventorySystem;

    protected override void Start()
    {
        base.Start();
        _inventorySystem = GetComponent<InventorySystem>();
        
        if (GetComponent<BuildingHoverTrigger>() == null)
        {
            gameObject.AddComponent<BuildingHoverTrigger>();
        }
        
        BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
        bool hasTriggerCollider = false;
        foreach (BoxCollider2D collider in colliders)
        {
            if (collider != null && collider.isTrigger)
            {
                hasTriggerCollider = true;
                break;
            }
        }
    }
    
    public void OnClicked()
    {
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.ShowMainStructureUI();
        }
    }
    
    public void AddResourceToStorageOnly(ResourceType type, int amount)
    {
        int totalAmount = GetTotalCurrentAmount();
        if (totalAmount >= maxStorageAmount) return;

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