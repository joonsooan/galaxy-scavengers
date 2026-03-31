using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class ElectricityConsumptionManager : MonoBehaviour
{
    public static ElectricityConsumptionManager Instance { get; private set; }

    private readonly List<IElectricityConsumer> _electricityConsumers = new List<IElectricityConsumer>();
    private readonly List<ResourceGenerator> _resourceGenerators = new List<ResourceGenerator>();
    private readonly List<Battery> _batteries = new List<Battery>();
    private readonly List<PowerReceiver> _powerReceivers = new List<PowerReceiver>();
    private readonly Dictionary<IElectricityConsumer, bool> _consumerStates = new Dictionary<IElectricityConsumer, bool>();
    private readonly List<MainStructure> _mainStructures = new List<MainStructure>();

    private AetherTimerManager _timerManager;

    private float TotalElectricityConsumptionPerSecond {
        get {
            float total = 0;
            foreach (IElectricityConsumer consumer in _electricityConsumers) {
                if (consumer != null) {
                    total += consumer.ElectricityConsumptionPerSecond;
                }
            }
            return total;
        }
    }

    public int MaxAetherOreStorageCapacity {
        get {
            int total = 0;
            foreach (MainStructure mainStructure in _mainStructures) {
                if (mainStructure != null) {
                    total += mainStructure.BaseAetherCapacity;
                }
            }

            return total;
        }
    }

    public bool IsAetherOreStorageFull {
        get {
            if (ResourceManager.Instance == null) return false;
            int cap = MaxAetherOreStorageCapacity;
            if (cap <= 0) return false;
            int currentAether = ResourceManager.Instance.GetResourceAmount(ResourceType.Aether);
            return currentAether >= cap;
        }
    }

    public int MaxElectricityStorageCapacity {
        get {
            int total = 0;
            foreach (Battery battery in _batteries) {
                if (battery != null) {
                    total += battery.GetMaxCapacity();
                }
            }

            foreach (ResourceGenerator generator in _resourceGenerators) {
                if (generator != null) {
                    total += generator.ElectricityBufferMax;
                }
            }

            return total;
        }
    }

    public bool IsElectricityStorageFull {
        get {
            int cap = MaxElectricityStorageCapacity;
            if (cap <= 0) return false;
            return GetTotalElectricityAmount() >= cap;
        }
    }

    public int GetTotalElectricityAmount()
    {
        if (ResourceManager.Instance == null) return 0;
        int inStorages = ResourceManager.Instance.GetResourceAmount(ResourceType.Electricity);
        int inBuffers = 0;
        foreach (ResourceGenerator generator in _resourceGenerators) {
            if (generator != null) {
                inBuffers += generator.ElectricityBufferCurrent;
            }
        }

        return inStorages + inBuffers;
    }

    private float ElectricityProductionPerSecond {
        get {
            float total = 0f;
            foreach (ResourceGenerator generator in _resourceGenerators) {
                if (generator != null && generator.IsConstructed) {
                    float productionPerSecond = generator.ResourceAmount / generator.GenerationInterval;
                    total += productionPerSecond;
                }
            }
            return total;
        }
    }

    public float NetElectricityPerSecond {
        get {
            return ElectricityProductionPerSecond - TotalElectricityConsumptionPerSecond;
        }
    }

    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
        }
        else {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(WaitForTimerManager());
    }

    private void OnDisable()
    {
        if (_timerManager != null) {
            AetherTimerManager.OnPowerTick -= OnPowerTick;
        }
    }

    private IEnumerator WaitForTimerManager()
    {
        while (_timerManager == null) {
            _timerManager = FindFirstObjectByType<AetherTimerManager>();
            yield return null;
        }
        AetherTimerManager.OnPowerTick += OnPowerTick;
    }

    public void RegisterConsumer(IElectricityConsumer consumer)
    {
        if (consumer != null && !_electricityConsumers.Contains(consumer)) {
            _electricityConsumers.Add(consumer);
            _consumerStates[consumer] = true;
        }
    }

    public void UnregisterConsumer(IElectricityConsumer consumer)
    {
        if (consumer != null) {
            _electricityConsumers.Remove(consumer);
            _consumerStates.Remove(consumer);
        }
    }

    public void RegisterBattery(Battery battery)
    {
        if (battery != null && !_batteries.Contains(battery)) {
            _batteries.Add(battery);
        }
    }

    public void UnregisterBattery(Battery battery)
    {
        if (battery != null) {
            _batteries.Remove(battery);
        }
    }

    public void RegisterResourceGenerator(ResourceGenerator generator)
    {
        if (generator != null && !_resourceGenerators.Contains(generator)) {
            _resourceGenerators.Add(generator);
        }
    }

    public void UnregisterResourceGenerator(ResourceGenerator generator)
    {
        if (generator != null) {
            _resourceGenerators.Remove(generator);
        }
    }

    public void RegisterPowerReceiver(PowerReceiver receiver)
    {
        if (receiver != null && !_powerReceivers.Contains(receiver)) {
            _powerReceivers.Add(receiver);
        }
    }

    public void UnregisterPowerReceiver(PowerReceiver receiver)
    {
        if (receiver != null) {
            _powerReceivers.Remove(receiver);
        }
    }

    public void RegisterMainStructure(MainStructure mainStructure)
    {
        if (mainStructure != null && !_mainStructures.Contains(mainStructure)) {
            _mainStructures.Add(mainStructure);
        }
    }

    public void UnregisterMainStructure(MainStructure mainStructure)
    {
        if (mainStructure != null) {
            _mainStructures.Remove(mainStructure);
        }
    }

    private void OnPowerTick()
    {
        if (ResourceManager.Instance == null) return;

        foreach (ResourceGenerator generator in _resourceGenerators) {
            generator?.SpillElectricityBufferToNetwork();
        }

        HashSet<Vector3Int> poweredCells = BuildPoweredCellSet();

        for (int i = _electricityConsumers.Count - 1; i >= 0; i--) {
            IElectricityConsumer consumer = _electricityConsumers[i];
            if (consumer == null) {
                _electricityConsumers.RemoveAt(i);
                continue;
            }

            int consumption = consumer.ElectricityConsumptionPerSecond;
            if (consumption <= 0) continue;

            bool powered = IsConsumerFullyPowered(consumer, poweredCells);
            bool wasOperational = _consumerStates.ContainsKey(consumer) && _consumerStates[consumer];

            if (!powered) {
                if (wasOperational) {
                    consumer.OnElectricityUnavailable();
                    _consumerStates[consumer] = false;
                }
                continue;
            }

            int availableElectricity = ResourceManager.Instance.GetResourceAmount(ResourceType.Electricity);
            bool hasEnoughElectricity = availableElectricity >= consumption;

            if (hasEnoughElectricity) {
                ResourceManager.Instance.RemoveResource(ResourceType.Electricity, consumption);

                if (!wasOperational) {
                    consumer.OnElectricityAvailable();
                    _consumerStates[consumer] = true;
                }
            }
            else {
                if (wasOperational) {
                    consumer.OnElectricityUnavailable();
                    _consumerStates[consumer] = false;
                }
            }
        }
    }

    private HashSet<Vector3Int> BuildPoweredCellSet()
    {
        List<IPowerGridNode> nodes = new List<IPowerGridNode>();

        foreach (ResourceGenerator generator in _resourceGenerators) {
            if (generator != null && generator.isActiveAndEnabled) {
                BoundsInt b = generator.GetPowerCoverageBounds();
                if (b.size.x > 0 && b.size.y > 0) {
                    nodes.Add(generator);
                }
            }
        }

        foreach (Battery battery in _batteries) {
            if (battery != null && battery.isActiveAndEnabled) {
                BoundsInt b = battery.GetPowerCoverageBounds();
                if (b.size.x > 0 && b.size.y > 0) {
                    nodes.Add(battery);
                }
            }
        }

        foreach (PowerReceiver receiver in _powerReceivers) {
            if (receiver != null && receiver.isActiveAndEnabled) {
                BoundsInt b = receiver.GetPowerCoverageBounds();
                if (b.size.x > 0 && b.size.y > 0) {
                    nodes.Add(receiver);
                }
            }
        }

        return PowerGridConnectivity.ComputePoweredCells(nodes);
    }

    private static bool IsConsumerFullyPowered(IElectricityConsumer consumer, HashSet<Vector3Int> poweredCells)
    {
        if (poweredCells == null || poweredCells.Count == 0) return false;
        Component component = consumer as Component;
        if (component == null || BuildingManager.Instance == null) return false;

        if (!BuildingManager.Instance.TryGetBuildingAnchorCells(component.transform, out _, out List<Vector3Int> occupied)) {
            return false;
        }

        if (occupied == null || occupied.Count == 0) return false;

        foreach (Vector3Int cell in occupied) {
            if (!poweredCells.Contains(cell)) {
                return false;
            }
        }

        return true;
    }
}
