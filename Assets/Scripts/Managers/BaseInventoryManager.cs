using System;
using System.Collections.Generic;
using UnityEngine;

public class BaseInventoryManager : MonoBehaviour
{
    public static BaseInventoryManager Instance { get; private set; }

    private const string BaseInventoryPrefix = "BaseInventory_";
    
    private readonly Dictionary<ResourceType, int> _baseInventory = new();

    public event Action<ResourceType, int> OnResourceChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeInventory();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeInventory()
    {
        // Initialize all resource types to 0
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            _baseInventory[type] = 0;
        }
        
        // Load saved data
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
        string key = BaseInventoryPrefix + type.ToString();
        PlayerPrefs.SetInt(key, _baseInventory[type]);
        PlayerPrefs.Save();
    }

    private void LoadInventory()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            string key = BaseInventoryPrefix + type.ToString();
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

    // Clear all base inventory (for testing/reset purposes)
    public void ClearAllInventory()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            _baseInventory[type] = 0;
            string key = BaseInventoryPrefix + type.ToString();
            PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
    }
}

