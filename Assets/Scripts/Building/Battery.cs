using System;
using UnityEngine;
using System.Collections.Generic;

public class Battery : Damageable, IStorage
{
    public event Action<ResourceType, int, int> OnResourceChanged;

    [Header("Storage Settings")]
    [SerializeField] private int maxStorageAmount;

    private int _currentAetherAmount;

    private void Start()
    {
        currentHealth = maxHealth;
        
        OnResourceChanged?.Invoke(ResourceType.Aether, GetCurrentResourceAmount(ResourceType.Aether), GetMaxCapacity());
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
        if (type != ResourceType.Aether || _currentAetherAmount >= maxStorageAmount)
        {
            return false;
        }

        int canAddAmount = Mathf.Min(amount, maxStorageAmount - _currentAetherAmount);
        _currentAetherAmount += canAddAmount;
        
        OnResourceChanged?.Invoke(ResourceType.Aether, _currentAetherAmount, maxStorageAmount);
        ResourceManager.Instance.AddResource(ResourceType.Aether, canAddAmount); 
        
        return canAddAmount > 0;
    }
    
    public bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn)
    {
        if (type != ResourceType.Aether || _currentAetherAmount <= 0)
        {
            amountWithdrawn = 0;
            return false;
        }

        amountWithdrawn = Mathf.Min(_currentAetherAmount, amountToWithdraw);
        _currentAetherAmount -= amountWithdrawn;
        
        OnResourceChanged?.Invoke(ResourceType.Aether, _currentAetherAmount, maxStorageAmount);
        
        return true;
    }
    
    public bool HasEnoughResources(ResourceCost[] costs)
    {
        int totalAetherNeeded = 0;
        
        foreach (var cost in costs)
        {
            if (cost.resourceType != ResourceType.Aether)
            {
                return false;
            }
            totalAetherNeeded += cost.amount;
        }

        return _currentAetherAmount >= totalAetherNeeded;
    }
    
    public int GetCurrentResourceAmount(ResourceType type)
    {
        if (type == ResourceType.Aether)
        {
            return _currentAetherAmount;
        }
        return 0;
    }
    
    public int GetMaxCapacity()
    {
        return maxStorageAmount;
    }
    
    public int GetTotalCurrentAmount()
    {
        return _currentAetherAmount;
    }
    
    public Vector3 GetPosition()
    {
        return transform.position;
    }
}

