using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceUnlockManager : MonoBehaviour
{
    public static ResourceUnlockManager Instance { get; private set; }

    private readonly HashSet<ResourceType> _unlockedResources = new HashSet<ResourceType>();

    public event Action<ResourceType> OnResourceUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _unlockedResources.Add(ResourceType.Ferrite);
        _unlockedResources.Add(ResourceType.Aether);
        _unlockedResources.Add(ResourceType.Biomass);
        _unlockedResources.Add(ResourceType.CryoCrystal);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool IsResourceUnlocked(ResourceType resourceType)
    {
        return _unlockedResources.Contains(resourceType);
    }

    public void UnlockResource(ResourceType resourceType)
    {
        if (_unlockedResources.Contains(resourceType))
        {
            return;
        }

        _unlockedResources.Add(resourceType);
        OnResourceUnlocked?.Invoke(resourceType);
    }
}
