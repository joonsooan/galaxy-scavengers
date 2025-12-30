using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class BaseStorage : Damageable, IStorage
{
    public event Action<ResourceType, int, int> OnResourceChanged;

    [Header("Base Storage Settings")]
    [SerializeField] protected int maxStorageAmount = 1000;
    
    [Header("UI Settings")]
    [SerializeField] protected float hoverDelay = 0.3f;

    protected readonly Dictionary<ResourceType, int> currentResources = new();
    private BoxCollider2D _boxCollider;

    private bool _isUIActive;
    private float _hoverTimer;

    protected override void Awake()
    {
        base.Awake();
        _boxCollider = GetComponent<BoxCollider2D>();
        
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            currentResources[type] = 0;
        }
    }

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        RefreshAllResourcesUI();
    }

    protected virtual void Update()
    {
        CheckMouseHover();
    }
    
    protected virtual void OnDestroy()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.RemoveStorage(this);
        }
        if (_isUIActive) HideStoredResources();
    }

    private void CheckMouseHover()
    {
        if (GameManager.Instance.IsDragging() || UIUtils.IsPointerOverUI()) 
        {
            ResetHover();
            return;
        }

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hitColliders = Physics2D.OverlapPointAll(mousePos);

        bool isHitMe = false;
        foreach (var col in hitColliders)
        {
            if (col == _boxCollider)
            {
                isHitMe = true;
                break;
            }
        }

        if (isHitMe)
        {
            if (!_isUIActive)
            {
                _hoverTimer += Time.deltaTime;
                if (_hoverTimer >= hoverDelay)
                {
                    ShowUI();
                }
            }
        }
        else
        {
            ResetHover();
        }
    }

    private void ShowUI()
    {
        _isUIActive = true;
        ShowStoredResources();
    }

    private void ResetHover()
    {
        _hoverTimer = 0f;
        if (_isUIActive)
        {
            _isUIActive = false;
            HideStoredResources();
        }
    }
    
    public virtual bool TryAddResource(ResourceType type, int amount)
    {
        int totalAmount = GetTotalCurrentAmount();
        if (totalAmount >= maxStorageAmount) return false;

        int canAddAmount = Mathf.Min(amount, maxStorageAmount - totalAmount);
        currentResources[type] += canAddAmount;
        
        NotifyResourceChange(type, canAddAmount);
        
        return canAddAmount > 0;
    }

    public virtual bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn)
    {
        int availableAmount = GetCurrentResourceAmount(type);
        if (availableAmount <= 0)
        {
            amountWithdrawn = 0;
            return false;
        }

        amountWithdrawn = Mathf.Min(availableAmount, amountToWithdraw);
        currentResources[type] -= amountWithdrawn;
        
        OnResourceChanged?.Invoke(type, currentResources[type], maxStorageAmount);
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.RemoveResource(type, amountWithdrawn);
        }
        if (_isUIActive) ShowStoredResources();
        
        return true;
    }

    public virtual bool HasEnoughResources(ResourceCost[] costs)
    {
        foreach (var cost in costs)
        {
            if (currentResources[cost.resourceType] < cost.amount) return false;
        }
        return true;
    }

    public virtual int GetCurrentResourceAmount(ResourceType type) => currentResources.ContainsKey(type) ? currentResources[type] : 0;
    
    public virtual int GetMaxCapacity() => maxStorageAmount;
    
    public virtual int GetTotalCurrentAmount() => currentResources.Values.Sum();
    
    public virtual Vector3 GetPosition() => transform.position;
    
    public virtual Dictionary<ResourceType, int> GetStoredResources() => currentResources;

    private void NotifyResourceChange(ResourceType type, int addedAmount)
    {
        OnResourceChanged?.Invoke(type, currentResources[type], maxStorageAmount);
        
        if (addedAmount > 0 && ResourceManager.Instance != null)
            ResourceManager.Instance.AddResource(type, addedAmount);
            
        if (_isUIActive) ShowStoredResources();
    }
    
    protected void InvokeResourceChanged(ResourceType type)
    {
        if (currentResources.TryGetValue(type, out var resource))
        {
            OnResourceChanged?.Invoke(type, resource, maxStorageAmount);
        }
    }

    protected void RefreshAllResourcesUI()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            OnResourceChanged?.Invoke(type, GetCurrentResourceAmount(type), GetMaxCapacity());
        }
    }

    private void ShowStoredResources()
    {
        if (GameManager.Instance.uiManager != null)
            GameManager.Instance.uiManager.DisplayStorageInfo(this);
    }

    private void HideStoredResources()
    {
        if (GameManager.Instance.uiManager != null)
            GameManager.Instance.uiManager.HideStorageInfo();
    }
}