using System;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class MainStructure : Damageable, IStorage
{
    public event Action<ResourceType, int, int> OnResourceChanged;
    
    [Header("UI Settings")]
    [SerializeField] private GameObject storageSliderPrefab;
    [SerializeField] private string canvasName = "ObjectUI_Canvas";
    [SerializeField] private Vector3 sliderOffset = new Vector3(0, 1.5f, 0);
    private GameObject _sliderInstance;
    
    [Header("Storage Settings")]
    [SerializeField] private int maxStorageAmount = 1000;

    [Header("Production Settings")]
    [SerializeField] private List<UnitData> producibleUnits;

    private readonly Dictionary<ResourceType, int> _currentResources = new();
    private readonly Queue<UnitData> _productionQueue = new();
    
    private bool _isProducing;

    protected override void Awake()
    {
        base.Awake();
        
        currentHealth = maxHealth;
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            _currentResources[type] = 0;
        }
    }

    private void Start()
    {
        GameObject unitMakeButtonObj = GameObject.Find("Unit Make Btn");

        if (unitMakeButtonObj != null)
        {
            Button unitMakeButton = unitMakeButtonObj.GetComponent<Button>();

            if (unitMakeButton != null)
            {
                unitMakeButton.onClick.AddListener(() => AddUnitToQueue(0));
            }
        }
        
        if (storageSliderPrefab != null)
        {
            Canvas canvas = GameObject.Find(canvasName)?.GetComponent<Canvas>();
            if (canvas != null)
            {
                _sliderInstance = Instantiate(storageSliderPrefab, canvas.transform);
                var controller = _sliderInstance.GetComponent<StorageSlider>();
                controller?.Initialize(this, sliderOffset);
            }
        }
        
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            OnResourceChanged?.Invoke(type, GetCurrentResourceAmount(type), GetMaxCapacity());
        }
    }
    
    public void InitializeStorage(ResourceType type, int amount)
    {
        if (_currentResources.ContainsKey(type))
        {
            _currentResources[type] = Mathf.Min(amount, maxStorageAmount);
        }
    }
    
    public void UpdateStorageUI()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            OnResourceChanged?.Invoke(type, GetCurrentResourceAmount(type), GetMaxCapacity());
        }
    }
    
    private void OnDestroy()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.RemoveStorage(this);
            GameManager.Instance.GameOver();
        }
        
        if (_sliderInstance != null)
        {
            Destroy(_sliderInstance);
        }
    }

    public bool StorageIsFull()
    {
        return GetTotalCurrentAmount() >= maxStorageAmount;
    }

    public bool TryAddResource(ResourceType type, int amount)
    {
        int totalAmount = GetTotalCurrentAmount();
        if (totalAmount >= maxStorageAmount)
        {
            return false;
        }

        int canAddAmount = Mathf.Min(amount, maxStorageAmount - totalAmount);
        
        _currentResources[type] += canAddAmount;
        
        OnResourceChanged?.Invoke(type, _currentResources[type], maxStorageAmount);
        ResourceManager.Instance.AddResource(type, canAddAmount); 
        
        return canAddAmount > 0;
    }
    
    public bool TryUseResources(CardCost[] costs)
    {
        if (!HasEnoughResources(costs))
        {
            return false;
        }

        foreach (var cost in costs)
        {
            _currentResources[cost.resourceType] -= cost.amount;
            OnResourceChanged?.Invoke(cost.resourceType, _currentResources[cost.resourceType], maxStorageAmount);
        }

        return true;
    } 
    
    public bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn)
    {
        int availableAmount = GetCurrentResourceAmount(type);
        if (availableAmount <= 0)
        {
            amountWithdrawn = 0;
            return false;
        }

        amountWithdrawn = Mathf.Min(availableAmount, amountToWithdraw);
        _currentResources[type] -= amountWithdrawn;
        
        OnResourceChanged?.Invoke(type, _currentResources[type], maxStorageAmount);
        
        return true;
    }
    
    public bool HasEnoughResources(CardCost[] costs)
    {
        foreach (var cost in costs)
        {
            if (_currentResources[cost.resourceType] < cost.amount)
            {
                return false;
            }
        }
        return true;
    }
    
    public int GetCurrentResourceAmount(ResourceType type)
    {
        return _currentResources[type];
    }
    
    public int GetMaxCapacity()
    {
        return maxStorageAmount;
    }

    public int GetTotalCurrentAmount()
    {
        return _currentResources.Values.Sum();
    }
    
    public Vector3 GetPosition()
    {
        return transform.position;
    }

    private void AddUnitToQueue(int unitIndex)
    {
        if (unitIndex < 0 || unitIndex >= producibleUnits.Count)
        {
            return;
        }

        UnitData unitData = producibleUnits[unitIndex];

        if (!ResourceManager.Instance.SpendResources(unitData.productionCosts))
        {
            Debug.Log("Can't produce unit");
            return;
        }

        _productionQueue.Enqueue(unitData);

        if (!_isProducing)
        {
            StartCoroutine(ProcessProductionQueue());
        }
    }

    private IEnumerator ProcessProductionQueue()
    {
        _isProducing = true;

        while (_productionQueue.Count > 0)
        {
            UnitData unitToProduce = _productionQueue.Dequeue();

            yield return new WaitForSeconds(unitToProduce.productionTime);

            Instantiate(unitToProduce.unitPrefab, transform.position, Quaternion.identity, BuildingManager.Instance.grid.transform);
        }
        
        _isProducing = false;
    }
}