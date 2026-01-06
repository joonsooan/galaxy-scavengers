using System;
using System.Collections.Generic;
using UnityEngine;

public class BaseInventoryManager : MonoBehaviour
{
    private const string BaseInventoryPrefix = "BaseInventory_";
    
    private readonly Dictionary<ResourceType, int> _baseInventory = new();
    private readonly List<Module> _baseModules = new();

    public event Action<ResourceType, int> OnResourceChanged;
    public event Action<Module> OnModuleAdded;
    public event Action<Module> OnModuleRemoved;

    private void Start()
    {
        InitializeInventory();
    }

    private void InitializeInventory()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            _baseInventory[type] = 0;
        }
        
        LoadInventory();
    }

    public int GetResourceAmount(ResourceType type)
    {
        _baseInventory.TryGetValue(type, out int amount);
        return amount;
    }

    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        
        _baseInventory[type] = _baseInventory.GetValueOrDefault(type, 0) + amount;
        SaveResource(type);
        OnResourceChanged?.Invoke(type, _baseInventory[type]);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveAllInventory();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveAllInventory();
        }
    }

    public bool RemoveResource(ResourceType type, int amount)
    {
        if (amount <= 0) return false;
        
        int currentAmount = GetResourceAmount(type);
        if (currentAmount < amount)
        {
            return false;
        }
        
        _baseInventory[type] = currentAmount - amount;
        SaveResource(type);
        OnResourceChanged?.Invoke(type, _baseInventory[type]);
        return true;
    }

    public Dictionary<ResourceType, int> GetAllResources()
    {
        return new Dictionary<ResourceType, int>(_baseInventory);
    }

    public void TransferFromGameInventory(ResourceType type, int amount)
    {
        AddResource(type, amount);
    }

    private void SaveResource(ResourceType type)
    {
        string key = BaseInventoryPrefix + type;
        PlayerPrefs.SetInt(key, _baseInventory[type]);
        PlayerPrefs.Save();
    }

    private void LoadInventory()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            string key = BaseInventoryPrefix + type;
            if (PlayerPrefs.HasKey(key))
            {
                _baseInventory[type] = PlayerPrefs.GetInt(key);
            }
        }
    }

    public void SaveAllInventory()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            SaveResource(type);
        }
    }

    public void ClearAllInventory()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            _baseInventory[type] = 0;
            string key = BaseInventoryPrefix + type;
            PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
    }
    
    public void AddModule(Module module)
    {
        if (module == null) return;
        
        _baseModules.Add(module);
        OnModuleAdded?.Invoke(module);
    }
    
    public bool RemoveModule(Module module)
    {
        if (module == null) return false;
        
        bool removed = _baseModules.Remove(module);
        if (removed)
        {
            OnModuleRemoved?.Invoke(module);
        }
        return removed;
    }
    
    public List<Module> GetAllModules()
    {
        return new List<Module>(_baseModules);
    }
    
    public int GetModuleCount()
    {
        return _baseModules.Count;
    }
}

