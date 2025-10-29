using System;
using UnityEngine;

public interface IStorage
{
    event Action<ResourceType, int, int> OnResourceChanged; 
    
    bool TryAddResource(ResourceType type, int amount);
    bool TryUseResources(CardCost[] costs);
    bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn);
    bool HasEnoughResources(CardCost[] costs);
    
    int GetCurrentResourceAmount(ResourceType type);
    int GetMaxCapacity();
    int GetTotalCurrentAmount();
    
    Vector3 GetPosition();
}