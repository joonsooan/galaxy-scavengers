using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public class Storage : Damageable, IStorage
{
    public event Action<ResourceType, int, int> OnResourceChanged;

    [Header("Storage Settings")]
    [SerializeField] private int maxStorageAmount;
    
    private readonly Dictionary<ResourceType, int> _currentResources = new();

    protected override void Awake()
    {
        base.Awake();
        
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            _currentResources[type] = 0;
        }
    }

    private void Start()
    {
        currentHealth = maxHealth;
        
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            OnResourceChanged?.Invoke(type, GetCurrentResourceAmount(type), GetMaxCapacity());
        }
    }
    
    private void OnDestroy()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.RemoveStorage(this);
        }
    }

    public bool TryAddResource(ResourceType type, int amount)
    {
        int totalAmount = GetTotalCurrentAmount();
        if (totalAmount >= maxStorageAmount)
        {
            return false;
        }

        int canAddAmount = Mathf.Min(amount, maxStorageAmount - totalAmount);
        
        _currentResources[type] += canAddAmount;
        
        OnResourceChanged?.Invoke(type, _currentResources[type], maxStorageAmount);
        ResourceManager.Instance.AddResource(type, canAddAmount); 
        
        return canAddAmount > 0;
    }
    
    public bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn)
    {
        int availableAmount = GetCurrentResourceAmount(type);
        if (availableAmount <= 0)
        {
            amountWithdrawn = 0;
            return false;
        }

        amountWithdrawn = Mathf.Min(availableAmount, amountToWithdraw);
        _currentResources[type] -= amountWithdrawn;
        
        OnResourceChanged?.Invoke(type, _currentResources[type], maxStorageAmount);
        
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.RemoveResource(type, amountWithdrawn);
        }
        
        return true;
    }
    
    public bool HasEnoughResources(ResourceCost[] costs)
    {
        foreach (var cost in costs)
        {
            if (_currentResources[cost.resourceType] < cost.amount)
            {
                return false;
            }
        }
        return true;
    }
    
    public int GetCurrentResourceAmount(ResourceType type)
    {
        return _currentResources[type];
    }
    
    public int GetMaxCapacity()
    {
        return maxStorageAmount;
    }

    public int GetTotalCurrentAmount()
    {
        return _currentResources.Values.Sum();
    }
    
    public Vector3 GetPosition()
    {
        return transform.position;
    }
}