using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AetherConsumptionManager : MonoBehaviour
{
    private readonly List<IAetherConsumer> _aetherConsumers = new ();
    private readonly Dictionary<IAetherConsumer, bool> _consumerStates = new ();
    private readonly List<Battery> _batteries = new ();
    private readonly List<ResourceGenerator> _aetherGenerators = new ();
    private readonly List<MainStructure> _mainStructures = new ();
    private readonly List<Storage> _storages = new ();
    
    private AetherTimerManager _timerManager;

    private float TotalAetherConsumptionPerSecond
    {
        get
        {
            float total = 0;
            foreach (IAetherConsumer consumer in _aetherConsumers)
            {
                if (consumer != null)
                {
                    total += consumer.AetherConsumptionPerSecond;
                }
            }
            return total;
        }
    }
    
    public int MaxAetherCapacity
    {
        get
        {
            int total = 0;
            
            foreach (MainStructure mainStructure in _mainStructures)
            {
                if (mainStructure != null)
                {
                    total += mainStructure.BaseAetherCapacity;
                }
            }
            
            foreach (Storage storage in _storages)
            {
                if (storage != null)
                {
                    total += storage.AetherCapacity;
                }
            }
            
            foreach (Battery battery in _batteries)
            {
                if (battery != null)
                {
                    total += battery.AetherCapacity;
                }
            }
            
            return total;
        }
    }
    
    public bool IsAetherCapacityFull
    {
        get
        {
            if (ResourceManager.Instance == null) return false;
            int currentAether = ResourceManager.Instance.GetResourceAmount(ResourceType.Aether);
            return currentAether >= MaxAetherCapacity;
        }
    }

    private float AetherProductionPerSecond
    {
        get
        {
            float total = 0f;
            foreach (ResourceGenerator generator in _aetherGenerators)
            {
                if (generator != null && generator.IsConstructed && generator.ResourceType == ResourceType.Aether)
                {
                    float productionPerSecond = generator.ResourceAmount / generator.GenerationInterval;
                    total += productionPerSecond;
                }
            }
            return Mathf.RoundToInt(total);
        }
    }
    
    public float NetAetherPerSecond => AetherProductionPerSecond - TotalAetherConsumptionPerSecond;
    
    private void OnEnable()
    {
        StartCoroutine(WaitForTimerManager());
    }
    
    private void OnDisable()
    {
        if (_timerManager != null)
        {
            AetherTimerManager.OnAetherTick -= OnAetherTick;
        }
    }
    
    private IEnumerator WaitForTimerManager()
    {
        while (_timerManager == null)
        {
            _timerManager = FindFirstObjectByType<AetherTimerManager>();
            yield return null;
        }
        AetherTimerManager.OnAetherTick += OnAetherTick;
    }
    
    public void RegisterConsumer(IAetherConsumer consumer)
    {
        if (consumer != null && !_aetherConsumers.Contains(consumer))
        {
            _aetherConsumers.Add(consumer);
            _consumerStates[consumer] = true;
        }
    }
    
    public void UnregisterConsumer(IAetherConsumer consumer)
    {
        if (consumer != null)
        {
            _aetherConsumers.Remove(consumer);
            _consumerStates.Remove(consumer);
        }
    }
    
    public void RegisterBattery(Battery battery)
    {
        if (battery != null && !_batteries.Contains(battery))
        {
            _batteries.Add(battery);
        }
    }
    
    public void UnregisterBattery(Battery battery)
    {
        if (battery != null)
        {
            _batteries.Remove(battery);
        }
    }
    
    public void RegisterResourceGenerator(ResourceGenerator generator)
    {
        if (generator != null && !_aetherGenerators.Contains(generator))
        {
            _aetherGenerators.Add(generator);
        }
    }
    
    public void UnregisterResourceGenerator(ResourceGenerator generator)
    {
        if (generator != null)
        {
            _aetherGenerators.Remove(generator);
        }
    }
    
    public void RegisterMainStructure(MainStructure mainStructure)
    {
        if (mainStructure != null && !_mainStructures.Contains(mainStructure))
        {
            _mainStructures.Add(mainStructure);
        }
    }
    
    public void UnregisterMainStructure(MainStructure mainStructure)
    {
        if (mainStructure != null)
        {
            _mainStructures.Remove(mainStructure);
        }
    }
    
    public void RegisterStorage(Storage storage)
    {
        if (storage != null && !_storages.Contains(storage))
        {
            _storages.Add(storage);
        }
    }
    
    public void UnregisterStorage(Storage storage)
    {
        if (storage != null)
        {
            _storages.Remove(storage);
        }
    }
    
    private void OnAetherTick()
    {
        if (ResourceManager.Instance == null) return;
        
        for (int i = _aetherConsumers.Count - 1; i >= 0; i--)
        {
            IAetherConsumer consumer = _aetherConsumers[i];
            if (consumer == null)
            {
                _aetherConsumers.RemoveAt(i);
                continue;
            }
            
            int consumption = consumer.AetherConsumptionPerSecond;
            if (consumption <= 0) continue;
            
            int availableAether = ResourceManager.Instance.GetResourceAmount(ResourceType.Aether);
            bool hasEnoughAether = availableAether >= consumption;
            
            bool wasOperational = _consumerStates.ContainsKey(consumer) && _consumerStates[consumer];
            
            if (hasEnoughAether)
            {
                ResourceManager.Instance.RemoveResource(ResourceType.Aether, consumption);
                
                if (!wasOperational)
                {
                    consumer.OnAetherAvailable();
                    _consumerStates[consumer] = true;
                }
            }
            else
            {
                if (wasOperational)
                {
                    consumer.OnAetherUnavailable();
                    _consumerStates[consumer] = false;
                }
            }
        }
    }
}
