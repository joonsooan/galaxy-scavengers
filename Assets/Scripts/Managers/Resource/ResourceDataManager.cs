using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResourceDataManager : MonoBehaviour
{
    private readonly HashSet<ResourceNode> _allResources = new ();
    private readonly List<IStorage> _allStorages = new ();
    private readonly Dictionary<ResourceType, int> _resourceCounts = new ();
    private readonly Dictionary<ResourceType, ResourceStats> _resourceStats = new ();
    private MainStructure _mainStructure;

    public static ResourceDataManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else {
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
    public static event Action<ResourceType, int> OnResourceAmountChanged;
    public static event Action<ResourceNode> OnResourceNodeAdded;
    public static event Action<ResourceNode> OnResourceNodeRemoved;

    public void InitializeResourceStats(List<ResourceStats> resourceStatsList)
    {
        _resourceStats.Clear();
        foreach (ResourceStats stats in resourceStatsList) {
            _resourceStats[stats.resourceType] = stats;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene") {
            _mainStructure = null;
            _allStorages.Clear();
            _allResources.Clear();
            
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
        if (!HasEnoughResources(costs)) {
            Debug.Log("Not Enough Resources");
            return false;
        }

        Dictionary<ResourceType, int> requiredResources = new Dictionary<ResourceType, int>();
        foreach (ResourceCost cost in costs) {
            if (requiredResources.ContainsKey(cost.resourceType)) {
                requiredResources[cost.resourceType] += cost.amount;
            }
            else {
                requiredResources.Add(cost.resourceType, cost.amount);
            }
        }

        Dictionary<ResourceType, int> availableInStorages = new Dictionary<ResourceType, int>();
        List<IStorage> storages = GetAllStorages();
        foreach (ResourceType type in requiredResources.Keys) {
            int totalInStorages = 0;
            foreach (IStorage storage in storages) {
                totalInStorages += storage.GetCurrentResourceAmount(type);
            }
            availableInStorages[type] = totalInStorages;
        }

        foreach (KeyValuePair<ResourceType, int> req in requiredResources) {
            if (GetResourceAmount(req.Key) < req.Value) {
                Debug.Log($"Not Enough Resources for {req.Key} after final check.");
                return false;
            }
        }

        foreach (ResourceCost cost in costs) {
            int remainingToWithdraw = cost.amount;
            foreach (IStorage storage in storages) {
                if (remainingToWithdraw <= 0) break;
                if (storage.TryWithdrawResource(cost.resourceType, remainingToWithdraw, out int amountWithdrawn)) {
                    remainingToWithdraw -= amountWithdrawn;
                }
            }
        }
        
        RecalculateResourceCountsFromStorages();

        return true;
    }

    public bool HasEnoughResources(ResourceCost[] costs)
    {
        foreach (ResourceCost cost in costs) {
            if (_resourceCounts.GetValueOrDefault(cost.resourceType) < cost.amount) {
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
        if (!_allStorages.Contains(storage)) {
            _allStorages.Add(storage);
            OnNewStorageAdded?.Invoke();
        }
    }

    public void RemoveStorage(IStorage storage)
    {
        if (_allStorages.Remove(storage)) {
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

        foreach (IStorage storage in _allStorages) {
            if (storage.GetCurrentResourceAmount(type) >= minAmount) {
                float dist = Vector3.Distance(position, storage.GetPosition());
                if (dist < minDistance) {
                    minDistance = dist;
                    closestStorage = storage;
                }
            }
        }
        return closestStorage;
    }
}

