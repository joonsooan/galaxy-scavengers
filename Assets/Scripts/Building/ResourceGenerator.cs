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
    
    public float GenerationInterval => generationInterval;
    public int ResourceAmount => resourceAmount;
    public ResourceType ResourceType => resourceType;
    public bool IsConstructed => _isConstructed;

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
            yield return new WaitForSeconds(generationInterval);
            
            GenerateResource();
        }
    }

    private void GenerateResource()
    {
        if (resourceType == ResourceType.Aether)
        {
            FindAndCacheAetherManager();
            
            if (_aetherConsumptionManager != null && _aetherConsumptionManager.IsAetherCapacityFull)
            {
                if (!_wasAetherStorageFull && GameAlertUIManager.Instance != null)
                {
                    GameAlertUIManager.Instance.RegisterAlert(GameAlertType.AetherStorageFull);
                }
                _wasAetherStorageFull = true;
                return;
            }
            
            if (_wasAetherStorageFull && GameAlertUIManager.Instance != null)
            {
                GameAlertUIManager.Instance.UnregisterAlert(GameAlertType.AetherStorageFull);
            }
            _wasAetherStorageFull = false;
        }
        
        ResourceManager.Instance.AddResource(resourceType, resourceAmount);
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