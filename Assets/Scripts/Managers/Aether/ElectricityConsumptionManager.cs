using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class ElectricityConsumptionManager : MonoBehaviour
{
    public static ElectricityConsumptionManager Instance { get; private set; }

    public bool IsElectricityDemandUnmet { get; private set; }

    public event Action OnAfterElectricityConsumersResolved;

    private readonly List<IElectricityConsumer> _electricityConsumers = new List<IElectricityConsumer>();
    private readonly List<ResourceGenerator> _resourceGenerators = new List<ResourceGenerator>();
    private readonly List<Battery> _batteries = new List<Battery>();
    private readonly List<PowerReceiver> _powerReceivers = new List<PowerReceiver>();
    private readonly Dictionary<IElectricityConsumer, bool> _consumerStates = new Dictionary<IElectricityConsumer, bool>();
    private readonly Dictionary<IElectricityConsumer, PowerFeedVisualState> _consumerVisualStates = new Dictionary<IElectricityConsumer, PowerFeedVisualState>();
    private readonly Dictionary<Battery, PowerFeedVisualState> _batteryVisualStates = new Dictionary<Battery, PowerFeedVisualState>();
    private readonly Dictionary<PowerReceiver, PowerFeedVisualState> _powerReceiverVisualStates = new Dictionary<PowerReceiver, PowerFeedVisualState>();
    private readonly List<IPowerGridNode> _powerNodesBuffer = new List<IPowerGridNode>();
    private readonly List<MainStructure> _mainStructures = new List<MainStructure>();
    private readonly List<IStorage> _electricityWithdrawOrderBuffer = new List<IStorage>();
    private readonly List<PoweredConsumerWork> _poweredConsumerSortBuffer = new List<PoweredConsumerWork>();
    private readonly Dictionary<ResourceGenerator, PowerFeedVisualState> _resourceGeneratorVisualStates = new Dictionary<ResourceGenerator, PowerFeedVisualState>();

    private struct PoweredConsumerWork
    {
        public IElectricityConsumer Consumer;
        public int Consumption;
        public int ComponentId;
        public float DistToSource;
    }

    private AetherTimerManager _timerManager;

    [Header("Power status floating icons (Object UI Canvas)")]
    [SerializeField] private GameObject powerDisconnectedIconPrefab;
    [SerializeField] private GameObject powerInsufficientIconPrefab;
    [SerializeField] private Sprite powerDisconnectedSprite;
    [SerializeField] private Sprite powerInsufficientSprite;
    [SerializeField] private float powerIconWorldYOffset = 0.6f;

    private static Texture2D _fallbackPowerIconTex;
    private static Sprite _fallbackPowerIconSprite;

    private const string PowerIconPoolDisconnected = "PowerIcon_Disconnected";
    private const string PowerIconPoolNeed = "PowerIcon_Need";
    private bool _powerIconPoolsEnsured;

    public float GetTotalElectricityConsumptionPerSecond()
    {
        float total = 0;
        foreach (IElectricityConsumer consumer in _electricityConsumers)
        {
            if (consumer != null)
            {
                total += consumer.ElectricityConsumptionPerSecond;
            }
        }
        return total;
    }

    public float GetEffectiveElectricityProductionPerSecond()
    {
        float total = 0f;
        foreach (ResourceGenerator generator in _resourceGenerators)
        {
            if (generator == null || !generator.IsConstructed)
            {
                continue;
            }

            if (!generator.HasFuelAvailableInRange() || IsElectricityStorageFull)
            {
                continue;
            }

            total += generator.ResourceAmount / generator.GenerationInterval;
        }
        return total;
    }

    public int MaxAetherOreStorageCapacity
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

            return total;
        }
    }

    public bool IsAetherOreStorageFull
    {
        get
        {
            if (ResourceManager.Instance == null) return false;
            int cap = MaxAetherOreStorageCapacity;
            if (cap <= 0) return false;
            int currentAether = ResourceManager.Instance.GetResourceAmount(ResourceType.Aether);
            return currentAether >= cap;
        }
    }

    public int MaxElectricityStorageCapacity
    {
        get
        {
            int total = 0;
            foreach (ResourceGenerator generator in _resourceGenerators)
            {
                if (generator != null)
                {
                    total += generator.ElectricityBufferMax;
                }
            }

            if (ResourceManager.Instance == null)
            {
                return total;
            }

            foreach (IStorage storage in ResourceManager.Instance.GetAllStorages())
            {
                if (storage is not Battery)
                {
                    continue;
                }

                if (storage == null)
                {
                    continue;
                }

                Component c = storage as Component;
                if (c == null || c.gameObject == null || !c.gameObject.activeInHierarchy)
                {
                    continue;
                }

                int elec = storage.GetCurrentResourceAmount(ResourceType.Electricity);
                int room = Mathf.Max(0, storage.GetMaxCapacity() - storage.GetTotalCurrentAmount());
                total += elec + room;
            }

            return total;
        }
    }

    public bool IsElectricityStorageFull
    {
        get
        {
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
        foreach (ResourceGenerator generator in _resourceGenerators)
        {
            if (generator != null)
            {
                inBuffers += generator.ElectricityBufferCurrent;
            }
        }

        return inStorages + inBuffers;
    }

    public float NetElectricityPerSecond => GetEffectiveElectricityProductionPerSecond() - GetTotalElectricityConsumptionPerSecond();

    public PowerFeedVisualState GetConsumerVisualState(IElectricityConsumer consumer)
    {
        if (consumer == null)
        {
            return PowerFeedVisualState.Ok;
        }
        if (_consumerVisualStates.TryGetValue(consumer, out PowerFeedVisualState s))
        {
            return s;
        }
        return PowerFeedVisualState.Disconnected;
    }

    public PowerFeedVisualState GetBatteryVisualState(Battery battery)
    {
        if (battery == null)
        {
            return PowerFeedVisualState.Ok;
        }
        if (_batteryVisualStates.TryGetValue(battery, out PowerFeedVisualState s))
        {
            return s;
        }
        return PowerFeedVisualState.Disconnected;
    }

    public PowerFeedVisualState GetPowerReceiverVisualState(PowerReceiver receiver)
    {
        if (receiver == null)
        {
            return PowerFeedVisualState.Ok;
        }
        if (_powerReceiverVisualStates.TryGetValue(receiver, out PowerFeedVisualState s))
        {
            return s;
        }
        return PowerFeedVisualState.Disconnected;
    }

    public PowerFeedVisualState GetResourceGeneratorVisualState(ResourceGenerator generator)
    {
        if (generator == null)
        {
            return PowerFeedVisualState.Ok;
        }
        if (_resourceGeneratorVisualStates.TryGetValue(generator, out PowerFeedVisualState s))
        {
            return s;
        }
        return PowerFeedVisualState.Disconnected;
    }

    public void FillActivePowerNodesForPreview(List<IPowerGridNode> destination)
    {
        BuildPowerNodeList(destination);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        if (GetComponent<PowerCoveragePreviewOverlay>() == null)
        {
            gameObject.AddComponent<PowerCoveragePreviewOverlay>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(WaitForTimerManager());
    }

    private void OnDisable()
    {
        if (_timerManager != null)
        {
            AetherTimerManager.OnPowerTick -= OnPowerTick;
        }
    }

    private IEnumerator WaitForTimerManager()
    {
        while (_timerManager == null)
        {
            _timerManager = FindFirstObjectByType<AetherTimerManager>();
            yield return null;
        }
        AetherTimerManager.OnPowerTick += OnPowerTick;
    }

    public void RegisterConsumer(IElectricityConsumer consumer)
    {
        if (consumer != null && !_electricityConsumers.Contains(consumer))
        {
            _electricityConsumers.Add(consumer);
            _consumerStates[consumer] = true;
            _consumerVisualStates[consumer] = PowerFeedVisualState.Disconnected;
            if (consumer is Component comp && comp.GetComponent<ElectricityConsumerStatusBillboard>() == null)
            {
                comp.gameObject.AddComponent<ElectricityConsumerStatusBillboard>();
            }
        }
    }

    public void UnregisterConsumer(IElectricityConsumer consumer)
    {
        if (consumer != null)
        {
            _electricityConsumers.Remove(consumer);
            _consumerStates.Remove(consumer);
            _consumerVisualStates.Remove(consumer);
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
            _batteryVisualStates.Remove(battery);
        }
    }

    public void RegisterResourceGenerator(ResourceGenerator generator)
    {
        if (generator != null && !_resourceGenerators.Contains(generator))
        {
            _resourceGenerators.Add(generator);
            _resourceGeneratorVisualStates[generator] = PowerFeedVisualState.Disconnected;
            if (generator.GetComponent<ResourceGeneratorPowerStatusBillboard>() == null)
            {
                generator.gameObject.AddComponent<ResourceGeneratorPowerStatusBillboard>();
            }
        }
    }

    public void UnregisterResourceGenerator(ResourceGenerator generator)
    {
        if (generator != null)
        {
            _resourceGenerators.Remove(generator);
            _resourceGeneratorVisualStates.Remove(generator);
        }
    }

    private void SanitizeDeadResourceGenerators()
    {
        for (int i = _resourceGenerators.Count - 1; i >= 0; i--)
        {
            ResourceGenerator g = _resourceGenerators[i];
            if (g == null)
            {
                _resourceGenerators.RemoveAt(i);
            }
        }

        List<ResourceGenerator> staleVisualKeys = new List<ResourceGenerator>();
        foreach (ResourceGenerator key in _resourceGeneratorVisualStates.Keys)
        {
            if (key == null)
            {
                staleVisualKeys.Add(key);
            }
        }

        foreach (ResourceGenerator key in staleVisualKeys)
        {
            _resourceGeneratorVisualStates.Remove(key);
        }
    }

    public void RegisterPowerReceiver(PowerReceiver receiver)
    {
        if (receiver != null && !_powerReceivers.Contains(receiver))
        {
            _powerReceivers.Add(receiver);
            _powerReceiverVisualStates[receiver] = PowerFeedVisualState.Disconnected;
        }
    }

    public void UnregisterPowerReceiver(PowerReceiver receiver)
    {
        if (receiver != null)
        {
            _powerReceivers.Remove(receiver);
            _powerReceiverVisualStates.Remove(receiver);
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

    private void OnPowerTick()
    {
        if (ResourceManager.Instance == null) return;

        SanitizeDeadResourceGenerators();

        foreach (ResourceGenerator generator in _resourceGenerators)
        {
            if (generator == null)
            {
                continue;
            }

            generator.SpillElectricityBufferToNetwork();
        }

        BuildPowerNodeList(_powerNodesBuffer);
        PowerGridConnectivity.AnalyzePowerGrid(_powerNodesBuffer, out HashSet<Vector3Int> poweredCells,
            out bool[] nodeConnected, out int[] nodeSourcedComponentId, out List<BoundsInt> nodeBounds);

        HashSet<long> poweredCellsXY = BuildPoweredCellsXYKeys(poweredCells);

        _poweredConsumerSortBuffer.Clear();
        for (int i = _electricityConsumers.Count - 1; i >= 0; i--)
        {
            IElectricityConsumer consumer = _electricityConsumers[i];
            if (consumer == null)
            {
                _electricityConsumers.RemoveAt(i);
                continue;
            }

            int consumption = consumer.ElectricityConsumptionPerSecond;
            if (consumption <= 0)
            {
                _consumerVisualStates[consumer] = PowerFeedVisualState.Ok;
                continue;
            }

            bool powered = IsConsumerFullyPowered(consumer, poweredCellsXY);
            bool wasOperational = _consumerStates.ContainsKey(consumer) && _consumerStates[consumer];

            if (!powered)
            {
                _consumerVisualStates[consumer] = PowerFeedVisualState.Disconnected;
                if (wasOperational)
                {
                    consumer.OnElectricityUnavailable();
                    _consumerStates[consumer] = false;
                }
                continue;
            }

            if (!TryFindConsumerPowerComponentId(consumer, _powerNodesBuffer, nodeBounds, nodeSourcedComponentId, poweredCellsXY, out int compId))
            {
                _consumerVisualStates[consumer] = PowerFeedVisualState.Disconnected;
                if (wasOperational)
                {
                    consumer.OnElectricityUnavailable();
                    _consumerStates[consumer] = false;
                }
                continue;
            }

            Component comp = consumer as Component;
            Vector3 consumerWorld = comp != null ? comp.transform.position : Vector3.zero;
            float distToSource = GetMinDistanceToActiveSourceWorld(compId, _powerNodesBuffer, nodeSourcedComponentId, consumerWorld);
            _poweredConsumerSortBuffer.Add(new PoweredConsumerWork
            {
                Consumer = consumer,
                Consumption = consumption,
                ComponentId = compId,
                DistToSource = distToSource
            });
        }

        _poweredConsumerSortBuffer.Sort((a, b) => a.DistToSource.CompareTo(b.DistToSource));

        foreach (PoweredConsumerWork w in _poweredConsumerSortBuffer)
        {
            Vector3 refPos = GetReferenceSourceWorldPositionForComponent(w.ComponentId, _powerNodesBuffer, nodeSourcedComponentId);
            BuildElectricityWithdrawOrderForConsumer(w.ComponentId, refPos, nodeSourcedComponentId);
            int got = ResourceManager.Instance.TryWithdrawElectricityFromStoragesInOrder(w.Consumption, _electricityWithdrawOrderBuffer);
            if (got < w.Consumption)
            {
                got += TryConsumeElectricityFromGeneratorsInComponent(w.ComponentId, w.Consumption - got, nodeSourcedComponentId, _powerNodesBuffer, _resourceGenerators);
            }
            bool ok = got >= w.Consumption;
            bool wasOperational = _consumerStates.ContainsKey(w.Consumer) && _consumerStates[w.Consumer];
            if (ok)
            {
                _consumerVisualStates[w.Consumer] = PowerFeedVisualState.Ok;
                if (!wasOperational)
                {
                    w.Consumer.OnElectricityAvailable();
                    _consumerStates[w.Consumer] = true;
                }
            }
            else
            {
                _consumerVisualStates[w.Consumer] = PowerFeedVisualState.InsufficientPool;
                if (wasOperational)
                {
                    w.Consumer.OnElectricityUnavailable();
                    _consumerStates[w.Consumer] = false;
                }
            }
        }

        foreach (ResourceGenerator gen in _resourceGenerators)
        {
            if (gen == null)
            {
                continue;
            }

            if (!gen.isActiveAndEnabled)
            {
                _resourceGeneratorVisualStates[gen] = PowerFeedVisualState.Ok;
                continue;
            }

            bool canProduce = gen.IsConstructed && gen.HasFuelAvailableInRange() && !IsElectricityStorageFull;
            int buffer = gen.ElectricityBufferCurrent;
            PowerFeedVisualState gvs;
            if (canProduce)
            {
                gvs = PowerFeedVisualState.Ok;
            }
            else if (buffer > 0)
            {
                gvs = PowerFeedVisualState.InsufficientPool;
            }
            else
            {
                gvs = PowerFeedVisualState.Disconnected;
            }
            _resourceGeneratorVisualStates[gen] = gvs;
        }

        foreach (Battery battery in _batteries)
        {
            if (battery == null || !battery.isActiveAndEnabled)
            {
                continue;
            }
            int idx = _powerNodesBuffer.IndexOf(battery);
            PowerFeedVisualState vs;
            if (idx < 0 || !nodeConnected[idx])
            {
                vs = PowerFeedVisualState.Disconnected;
            }
            else if (battery.GetCurrentResourceAmount(ResourceType.Electricity) <= 0)
            {
                vs = PowerFeedVisualState.InsufficientPool;
            }
            else
            {
                vs = PowerFeedVisualState.Ok;
            }
            _batteryVisualStates[battery] = vs;
        }

        foreach (PowerReceiver receiver in _powerReceivers)
        {
            if (receiver == null || !receiver.isActiveAndEnabled)
            {
                continue;
            }
            int idx = _powerNodesBuffer.IndexOf(receiver);
            PowerFeedVisualState vs;
            if (idx < 0 || !nodeConnected[idx])
            {
                vs = PowerFeedVisualState.Disconnected;
            }
            else if (GetTotalElectricityAmount() <= 0)
            {
                vs = PowerFeedVisualState.InsufficientPool;
            }
            else
            {
                vs = PowerFeedVisualState.Ok;
            }
            _powerReceiverVisualStates[receiver] = vs;
        }

        bool unmet = false;
        foreach (IElectricityConsumer consumer in _electricityConsumers)
        {
            if (consumer == null)
            {
                continue;
            }

            if (consumer.ElectricityConsumptionPerSecond <= 0)
            {
                continue;
            }

            if (_consumerVisualStates.TryGetValue(consumer, out PowerFeedVisualState vs) &&
                vs == PowerFeedVisualState.InsufficientPool)
            {
                unmet = true;
                break;
            }
        }

        IsElectricityDemandUnmet = unmet;
        OnAfterElectricityConsumersResolved?.Invoke();
    }

    private static HashSet<long> BuildPoweredCellsXYKeys(HashSet<Vector3Int> poweredCells)
    {
        HashSet<long> keys = new HashSet<long>();
        if (poweredCells == null || poweredCells.Count == 0)
        {
            return keys;
        }
        foreach (Vector3Int p in poweredCells)
        {
            keys.Add(PackCellXY(p.x, p.y));
        }
        return keys;
    }

    private static long PackCellXY(int x, int y)
    {
        unchecked
        {
            return ((long)(uint)x << 32) | (uint)y;
        }
    }

    private static bool BoundsContainsCellXY(BoundsInt b, Vector3Int cell)
    {
        return cell.x >= b.xMin && cell.x < b.xMax && cell.y >= b.yMin && cell.y < b.yMax;
    }

    private void BuildPowerNodeList(List<IPowerGridNode> buffer)
    {
        buffer.Clear();

        foreach (ResourceGenerator generator in _resourceGenerators)
        {
            if (generator != null && generator.isActiveAndEnabled)
            {
                BoundsInt b = generator.GetPowerCoverageBounds();
                if (b.size.x > 0 && b.size.y > 0)
                {
                    buffer.Add(generator);
                }
            }
        }

        foreach (Battery battery in _batteries)
        {
            if (battery != null && battery.isActiveAndEnabled)
            {
                BoundsInt b = battery.GetPowerCoverageBounds();
                if (b.size.x > 0 && b.size.y > 0)
                {
                    buffer.Add(battery);
                }
            }
        }

        foreach (PowerReceiver receiver in _powerReceivers)
        {
            if (receiver != null && receiver.isActiveAndEnabled)
            {
                BoundsInt b = receiver.GetPowerCoverageBounds();
                if (b.size.x > 0 && b.size.y > 0)
                {
                    buffer.Add(receiver);
                }
            }
        }
    }

    private static bool IsConsumerFullyPowered(IElectricityConsumer consumer, HashSet<long> poweredCellsXY)
    {
        if (poweredCellsXY == null || poweredCellsXY.Count == 0) return false;
        Component component = consumer as Component;
        if (component == null || BuildingManager.Instance == null) return false;

        if (!BuildingManager.Instance.TryGetBuildingAnchorCells(component.transform, out _, out List<Vector3Int> occupied))
        {
            return false;
        }

        if (occupied == null || occupied.Count == 0) return false;

        foreach (Vector3Int cell in occupied)
        {
            if (poweredCellsXY.Contains(PackCellXY(cell.x, cell.y)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindConsumerPowerComponentId(IElectricityConsumer consumer, List<IPowerGridNode> nodes, List<BoundsInt> nodeBounds, int[] nodeSourcedComponentId, HashSet<long> poweredCellsXY, out int componentId)
    {
        componentId = -1;
        if (nodes == null || nodeBounds == null || nodeSourcedComponentId == null || poweredCellsXY == null || poweredCellsXY.Count == 0)
        {
            return false;
        }

        Component comp = consumer as Component;
        if (comp == null || BuildingManager.Instance == null)
        {
            return false;
        }

        if (!BuildingManager.Instance.TryGetBuildingAnchorCells(comp.transform, out _, out List<Vector3Int> occupied))
        {
            return false;
        }

        if (occupied == null || occupied.Count == 0)
        {
            return false;
        }

        foreach (Vector3Int cell in occupied)
        {
            if (!poweredCellsXY.Contains(PackCellXY(cell.x, cell.y)))
            {
                continue;
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodeSourcedComponentId[i] < 0)
                {
                    continue;
                }

                BoundsInt b = nodeBounds[i];
                if (b.size.x <= 0 || b.size.y <= 0)
                {
                    continue;
                }

                if (!BoundsContainsCellXY(b, cell))
                {
                    continue;
                }

                componentId = nodeSourcedComponentId[i];
                return true;
            }
        }

        return false;
    }

    private static float GetMinDistanceToActiveSourceWorld(int componentId, List<IPowerGridNode> nodes, int[] nodeSourcedComponentId, Vector3 consumerWorldPos)
    {
        float best = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodeSourcedComponentId[i] != componentId)
            {
                continue;
            }

            IPowerGridNode node = nodes[i];
            if (!node.IsActivePowerSource())
            {
                continue;
            }

            Component c = node as Component;
            if (c == null)
            {
                continue;
            }

            float d = Vector3.Distance(consumerWorldPos, c.transform.position);
            if (d < best)
            {
                best = d;
            }
        }

        return best < float.MaxValue ? best : 0f;
    }

    private static Vector3 GetReferenceSourceWorldPositionForComponent(int componentId, List<IPowerGridNode> nodes, int[] nodeSourcedComponentId)
    {
        Vector3 sumG = Vector3.zero;
        int nG = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodeSourcedComponentId[i] != componentId)
            {
                continue;
            }

            if (nodes[i] is ResourceGenerator rg && rg.isActiveAndEnabled)
            {
                Component c = rg as Component;
                if (c != null)
                {
                    sumG += c.transform.position;
                    nG++;
                }
            }
        }

        if (nG > 0)
        {
            return sumG / nG;
        }

        Vector3 sumB = Vector3.zero;
        int nB = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodeSourcedComponentId[i] != componentId)
            {
                continue;
            }

            if (nodes[i] is Battery bat && bat.GetCurrentResourceAmount(ResourceType.Electricity) > 0)
            {
                Component c = bat as Component;
                if (c != null)
                {
                    sumB += c.transform.position;
                    nB++;
                }
            }
        }

        if (nB > 0)
        {
            return sumB / nB;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodeSourcedComponentId[i] != componentId)
            {
                continue;
            }

            if (nodes[i] is ResourceGenerator rg2)
            {
                Component c = rg2 as Component;
                return c != null ? c.transform.position : Vector3.zero;
            }
        }

        return Vector3.zero;
    }

    private static int TryConsumeElectricityFromGeneratorsInComponent(int componentId, int need, int[] nodeSourcedComponentId, List<IPowerGridNode> nodes, List<ResourceGenerator> generators)
    {
        if (need <= 0 || nodeSourcedComponentId == null || nodes == null || generators == null)
        {
            return 0;
        }
        int total = 0;
        for (int gi = 0; gi < generators.Count; gi++)
        {
            ResourceGenerator gen = generators[gi];
            if (gen == null || !gen.isActiveAndEnabled)
            {
                continue;
            }
            int nodeIndex = -1;
            for (int j = 0; j < nodes.Count; j++)
            {
                if (ReferenceEquals(nodes[j], gen))
                {
                    nodeIndex = j;
                    break;
                }
            }
            if (nodeIndex < 0 || nodeSourcedComponentId[nodeIndex] != componentId)
            {
                continue;
            }
            total += gen.TryConsumeBufferForPowerDelivery(need - total);
            if (total >= need)
            {
                break;
            }
        }
        return total;
    }

    private void BuildElectricityWithdrawOrderForConsumer(int componentId, Vector3 refWorldPos, int[] nodeSourcedComponentId)
    {
        _electricityWithdrawOrderBuffer.Clear();
        List<(Battery b, float d)> inComp = new List<(Battery, float)>();
        List<(Battery b, float d)> outComp = new List<(Battery, float)>();

        foreach (Battery bat in _batteries)
        {
            if (bat == null || !bat.isActiveAndEnabled)
            {
                continue;
            }

            if (bat.GetCurrentResourceAmount(ResourceType.Electricity) <= 0)
            {
                continue;
            }

            int nodeIndex = -1;
            for (int j = 0; j < _powerNodesBuffer.Count; j++)
            {
                if (ReferenceEquals(_powerNodesBuffer[j], bat))
                {
                    nodeIndex = j;
                    break;
                }
            }

            int batComp = nodeIndex >= 0 ? nodeSourcedComponentId[nodeIndex] : -1;
            float d = Vector3.Distance(bat.GetPosition(), refWorldPos);
            if (batComp == componentId)
            {
                inComp.Add((bat, d));
            }
            else
            {
                outComp.Add((bat, d));
            }
        }

        inComp.Sort((a, b) => a.d.CompareTo(b.d));
        outComp.Sort((a, b) => a.d.CompareTo(b.d));
        for (int i = 0; i < inComp.Count; i++)
        {
            _electricityWithdrawOrderBuffer.Add(inComp[i].b);
        }
        for (int i = 0; i < outComp.Count; i++)
        {
            _electricityWithdrawOrderBuffer.Add(outComp[i].b);
        }

        MainStructure mainStructure = ResourceDataManager.Instance != null ? ResourceDataManager.Instance.GetMainStructure() : null;
        if (mainStructure != null && mainStructure.GetCurrentResourceAmount(ResourceType.Electricity) > 0)
        {
            _electricityWithdrawOrderBuffer.Add(mainStructure);
        }
    }

    public void ReleasePowerFloatingIcon(PowerStatusWorldFollower follower)
    {
        if (follower == null)
        {
            return;
        }

        follower.ClearFollowTarget();
        string tag = follower.PoolReturnTag;
        if (!string.IsNullOrEmpty(tag) && ObjectPooler.Instance != null)
        {
            ObjectPooler.Instance.ReturnUIPooled(tag, follower.gameObject);
        }
        else
        {
            Destroy(follower.gameObject);
        }
    }

    private void TryEnsurePowerIconPools()
    {
        if (_powerIconPoolsEnsured || ObjectPooler.Instance == null)
        {
            return;
        }

        if (powerDisconnectedIconPrefab != null)
        {
            ObjectPooler.Instance.EnsureUIPool(PowerIconPoolDisconnected, powerDisconnectedIconPrefab, 16);
        }

        if (powerInsufficientIconPrefab != null)
        {
            ObjectPooler.Instance.EnsureUIPool(PowerIconPoolNeed, powerInsufficientIconPrefab, 16);
        }

        _powerIconPoolsEnsured = true;
    }

    public PowerStatusWorldFollower SpawnPowerFloatingIcon(PowerFeedVisualState state, Transform followTarget)
    {
        if (state != PowerFeedVisualState.Disconnected && state != PowerFeedVisualState.InsufficientPool)
        {
            return null;
        }
        bool disconnected = state == PowerFeedVisualState.Disconnected;
        Canvas canvas = GameManager.Instance?.uiManager?.GetObjectUICanvas();
        if (canvas == null)
        {
            return null;
        }

        TryEnsurePowerIconPools();

        GameObject prefab = disconnected ? powerDisconnectedIconPrefab : powerInsufficientIconPrefab;
        Sprite sprite = disconnected ? powerDisconnectedSprite : powerInsufficientSprite;
        string poolTag = disconnected ? PowerIconPoolDisconnected : PowerIconPoolNeed;

        GameObject go = null;
        string usePoolTag = null;
        if (prefab != null && ObjectPooler.Instance != null && ObjectPooler.Instance.HasUIPool(poolTag))
        {
            go = ObjectPooler.Instance.SpawnUIPooled(poolTag, canvas.transform);
            if (go != null)
            {
                usePoolTag = poolTag;
            }
        }

        if (go == null && prefab != null)
        {
            go = Instantiate(prefab, canvas.transform, false);
        }

        if (go == null)
        {
            bool usedFallback = false;
            if (sprite == null)
            {
                sprite = EnsureFallbackPowerIconSprite();
                usedFallback = true;
            }
            go = new GameObject(disconnected ? "PowerDisconnectedIconUI" : "PowerInsufficientIconUI");
            go.layer = canvas.gameObject.layer;
            RectTransform rtInit = go.AddComponent<RectTransform>();
            rtInit.SetParent(canvas.transform, false);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.color = usedFallback
                ? (disconnected ? new Color(0.95f, 0.25f, 0.2f, 0.95f) : new Color(0.95f, 0.65f, 0.15f, 0.95f))
                : Color.white;
            rtInit.sizeDelta = sprite != null ? sprite.rect.size : new Vector2(8f, 8f);
        }

        PowerStatusWorldFollower follower = go.GetComponent<PowerStatusWorldFollower>();
        if (follower == null)
        {
            follower = go.AddComponent<PowerStatusWorldFollower>();
        }
        follower.Initialize(followTarget, Vector3.up * powerIconWorldYOffset, usePoolTag);
        return follower;
    }

    private static Sprite EnsureFallbackPowerIconSprite()
    {
        if (_fallbackPowerIconSprite != null)
        {
            return _fallbackPowerIconSprite;
        }
        _fallbackPowerIconTex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                _fallbackPowerIconTex.SetPixel(x, y, Color.white);
            }
        }
        _fallbackPowerIconTex.Apply();
        _fallbackPowerIconTex.hideFlags = HideFlags.HideAndDontSave;
        _fallbackPowerIconSprite = Sprite.Create(_fallbackPowerIconTex, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
        return _fallbackPowerIconSprite;
    }
}
