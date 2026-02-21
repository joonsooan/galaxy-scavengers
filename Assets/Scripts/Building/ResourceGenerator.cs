using UnityEngine;
using System.Collections;

public class ResourceGenerator : Damageable
{
    [Header("Values")]
    [SerializeField] private float generationInterval = 5f;
    [SerializeField] private int resourceAmount = 1;
    [SerializeField] private ResourceType resourceType;
    
    [Header("VFX")]
    [SerializeField] private Vector3 resourceImageOffset = new (0f, 0.5f, 0f);
    
    private Coroutine _productionCoroutine;
    private bool _isConstructed;
    private AetherConsumptionManager _aetherConsumptionManager;
    private bool _wasAetherStorageFull;
    private WaitForSeconds _generationIntervalWait;
    
    public float GenerationInterval => generationInterval;
    public int ResourceAmount => resourceAmount;
    public ResourceType ResourceType => resourceType;
    public bool IsConstructed => _isConstructed;

    protected override void Awake()
    {
        base.Awake();
        _generationIntervalWait = CoroutineCache.GetWaitForSeconds(generationInterval);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        if (!BuildingManager.IsBuildingProperlyPlaced(transform))
        {
            return;
        }
        
        FindAndCacheAetherManager();
        
        if (_aetherConsumptionManager != null && resourceType == ResourceType.Aether && _isConstructed)
        {
            _aetherConsumptionManager.RegisterResourceGenerator(this);
        }
        
        ActivateComboCard();
    }
    
    protected override void OnDisable()
    {
        if (_aetherConsumptionManager != null && resourceType == ResourceType.Aether)
        {
            _aetherConsumptionManager.UnregisterResourceGenerator(this);
        }
        
        base.OnDisable();
    }
    
    private void FindAndCacheAetherManager()
    {
        if (_aetherConsumptionManager == null)
        {
            _aetherConsumptionManager = FindFirstObjectByType<AetherConsumptionManager>();
        }
    }

    private void ActivateComboCard()
    {
        if (!_isConstructed)
        {
            return;
        }
        
        if (_productionCoroutine != null)
        {
            StopCoroutine(_productionCoroutine);
        }
        _productionCoroutine = StartCoroutine(ProduceResource());
    }
    
    public void SetConstructed()
    {
        _isConstructed = true;
        
        if (!BuildingManager.IsBuildingProperlyPlaced(transform))
        {
            ActivateComboCard();
            return;
        }
        
        if (resourceType == ResourceType.Aether)
        {
            FindAndCacheAetherManager();
            if (_aetherConsumptionManager != null)
            {
                _aetherConsumptionManager.RegisterResourceGenerator(this);
            }
        }
        
        ActivateComboCard();
    }

    private IEnumerator ProduceResource()
    {
        while (true)
        {
            yield return _generationIntervalWait;
            
            GenerateResource();
        }
    }

    private void GenerateResource()
    {
        var alertManager = FindFirstObjectByType<GameAlertUIManager>();
        if (resourceType == ResourceType.Aether)
        {
            FindAndCacheAetherManager();
            
            if (_aetherConsumptionManager != null && _aetherConsumptionManager.IsAetherCapacityFull)
            {
                if (!_wasAetherStorageFull && alertManager != null)
                {
                    alertManager.RegisterAlert(GameAlertType.AetherStorageFull, this);
                }
                _wasAetherStorageFull = true;
                return;
            }
        }
        
        ResourceManager.Instance.AddResource(resourceType, resourceAmount);

        if (resourceType == ResourceType.Aether && _aetherConsumptionManager != null)
        {
            bool isNowFull = _aetherConsumptionManager.IsAetherCapacityFull;
            if (isNowFull && !_wasAetherStorageFull && alertManager != null)
            {
                alertManager.RegisterAlert(GameAlertType.AetherStorageFull, this);
            }
            else if (!isNowFull && _wasAetherStorageFull && alertManager != null)
            {
                alertManager.UnregisterAlert(GameAlertType.AetherStorageFull, this);
            }
            _wasAetherStorageFull = isNowFull;
        }

        ShowResourceImage();
    }
    
    private void ShowResourceImage()
    {
        GameObject imageObj = ObjectPooler.Instance.SpawnFromPool(
            "ResourceImage", transform.position + resourceImageOffset, Quaternion.identity);

        if (imageObj != null)
        {
            FloatingResourceImage floatingImage = imageObj.GetComponent<FloatingResourceImage>();
            if (floatingImage != null)
            {
                floatingImage.Play(resourceType);
            }
        }
    }
}