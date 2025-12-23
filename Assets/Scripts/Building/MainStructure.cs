using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainStructure : Damageable, IStorage, IClickable
{
    [Header("Storage Settings")]
    [SerializeField] private int maxStorageAmount = 1000;

    [Header("Production Settings")]
    [SerializeField] private List<UnitData> producibleUnits;

    private readonly Dictionary<ResourceType, int> _currentResources = new ();
    private readonly Queue<UnitData> _productionQueue = new ();
    private Vector3 _unitSpawnPos;

    private bool _isProducing;
    private GameObject _sliderInstance;
    private InventorySystem _inventorySystem;

    protected override void Awake()
    {
        base.Awake();

        currentHealth = maxHealth;
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) {
            _currentResources[type] = 0;
        }
        
        _unitSpawnPos = transform.position - 2 * Vector3.up;
        
    }

    private void Start()
    {
        InitUnitBtns();
        InitResources();
        _inventorySystem = GetComponent<InventorySystem>();
    }

    public void OnClicked()
    {
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.ShowMainStructureUI();
        }

        if (_inventorySystem == null)
        {
            _inventorySystem = GetComponent<InventorySystem>();
        }

        if (_inventorySystem != null)
        {
            _inventorySystem.ToggleInventory();
        }
    }

    private void OnDestroy()
    {
        if (ResourceManager.Instance != null) {
            ResourceManager.Instance.RemoveStorage(this);
            GameManager.Instance.GameOver();
        }

        if (_sliderInstance != null) {
            Destroy(_sliderInstance);
        }
    }

    public event Action<ResourceType, int, int> OnResourceChanged;

    public bool TryAddResource(ResourceType type, int amount)
    {
        int totalAmount = GetTotalCurrentAmount();
        if (totalAmount >= maxStorageAmount) {
            return false;
        }

        int canAddAmount = Mathf.Min(amount, maxStorageAmount - totalAmount);

        _currentResources[type] += canAddAmount;

        OnResourceChanged?.Invoke(type, _currentResources[type], maxStorageAmount);
        ResourceManager.Instance.AddResource(type, canAddAmount);

        return canAddAmount > 0;
    }

    public bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn)
    {
        int availableAmount = GetCurrentResourceAmount(type);
        if (availableAmount <= 0) {
            amountWithdrawn = 0;
            return false;
        }

        amountWithdrawn = Mathf.Min(availableAmount, amountToWithdraw);
        _currentResources[type] -= amountWithdrawn;

        OnResourceChanged?.Invoke(type, _currentResources[type], maxStorageAmount);
        
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.RemoveResource(type, amountWithdrawn);
        }

        return true;
    }

    public bool HasEnoughResources(ResourceCost[] costs)
    {
        foreach (ResourceCost cost in costs) {
            if (_currentResources[cost.resourceType] < cost.amount) {
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

    private void InitUnitBtns()
    {
        GameObject lifterMakeButtonObj = GameObject.Find("Lifter Make Btn");

        if (lifterMakeButtonObj != null)
        {
            Button btn = lifterMakeButtonObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => AddUnitToQueue(0));
            }
        }
    }

    private void InitResources()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) {
            OnResourceChanged?.Invoke(type, GetCurrentResourceAmount(type), GetMaxCapacity());
        }
    }

    public void InitializeStorage(ResourceType type, int amount)
    {
        if (_currentResources.ContainsKey(type)) {
            _currentResources[type] = Mathf.Min(amount, maxStorageAmount);
        }
    }

    public void UpdateStorageUI()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) {
            OnResourceChanged?.Invoke(type, GetCurrentResourceAmount(type), GetMaxCapacity());
        }
    }

    public int AddResourceToStorageOnly(ResourceType type, int amount)
    {
        int totalAmount = GetTotalCurrentAmount();
        if (totalAmount >= maxStorageAmount) {
            return 0;
        }

        int canAddAmount = Mathf.Min(amount, maxStorageAmount - totalAmount);
        _currentResources[type] += canAddAmount;
        OnResourceChanged?.Invoke(type, _currentResources[type], maxStorageAmount);

        return canAddAmount;
    }

    private void AddUnitToQueue(int unitIndex)
    {
        if (unitIndex < 0 || unitIndex >= producibleUnits.Count) {
            return;
        }

        UnitData unitData = producibleUnits[unitIndex];

        if (!ResourceManager.Instance.SpendResources(unitData.productionCosts)) {
            Debug.Log("Can't produce unit");
            return;
        }

        _productionQueue.Enqueue(unitData);

        if (!_isProducing) {
            StartCoroutine(ProcessProductionQueue());
        }
    }

    private IEnumerator ProcessProductionQueue()
    {
        _isProducing = true;

        while (_productionQueue.Count > 0) {
            UnitData unitToProduce = _productionQueue.Dequeue();

            yield return new WaitForSeconds(unitToProduce.productionTime);
            
            Instantiate(unitToProduce.unitPrefab, _unitSpawnPos, Quaternion.identity, BuildingManager.Instance.grid.transform);
        }

        _isProducing = false;
    }

    public Dictionary<ResourceType, int> GetStoredResources()
    {
        return _currentResources;
    }

    private void ShowStoredResources()
    {
        if (GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.DisplayStorageInfo(this);
        }
    }

    private void HideStoredResources()
    {
        if (GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.HideStorageInfo();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowStoredResources();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideStoredResources();
    }
}
