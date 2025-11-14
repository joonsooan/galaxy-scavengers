using System;
using UnityEngine;
using System.Collections.Generic;

public class Battery : Damageable, IStorage
{
    public event Action<ResourceType, int, int> OnResourceChanged;

    [Header("Storage Settings")]
    [SerializeField] private int maxStorageAmount;
    
    private int _currentAetherAmount = 0;

    [Header("UI Settings")]
    [SerializeField] private GameObject storageSliderPrefab;
    [SerializeField] private string canvasName = "ObjectUI_Canvas";
    [SerializeField] private Vector3 sliderOffset = new Vector3(0, 1.0f, 0);
    private GameObject _sliderInstance;

    private void Start()
    {
        currentHealth = maxHealth;

        if (storageSliderPrefab != null)
        {
            Canvas canvas = GameObject.Find(canvasName)?.GetComponent<Canvas>();
            
            if (canvas != null)
            {
                _sliderInstance = Instantiate(storageSliderPrefab, canvas.transform);
                var controller = _sliderInstance.GetComponent<StorageSlider>();
                controller?.Initialize(this, sliderOffset);
            }
        }
        
        OnResourceChanged?.Invoke(ResourceType.Aether, GetCurrentResourceAmount(ResourceType.Aether), GetMaxCapacity());
    }

    private void OnDestroy()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.RemoveStorage(this);
        }

        if (_sliderInstance != null)
        {
            Destroy(_sliderInstance);
        }
    }

    public bool TryAddResource(ResourceType type, int amount)
    {
        // Battery only stores Aether
        if (type != ResourceType.Aether)
        {
            return false;
        }

        if (_currentAetherAmount >= maxStorageAmount)
        {
            return false;
        }

        int canAddAmount = Mathf.Min(amount, maxStorageAmount - _currentAetherAmount);
        _currentAetherAmount += canAddAmount;
        
        OnResourceChanged?.Invoke(ResourceType.Aether, _currentAetherAmount, maxStorageAmount);
        ResourceManager.Instance.AddResource(ResourceType.Aether, canAddAmount); 
        
        return canAddAmount > 0;
    }
    
    public bool TryUseResources(ResourceCost[] costs)
    {
        // Battery only stores Aether, so check if all costs are Aether
        int totalAetherNeeded = 0;
        foreach (var cost in costs)
        {
            if (cost.resourceType != ResourceType.Aether)
            {
                return false; // Battery can't provide non-Aether resources
            }
            totalAetherNeeded += cost.amount;
        }

        if (_currentAetherAmount < totalAetherNeeded)
        {
            return false;
        }

        _currentAetherAmount -= totalAetherNeeded;
        OnResourceChanged?.Invoke(ResourceType.Aether, _currentAetherAmount, maxStorageAmount);

        return true;
    }
    
    public bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn)
    {
        // Battery only stores Aether
        if (type != ResourceType.Aether)
        {
            amountWithdrawn = 0;
            return false;
        }

        if (_currentAetherAmount <= 0)
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
        // Battery only stores Aether
        int totalAetherNeeded = 0;
        foreach (var cost in costs)
        {
            if (cost.resourceType != ResourceType.Aether)
            {
                return false; // Battery can't provide non-Aether resources
            }
            totalAetherNeeded += cost.amount;
        }

        return _currentAetherAmount >= totalAetherNeeded;
    }
    
    public int GetCurrentResourceAmount(ResourceType type)
    {
        // Battery only stores Aether
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

