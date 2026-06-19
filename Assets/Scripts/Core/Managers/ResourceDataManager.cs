using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResourceDataManager : MonoBehaviour
{
    private readonly HashSet<ResourceNode> _allResources = new();
    private readonly List<IStorage> _allStorages = new();
    private readonly Dictionary<IStorage, int> _reservedCapacity = new Dictionary<IStorage, int>();
    private readonly Dictionary<ResourceType, int> _resourceCounts = new();
    private readonly Dictionary<ResourceType, ResourceStats> _resourceStats = new();
    private MainStructure _mainStructure;

    // Redistribution system
    private readonly List<RedistributionTask> _pendingRedistributions = new List<RedistributionTask>();

    // Per-storage, per-resource source reservations so multiple units cannot
    // claim the same resource amount from the same source.
    private readonly Dictionary<IStorage, Dictionary<ResourceType, int>> _reservedSourceAmount =
        new Dictionary<IStorage, Dictionary<ResourceType, int>>();

    public static ResourceDataManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public static event Action OnNewStorageAdded;
    public static event Action<IStorage> OnStorageRemoved;
    public static event Action<IStorage, int> OnStorageSpaceFreed;
    public static event Action<ResourceType, int> OnResourceAmountChanged;
    public static event Action<ResourceNode> OnResourceNodeAdded;
    public static event Action<ResourceNode> OnResourceNodeRemoved;

    public void ReserveCapacity(IStorage storage, int amount)
    {
        if (storage == null || amount <= 0) return;
        _reservedCapacity.TryGetValue(storage, out int current);
        _reservedCapacity[storage] = current + amount;
    }

    public void ReleaseCapacity(IStorage storage, int amount)
    {
        if (storage == null || amount <= 0) return;
        if (_reservedCapacity.TryGetValue(storage, out int current))
        {
            int remaining = Mathf.Max(0, current - amount);
            if (remaining <= 0)
            {
                _reservedCapacity.Remove(storage);
            }
            else
            {
                _reservedCapacity[storage] = remaining;
            }
        }
    }

    public int GetAvailableCapacity(IStorage storage)
    {
        if (storage == null) return 0;
        int reserved = _reservedCapacity.TryGetValue(storage, out int r) ? r : 0;
        return Mathf.Max(0, storage.GetMaxCapacity() - storage.GetTotalCurrentAmount() - reserved);
    }

    public void NotifyStorageSpaceFreed(IStorage storage, int availableCapacity)
    {
        if (storage != null && availableCapacity > 0)
        {
            OnStorageSpaceFreed?.Invoke(storage, availableCapacity);
        }
    }

    public void InitializeResourceStats(List<ResourceStats> resourceStatsList)
    {
        _resourceStats.Clear();
        foreach (ResourceStats stats in resourceStatsList)
        {
            _resourceStats[stats.resourceType] = stats;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene" || scene.name == "TutorialScene")
        {
            _mainStructure = null;
            _allStorages.Clear();
            _allResources.Clear();
            _reservedCapacity.Clear();
            _pendingRedistributions.Clear();
            _reservedSourceAmount.Clear();

            StartCoroutine(DelayedSceneInitialization());
        }
    }

    private IEnumerator DelayedSceneInitialization()
    {
        yield return null;

        ResetResourceCount();

        yield return null;

        RecalculateResourceCountsFromStorages();
    }

    public void RecalculateResourceCountsFromStorages()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            int totalAmount = 0;
            foreach (IStorage storage in _allStorages)
            {
                totalAmount += storage.GetCurrentResourceAmount(type);
            }
            SetResource(type, totalAmount);
        }
    }

    private void SetResource(ResourceType type, int amount)
    {
        _resourceCounts[type] = amount;
        OnResourceAmountChanged?.Invoke(type, amount);
    }

    private void ResetResourceCount()
    {
        _resourceCounts.Clear();
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            SetResource(type, 0);
        }
    }

    public void AddResource(ResourceType type, int amount)
    {
        int currentAmount = GetResourceAmount(type);
        SetResource(type, currentAmount + amount);
    }

    public bool RemoveResource(ResourceType type, int amount)
    {
        int currentAmount = GetResourceAmount(type);
        if (currentAmount < amount)
        {
            return false;
        }
        SetResource(type, currentAmount - amount);
        return true;
    }

    public bool SpendResources(ResourceCost[] costs)
    {
        if (!HasEnoughResources(costs))
        {
            Debug.Log("Not Enough Resources");
            return false;
        }

        Dictionary<ResourceType, int> requiredResources = new Dictionary<ResourceType, int>();
        foreach (ResourceCost cost in costs)
        {
            if (requiredResources.ContainsKey(cost.resourceType))
            {
                requiredResources[cost.resourceType] += cost.amount;
            }
            else
            {
                requiredResources.Add(cost.resourceType, cost.amount);
            }
        }

        Dictionary<ResourceType, int> availableInStorages = new Dictionary<ResourceType, int>();
        List<IStorage> storages = GetAllStorages();

        foreach (ResourceType type in requiredResources.Keys)
        {
            int totalInStorages = 0;
            foreach (IStorage storage in storages)
            {
                totalInStorages += storage.GetCurrentResourceAmount(type);
            }
            availableInStorages[type] = totalInStorages;
        }

        foreach (KeyValuePair<ResourceType, int> req in requiredResources)
        {
            if (GetResourceAmount(req.Key) < req.Value)
            {
                Debug.Log($"Not Enough Resources for {req.Key} after final check.");
                return false;
            }
        }

        foreach (ResourceCost cost in costs)
        {
            int remainingToWithdraw = cost.amount;
            foreach (IStorage storage in storages)
            {
                if (remainingToWithdraw <= 0) break;
                if (storage.TryWithdrawResource(cost.resourceType, remainingToWithdraw, out int amountWithdrawn))
                {
                    remainingToWithdraw -= amountWithdrawn;
                }
            }
        }

        RecalculateResourceCountsFromStorages();

        return true;
    }

    public bool HasEnoughResources(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            return true;
        }

        Dictionary<ResourceType, int> requiredResources = new Dictionary<ResourceType, int>();
        foreach (ResourceCost cost in costs)
        {
            if (cost == null)
            {
                continue;
            }

            if (requiredResources.ContainsKey(cost.resourceType))
            {
                requiredResources[cost.resourceType] += cost.amount;
            }
            else
            {
                requiredResources[cost.resourceType] = cost.amount;
            }
        }

        foreach (KeyValuePair<ResourceType, int> req in requiredResources)
        {
            if (GetResourceAmount(req.Key) < req.Value)
            {
                return false;
            }
        }

        return true;
    }

    public int GetResourceAmount(ResourceType type)
    {
        _resourceCounts.TryGetValue(type, out int value);
        return value;
    }

    public ResourceStats GetResourceStats(ResourceType type)
    {
        _resourceStats.TryGetValue(type, out ResourceStats stats);
        return stats;
    }

    public void SetResourceStats(ResourceType type, ResourceStats stats)
    {
        _resourceStats[type] = stats;
    }

    public void AddStorage(IStorage storage)
    {
        if (!_allStorages.Contains(storage))
        {
            _allStorages.Add(storage);
            storage.OnFilterChanged += () => ScanForRedistribution(storage);
            OnNewStorageAdded?.Invoke();
            OnStorageRegistered(storage);
        }
    }

    public void RemoveStorage(IStorage storage)
    {
        if (_allStorages.Remove(storage))
        {
            _reservedCapacity.Remove(storage);
            _reservedSourceAmount.Remove(storage);

            // Cancel all redistribution tasks that reference this storage.
            for (int i = _pendingRedistributions.Count - 1; i >= 0; i--)
            {
                RedistributionTask task = _pendingRedistributions[i];
                if (task.Source == storage || task.Destination == storage)
                {
                    CancelRedistributionTask(task);
                }
            }

            OnStorageRemoved?.Invoke(storage);
        }
    }

    public List<IStorage> GetAllStorages()
    {
        return _allStorages;
    }

    public void RegisterMainStructure(MainStructure mainStructure)
    {
        _mainStructure = mainStructure;
    }

    public MainStructure GetMainStructure()
    {
        return _mainStructure;
    }

    public void AddResourceNode(ResourceNode node)
    {
        if (_allResources.Add(node))
        {
            OnResourceNodeAdded?.Invoke(node);
        }
    }

    public void RemoveResourceNode(ResourceNode node)
    {
        if (_allResources.Remove(node))
        {
            OnResourceNodeRemoved?.Invoke(node);
        }
    }

    public List<ResourceNode> GetAllResources()
    {
        return new List<ResourceNode>(_allResources);
    }

    public IStorage FindClosestStorageWithResource(Vector3 position, ResourceType type, int minAmount)
    {
        IStorage closestStorage = null;
        float minDistance = float.MaxValue;

        foreach (IStorage storage in _allStorages)
        {
            if (storage.GetCurrentResourceAmount(type) >= minAmount)
            {
                float dist = Vector3.Distance(position, storage.GetPosition());
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestStorage = storage;
                }
            }
        }
        return closestStorage;
    }

    // -------------------------------------------------------------------------
    // Source reservation helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reserves an amount of a specific resource at the source storage so that
    /// multiple units cannot claim the same resource simultaneously.
    /// </summary>
    public void ReserveSourceAmount(IStorage storage, ResourceType type, int amount)
    {
        if (storage == null || amount <= 0) return;

        if (!_reservedSourceAmount.TryGetValue(storage, out Dictionary<ResourceType, int> perType))
        {
            perType = new Dictionary<ResourceType, int>();
            _reservedSourceAmount[storage] = perType;
        }

        perType.TryGetValue(type, out int current);
        perType[type] = current + amount;
    }

    /// <summary>
    /// Releases a previously reserved source amount.
    /// </summary>
    public void ReleaseSourceAmount(IStorage storage, ResourceType type, int amount)
    {
        if (storage == null || amount <= 0) return;
        if (!_reservedSourceAmount.TryGetValue(storage, out Dictionary<ResourceType, int> perType)) return;

        if (perType.TryGetValue(type, out int current))
        {
            int remaining = Mathf.Max(0, current - amount);
            if (remaining <= 0)
            {
                perType.Remove(type);
            }
            else
            {
                perType[type] = remaining;
            }

            if (perType.Count == 0)
            {
                _reservedSourceAmount.Remove(storage);
            }
        }
    }

    /// <summary>
    /// Returns the amount of a resource that is available to be claimed from a
    /// source storage (actual stored amount minus already-reserved source amounts).
    /// </summary>
    public int GetAvailableSourceAmount(IStorage storage, ResourceType type)
    {
        if (storage == null) return 0;
        int stored = storage.GetCurrentResourceAmount(type);
        int reserved = 0;
        if (_reservedSourceAmount.TryGetValue(storage, out Dictionary<ResourceType, int> perType))
        {
            perType.TryGetValue(type, out reserved);
        }
        return Mathf.Max(0, stored - reserved);
    }

    // -------------------------------------------------------------------------
    // Routing: best deposit target
    // -------------------------------------------------------------------------

    /// <summary>
    /// Finds the best storage to deposit a given resource type into.
    /// Rules (in priority order):
    ///   1. Filter must allow the resource type.
    ///   2. Available capacity > 0 (after reservations).
    ///   3. Battery storages are excluded.
    ///   4. Prefer highest-priority storage; break ties by shortest distance from fromPosition.
    /// Returns null if no valid storage is found.
    /// </summary>
    public IStorage GetBestDepositTarget(ResourceType type, Vector3 fromPosition, IStorage excludeStorage = null)
    {
        IStorage bestStorage = null;
        int bestPriority = -1;
        float bestDistance = float.MaxValue;

        foreach (IStorage storage in _allStorages)
        {
            if (storage == null) continue;
            if (storage == excludeStorage) continue;
            if (storage is Battery) continue;

            StorageFilter filter = storage.GetFilter();
            if (filter == null || !filter.IsAllowed(type)) continue;

            int available = GetAvailableCapacity(storage);
            if (available <= 0) continue;

            int priority = filter.Priority;
            float distance = Vector3.Distance(fromPosition, storage.GetPosition());

            if (priority > bestPriority || (priority == bestPriority && distance < bestDistance))
            {
                bestPriority = priority;
                bestDistance = distance;
                bestStorage = storage;
            }
        }

        return bestStorage;
    }

    // -------------------------------------------------------------------------
    // Redistribution scanning
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans the storage's current resources and enqueues RedistributionTasks for
    /// any resource that should move to a higher-priority destination.
    /// Call after a filter change or when a new higher-priority storage is added.
    /// depth limits recursive chains to a maximum of 2.
    /// </summary>
    public void ScanForRedistribution(IStorage storage, int depth = 0)
    {
        if (depth >= 2) return;
        if (storage == null) return;

        StorageFilter sourceFilter = storage.GetFilter();
        if (sourceFilter == null) return;

        Dictionary<ResourceType, int> stored = storage.GetStoredResources();
        foreach (KeyValuePair<ResourceType, int> entry in stored)
        {
            ResourceType type = entry.Key;
            int amount = entry.Value;
            if (amount <= 0) continue;

            // Skip if a task for this (source, type) pair already exists.
            if (HasPendingTask(storage, type)) continue;

            int availableSourceAmount = GetAvailableSourceAmount(storage, type);
            if (availableSourceAmount <= 0) continue;

            if (!sourceFilter.IsAllowed(type))
            {
                // Resource is not welcome in this storage — try to find any valid destination.
                IStorage destination = GetBestDepositTarget(type, storage.GetPosition(), storage);
                if (destination != null)
                {
                    EnqueueRedistributionTask(storage, destination, type, availableSourceAmount);
                }
                // No destination: resource stays put until capacity opens elsewhere.
            }
            else
            {
                // Resource is allowed here but a higher-priority destination may exist.
                IStorage destination = GetBestDepositTarget(type, storage.GetPosition(), storage);
                if (destination != null && destination.GetFilter() != null &&
                    destination.GetFilter().Priority > sourceFilter.Priority)
                {
                    EnqueueRedistributionTask(storage, destination, type, availableSourceAmount);
                }
            }
        }
    }

    private bool HasPendingTask(IStorage source, ResourceType type)
    {
        foreach (RedistributionTask existing in _pendingRedistributions)
        {
            if (existing.Source == source && existing.ResourceType == type)
            {
                return true;
            }
        }
        return false;
    }

    private void EnqueueRedistributionTask(IStorage source, IStorage destination, ResourceType type, int amount)
    {
        // Reserve the amount at the source so it is not double-claimed.
        ReserveSourceAmount(source, type, amount);
        // Reserve capacity at the destination so it is not double-allocated.
        ReserveCapacity(destination, amount);

        RedistributionTask task = new RedistributionTask
        {
            Source = source,
            Destination = destination,
            ResourceType = type,
            Amount = amount,
            IsClaimed = false
        };
        _pendingRedistributions.Add(task);
    }

    // -------------------------------------------------------------------------
    // Task claiming and completion
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by an idle unit to claim the redistribution task whose source
    /// is closest to fromPosition. Marks the task as IsClaimed so no other
    /// unit picks it up.
    /// Returns false when no unclaimed tasks are available.
    /// </summary>
    public bool TryClaimRedistributionTask(Vector3 fromPosition, out RedistributionTask task)
    {
        task = null;
        float bestDistance = float.MaxValue;

        foreach (RedistributionTask pending in _pendingRedistributions)
        {
            if (pending.IsClaimed) continue;
            if (pending.Source == null || pending.Destination == null) continue;

            float distance = Vector3.Distance(fromPosition, pending.Source.GetPosition());
            if (distance < bestDistance)
            {
                bestDistance = distance;
                task = pending;
            }
        }

        if (task != null)
        {
            task.IsClaimed = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Called when a unit has successfully moved the resource. Releases all
    /// reservations and removes the task from the queue.
    /// </summary>
    public void CompleteRedistributionTask(RedistributionTask task)
    {
        if (task == null) return;

        ReleaseSourceAmount(task.Source, task.ResourceType, task.Amount);
        ReleaseCapacity(task.Destination, task.Amount);
        _pendingRedistributions.Remove(task);
    }

    /// <summary>
    /// Called when a task cannot be completed (e.g., storage destroyed, unit
    /// interrupted). Releases all reservations and removes the task so it may
    /// be re-queued on the next scan.
    /// </summary>
    public void CancelRedistributionTask(RedistributionTask task)
    {
        if (task == null) return;

        ReleaseSourceAmount(task.Source, task.ResourceType, task.Amount);
        if (task.Destination != null)
        {
            ReleaseCapacity(task.Destination, task.Amount);
        }
        _pendingRedistributions.Remove(task);
    }

    /// <summary>
    /// Called when a new IStorage is registered. Triggers redistribution scans
    /// on all existing lower-priority storages that hold resources now better
    /// suited for the new storage.
    /// </summary>
    public void OnStorageRegistered(IStorage newStorage)
    {
        if (newStorage == null) return;
        if (newStorage is Battery) return;

        StorageFilter newFilter = newStorage.GetFilter();
        if (newFilter == null) return;

        foreach (IStorage existing in _allStorages)
        {
            if (existing == null || existing == newStorage) continue;
            if (existing is Battery) continue;

            StorageFilter existingFilter = existing.GetFilter();
            if (existingFilter == null) continue;

            // Only scan storages with lower or equal priority — they might have
            // resources that should now flow to the new, possibly higher-priority storage.
            if (existingFilter.Priority <= newFilter.Priority)
            {
                ScanForRedistribution(existing);
            }
        }
    }
}

