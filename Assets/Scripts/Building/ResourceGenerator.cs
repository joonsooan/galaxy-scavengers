using UnityEngine;
using System.Collections;

public class ResourceGenerator : Damageable
{
    [Header("Values")]
    [SerializeField] private float generationInterval = 5f;
    [SerializeField] private int resourceAmount = 1;
    [SerializeField] private ResourceType resourceType;
    
    private Coroutine _productionCoroutine;
    private bool _isConstructed;
    private AetherConsumptionManager _aetherConsumptionManager;
    
    public float GenerationInterval => generationInterval;
    public int ResourceAmount => resourceAmount;
    public ResourceType ResourceType => resourceType;
    public bool IsConstructed => _isConstructed;

    protected override void OnEnable()
    {
        base.OnEnable();
        
        FindAndCacheAetherManager();
        
        // Only register if constructed and generates Aether
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
        
        // Register with AetherConsumptionManager if this generates Aether
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
        // Don't generate aether if capacity is full
        if (resourceType == ResourceType.Aether)
        {
            if (_aetherConsumptionManager != null && _aetherConsumptionManager.IsAetherCapacityFull)
            {
                return;
            }
        }
        
        ResourceManager.Instance.AddResource(resourceType, resourceAmount);
        ShowResourceText(resourceAmount);
    }
    
    private void ShowResourceText(int amount)
    {
        GameObject textObj = ObjectPooler.Instance.SpawnFromPool(
            "ResourceText", transform.position, Quaternion.identity);

        if (textObj != null)
        {
            FloatingNumText floatingText = textObj.GetComponent<FloatingNumText>();
            if (floatingText != null)
            {
                floatingText.Play($"+{amount}", Color.white);
            }
        }
    }
}