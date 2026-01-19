using System;
using System.Collections.Generic;
using UnityEngine;

public class StorageTrackerManager : MonoBehaviour
{
    private AetherConsumptionManager _aetherConsumptionManager;

    private ResourceManager _resourceManager;

    public int CurrentTotalStoredResourceAmount { get; private set; }
    public int MaxStorableResourceAmount { get; private set; }
    public int CurrentAetherAmount { get; private set; }
    public int MaxStorableAetherAmount { get; private set; }

    private void Start()
    {
        _resourceManager = FindFirstObjectByType<ResourceManager>();
        _aetherConsumptionManager = FindFirstObjectByType<AetherConsumptionManager>();

        if (_resourceManager != null) {
            ResourceManager.OnNewStorageAdded += OnStorageAdded;
            ResourceManager.OnStorageRemoved += OnStorageRemoved;
            ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;

            List<IStorage> existingStorages = _resourceManager.GetAllStorages();
            foreach (IStorage storage in existingStorages) {
                if (storage != null) {
                    storage.OnResourceChanged += OnStorageResourceChanged;
                }
            }
        }

        UpdateStorageValues();
        UpdateAetherValues();
    }

    private void OnDestroy()
    {
        if (_resourceManager != null) {
            ResourceManager.OnNewStorageAdded -= OnStorageAdded;
            ResourceManager.OnStorageRemoved -= OnStorageRemoved;
            ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;

            List<IStorage> storages = _resourceManager.GetAllStorages();
            foreach (IStorage storage in storages) {
                if (storage != null) {
                    storage.OnResourceChanged -= OnStorageResourceChanged;
                }
            }
        }
    }

    public event Action OnStorageChanged;
    public event Action OnAetherChanged;

    private void OnStorageAdded()
    {
        if (_resourceManager != null) {
            List<IStorage> storages = _resourceManager.GetAllStorages();
            IStorage newStorage = storages[storages.Count - 1];
            if (newStorage != null) {
                newStorage.OnResourceChanged += OnStorageResourceChanged;
            }
        }

        UpdateStorageValues();
        UpdateAetherValues();
    }

    private void OnStorageRemoved(IStorage storage)
    {
        if (storage != null) {
            storage.OnResourceChanged -= OnStorageResourceChanged;
        }

        UpdateStorageValues();
        UpdateAetherValues();
    }

    private void OnStorageResourceChanged(ResourceType type, int current, int max)
    {
        UpdateStorageValues();
        if (type == ResourceType.Aether) {
            UpdateAetherValues();
        }
    }

    private void OnResourceAmountChanged(ResourceType type, int amount)
    {
        if (type == ResourceType.Aether) {
            UpdateAetherValues();
        }
        UpdateStorageValues();
    }

    private void UpdateStorageValues()
    {
        if (_resourceManager == null) return;

        List<IStorage> storages = _resourceManager.GetAllStorages();

        int currentTotal = 0;
        int maxTotal = 0;

        foreach (IStorage storage in storages) {
            if (storage == null) continue;

            currentTotal += storage.GetTotalCurrentAmount();
            maxTotal += storage.GetMaxCapacity();
        }

        bool changed = CurrentTotalStoredResourceAmount != currentTotal ||
            MaxStorableResourceAmount != maxTotal;

        CurrentTotalStoredResourceAmount = currentTotal;
        MaxStorableResourceAmount = maxTotal;

        if (changed) {
            OnStorageChanged?.Invoke();
        }
    }

    private void UpdateAetherValues()
    {
        if (_resourceManager == null || _aetherConsumptionManager == null) return;

        int currentAether = _resourceManager.GetResourceAmount(ResourceType.Aether);
        int maxAether = _aetherConsumptionManager.MaxAetherCapacity;

        bool changed = CurrentAetherAmount != currentAether ||
            MaxStorableAetherAmount != maxAether;

        CurrentAetherAmount = currentAether;
        MaxStorableAetherAmount = maxAether;

        if (changed) {
            OnAetherChanged?.Invoke();
        }
    }
}
