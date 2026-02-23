using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BaseStorage : Damageable, IStorage
{
    [Header("Base Storage Settings")]
    [SerializeField] protected int maxStorageAmount = 1000;

    protected readonly Dictionary<ResourceType, int> currentResources = new Dictionary<ResourceType, int>();

    protected override void Awake()
    {
        base.Awake();

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) {
            currentResources[type] = 0;
        }
    }

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        RefreshAllResourcesUI();
    }

    protected override void OnDestroy()
    {
        if (ResourceManager.Instance != null) {
            ResourceManager.Instance.RemoveStorage(this);
        }
    }

    public event Action<ResourceType, int, int> OnResourceChanged;

    public virtual bool TryAddResource(ResourceType type, int amount)
    {
        int totalAmount = GetTotalCurrentAmount();
        bool isFull = totalAmount >= maxStorageAmount;

        if (isFull) {
            return false;
        }

        int canAddAmount = Mathf.Min(amount, maxStorageAmount - totalAmount);
        currentResources[type] += canAddAmount;

        NotifyResourceChange(type, canAddAmount);

        return canAddAmount > 0;
    }

    public virtual bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn)
    {
        int availableAmount = GetCurrentResourceAmount(type);
        if (availableAmount <= 0) {
            amountWithdrawn = 0;
            return false;
        }

        amountWithdrawn = Mathf.Min(availableAmount, amountToWithdraw);
        currentResources[type] -= amountWithdrawn;

        OnResourceChanged?.Invoke(type, currentResources[type], maxStorageAmount);
        if (ResourceManager.Instance != null) {
            ResourceManager.Instance.RemoveResource(type, amountWithdrawn);
        }
        
        return true;
    }

    public virtual bool HasEnoughResources(ResourceCost[] costs)
    {
        foreach (ResourceCost cost in costs) {
            if (currentResources[cost.resourceType] < cost.amount) return false;
        }
        return true;
    }

    public virtual int GetCurrentResourceAmount(ResourceType type)
    {
        return currentResources.ContainsKey(type) ? currentResources[type] : 0;
    }

    public virtual int GetMaxCapacity()
    {
        return maxStorageAmount;
    }

    public virtual int GetTotalCurrentAmount()
    {
        return currentResources.Values.Sum();
    }

    public virtual Vector3 GetPosition()
    {
        return transform.position;
    }

    public virtual Dictionary<ResourceType, int> GetStoredResources()
    {
        return currentResources;
    }

    /// <summary>
    ///     Sets the maximum storage capacity (used by module effects)
    /// </summary>
    public virtual void SetMaxStorage(int newMaxStorage)
    {
        maxStorageAmount = newMaxStorage;
        RefreshAllResourcesUI();
    }

    private void NotifyResourceChange(ResourceType type, int addedAmount)
    {
        OnResourceChanged?.Invoke(type, currentResources[type], maxStorageAmount);

        if (addedAmount > 0 && ResourceManager.Instance != null)
            ResourceManager.Instance.AddResource(type, addedAmount);
    }

    protected void InvokeResourceChanged(ResourceType type)
    {
        if (currentResources.TryGetValue(type, out int resource)) {
            OnResourceChanged?.Invoke(type, resource, maxStorageAmount);
        }
    }

    protected void RefreshAllResourcesUI()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) {
            OnResourceChanged?.Invoke(type, GetCurrentResourceAmount(type), GetMaxCapacity());
        }
    }
}
