using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public interface IStorage
{
    event Action<ResourceType, int, int> OnResourceChanged; 
    
    bool TryAddResource(ResourceType type, int amount);
    bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn);
    bool HasEnoughResources(ResourceCost[] costs);
    
    int GetCurrentResourceAmount(ResourceType type);
    int GetMaxCapacity();
    int GetTotalCurrentAmount();

    Dictionary<ResourceType, int> GetStoredResources();
    
    Vector3 GetPosition();
}