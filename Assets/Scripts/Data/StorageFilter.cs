using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds per-storage filter configuration: which resource types are accepted
/// and the storage's priority level (0=Low, 1=Medium, 2=Important, 3=Critical).
/// </summary>
[Serializable]
public class StorageFilter
{
    public int Priority;
    public HashSet<ResourceType> AllowedResources;

    /// <summary>
    /// Initializes a new StorageFilter with all provided resource types allowed and Priority 0 (Low).
    /// </summary>
    public StorageFilter(IEnumerable<ResourceType> allResourceTypes)
    {
        Priority = 0;
        AllowedResources = new HashSet<ResourceType>();
        foreach (ResourceType type in allResourceTypes)
        {
            AllowedResources.Add(type);
        }
    }

    public bool IsAllowed(ResourceType type)
    {
        return AllowedResources.Contains(type);
    }

    public void SetAllowed(ResourceType type, bool allowed)
    {
        if (allowed)
        {
            AllowedResources.Add(type);
        }
        else
        {
            AllowedResources.Remove(type);
        }
    }

    public void SetPriority(int priority)
    {
        Priority = Mathf.Clamp(priority, 0, 3);
    }
}
