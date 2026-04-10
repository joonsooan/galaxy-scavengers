using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainStructure : BaseStorage, IClickable, IElectricityConsumer
{
    [Header("Drone production panel text")]
    [SerializeField] private string droneProduceDisplayName;
    [SerializeField] [TextArea(3, 10)] private string droneProduceDescription;

    [Header("Production Settings")]
    [SerializeField] private List<UnitData> producibleUnits;

    [Header("Electricity consumption (drone production)")]
    [SerializeField] private int electricityConsumptionPerSecond = 1;

    [Header("Production Progress UI")]
    [SerializeField] private ProductionProgressSlider productionSlider;

    [Header("Aether Capacity")]
    [SerializeField] private int baseAetherCapacity = 100;

    private readonly Dictionary<int, int> _producedUnitCounts = new();
    private readonly Queue<UnitData> _productionQueue = new();
    private readonly Dictionary<int, int> _targetUnitCounts = new();

    private ElectricityConsumptionManager _electricityConsumptionManager;
    private UnitData _currentProducingUnit;

    private float _currentProductionTime;
    private float _productionStartTime;
    private bool _isManualQueueProcessing;
    private bool _isProducing;

    private Coroutine _productionCoroutine;
    private WaitForSeconds _productionWaitWait;
    private bool _consumerRegistered;

    public int BaseAetherCapacity => baseAetherCapacity;

    public string DroneProduceDisplayName => droneProduceDisplayName;

    public string DroneProduceDescription => droneProduceDescription;

    public List<UnitData> ProducibleUnits => producibleUnits;

    public int ElectricityConsumptionPerSecond => electricityConsumptionPerSecond;

    public bool IsOperational { get; private set; } = true;

    public static event Action<MainStructure> OnDroneProducePanelClicked;

    public static event Action<int, int, int> OnUnitTargetChanged;

    public static event Action<UnitData> OnUnitProduced;

    protected override void Awake()
    {
        base.Awake();
        _productionWaitWait = CoroutineCache.GetWaitForSeconds(1f);
    }

    protected override void Start()
    {
        base.Start();

        if (productionSlider != null) {
            productionSlider.Initialize(transform);
        }

        if (IsProperlyPlacedBuilding()) {
            FindAndCacheElectricityManager();
            if (_electricityConsumptionManager != null) {
                _electricityConsumptionManager.RegisterMainStructure(this);
                TryRegisterElectricityConsumer();
            }

            if (TargetManager.Instance != null) {
                TargetManager.Instance.RegisterTarget(this);
            }
        }

        if (GetComponent<BuildingHoverTrigger>() == null) {
            gameObject.AddComponent<BuildingHoverTrigger>();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!IsProperlyPlacedBuilding()) {
            return;
        }

        FindAndCacheElectricityManager();
        if (_electricityConsumptionManager != null) {
            TryRegisterElectricityConsumer();
        }

        ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;
    }

    protected override void OnDisable()
    {
        ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;

        if (_electricityConsumptionManager != null) {
            if (_consumerRegistered) {
                _electricityConsumptionManager.UnregisterConsumer(this);
                _consumerRegistered = false;
            }
            _electricityConsumptionManager.UnregisterMainStructure(this);
        }

        if (productionSlider != null) {
            productionSlider.gameObject.SetActive(false);
        }

        base.OnDisable();
    }

    private void TryRegisterElectricityConsumer()
    {
        if (_electricityConsumptionManager == null || electricityConsumptionPerSecond <= 0 || _consumerRegistered) {
            return;
        }

        _electricityConsumptionManager.RegisterConsumer(this);
        _consumerRegistered = true;
    }

    private bool IsProperlyPlacedBuilding()
    {
        if (BuildingManager.Instance == null) {
            return true;
        }

        return BuildingManager.IsBuildingProperlyPlaced(transform);
    }

    private void FindAndCacheElectricityManager()
    {
        if (_electricityConsumptionManager == null) {
            _electricityConsumptionManager = ElectricityConsumptionManager.Instance;
        }
    }

    public void OnClicked()
    {
        OnDroneProducePanelClicked?.Invoke(this);
    }

    public void OnElectricityUnavailable()
    {
        if (IsOperational) {
            IsOperational = false;
            StopProduction();
        }
    }

    public void OnElectricityAvailable()
    {
        if (!IsOperational) {
            IsOperational = true;
            if (!_isProducing) {
                if (_productionQueue.Count > 0) {
                    _productionCoroutine = StartCoroutine(ProcessProductionQueue());
                }
                else if (HasPendingTargets()) {
                    UpdateQueueFromTargets();
                    if (_productionQueue.Count > 0) {
                        _productionCoroutine = StartCoroutine(ProcessProductionQueue());
                    }
                }
            }
        }
    }

    private float GetProductionSpeedMultiplier()
    {
        if (CoreRepairManager.Instance != null && !CoreRepairManager.Instance.IsPartRepaired(CorePart.Repeater)) {
            CorePartData repeaterData = CoreRepairManager.Instance.GetPartData(CorePart.Repeater);
            if (repeaterData != null) {
                return 1f - repeaterData.debuffValue;
            }
        }

        return 1f;
    }

    private void StopProduction()
    {
        if (_isProducing && _productionCoroutine != null) {
            StopCoroutine(_productionCoroutine);
            _productionCoroutine = null;
            _isProducing = false;
        }
    }

    private void OnResourceAmountChanged(ResourceType type, int amount)
    {
        if (_isManualQueueProcessing) {
            return;
        }

        if (!_isProducing && HasPendingTargets() && IsOperational) {
            UpdateQueueFromTargets();
            if (_productionQueue.Count > 0) {
                _productionCoroutine = StartCoroutine(ProcessProductionQueue());
            }
        }
    }

    public int GetCurrentUnitCount(int unitIndex)
    {
        if (producibleUnits != null &&
            unitIndex >= 0 &&
            unitIndex < producibleUnits.Count &&
            UnitManager.Instance != null &&
            UnitManager.Instance.AllyUnits != null)
        {
            UnitData targetUnitData = producibleUnits[unitIndex];
            int count = 0;
            IReadOnlyList<UnitBase> allyUnits = UnitManager.Instance.AllyUnits;
            for (int i = 0; i < allyUnits.Count; i++) {
                UnitBase unit = allyUnits[i];
                if (unit == null) {
                    continue;
                }

                if (unit.unitData == targetUnitData) {
                    count++;
                }
            }

            return count;
        }

        _producedUnitCounts.TryGetValue(unitIndex, out int fallbackCount);
        return fallbackCount;
    }

    public int GetTargetUnitCount(int unitIndex)
    {
        _targetUnitCounts.TryGetValue(unitIndex, out int count);
        return count;
    }

    private int GetTotalActiveCount(int unitIndex)
    {
        if (producibleUnits == null) {
            return 0;
        }

        int count = CountUnitsInQueue(unitIndex);

        if (_currentProducingUnit != null && _currentProducingUnit == producibleUnits[unitIndex]) {
            count++;
        }

        return count;
    }

    public void SetTargetUnitCount(int unitIndex, int targetCount)
    {
        if (producibleUnits == null ||
            unitIndex < 0 || unitIndex >= producibleUnits.Count) {
            return;
        }

        int currentProduced = GetCurrentUnitCount(unitIndex);
        _targetUnitCounts[unitIndex] = Mathf.Max(0, targetCount);

        int neededInTotal = targetCount - currentProduced;
        int currentlyActive = GetTotalActiveCount(unitIndex);

        if (neededInTotal > currentlyActive) {
            int unitsToAdd = neededInTotal - currentlyActive;
            AddUnitsToQueue(unitIndex, unitsToAdd);
        }
        else if (neededInTotal < currentlyActive) {
            int unitsToRemove = currentlyActive - neededInTotal;
            RemoveUnitsFromQueue(unitIndex, unitsToRemove);
        }

        OnUnitTargetChanged?.Invoke(unitIndex, currentProduced, targetCount);

        if (!_isProducing && _productionQueue.Count > 0 && IsOperational) {
            _productionCoroutine = StartCoroutine(ProcessProductionQueue());
        }
    }

    private void AddUnitsToQueue(int unitIndex, int count)
    {
        if (producibleUnits == null ||
            unitIndex < 0 || unitIndex >= producibleUnits.Count) {
            return;
        }

        UnitData unitData = producibleUnits[unitIndex];
        _isManualQueueProcessing = true;

        try {
            for (int i = 0; i < count; i++) {
                if (ResourceManager.Instance.SpendResources(unitData.productionCosts)) {
                    _productionQueue.Enqueue(unitData);
                }
                else {
                    Debug.Log($"Can't produce unit {unitData.unitName}: insufficient resources");
                    break;
                }
            }
        }
        finally {
            _isManualQueueProcessing = false;
        }
    }

    private void RemoveUnitsFromQueue(int unitIndex, int count)
    {
        if (producibleUnits == null ||
            unitIndex < 0 || unitIndex >= producibleUnits.Count) {
            return;
        }

        UnitData targetUnit = producibleUnits[unitIndex];
        int removed = 0;

        List<UnitData> queueList = new List<UnitData>(_productionQueue);
        _productionQueue.Clear();

        for (int i = queueList.Count - 1; i >= 0 && removed < count; i--) {
            if (queueList[i] == targetUnit) {
                if (targetUnit.productionCosts != null) {
                    foreach (ResourceCost cost in targetUnit.productionCosts) {
                        ResourceManager.Instance.AddResource(cost.resourceType, cost.amount);
                    }
                }

                queueList.RemoveAt(i);
                removed++;
            }
        }

        foreach (UnitData unit in queueList) {
            _productionQueue.Enqueue(unit);
        }
    }

    private IEnumerator ProcessProductionQueue()
    {
        _isProducing = true;

        while (_productionQueue.Count > 0 || HasPendingTargets()) {
            if (!IsOperational) {
                _isProducing = false;
                if (productionSlider != null) {
                    productionSlider.gameObject.SetActive(false);
                }

                yield break;
            }

            if (UnitManager.Instance != null && !UnitManager.Instance.CanSpawnUnit()) {
                if (productionSlider != null) {
                    productionSlider.gameObject.SetActive(false);
                }

                _isProducing = false;
                yield break;
            }

            if (_productionQueue.Count == 0) {
                UpdateQueueFromTargets();
            }

            if (_productionQueue.Count == 0) {
                if (productionSlider != null) {
                    productionSlider.gameObject.SetActive(false);
                }

                if (!HasPendingTargets()) {
                    _isProducing = false;
                    yield break;
                }

                yield return _productionWaitWait;
                continue;
            }

            UnitData unitToProduce = _productionQueue.Dequeue();
            _currentProducingUnit = unitToProduce;
            _productionStartTime = Time.time;

            float productionSpeedMultiplier = GetProductionSpeedMultiplier();
            _currentProductionTime = unitToProduce.productionTime / productionSpeedMultiplier;

            if (productionSlider != null) {
                productionSlider.gameObject.SetActive(true);
                productionSlider.SetProgress(0f);
            }

            float elapsedTime = 0f;
            while (elapsedTime < _currentProductionTime) {
                if (UnitManager.Instance != null && !UnitManager.Instance.CanSpawnUnit()) {
                    _currentProducingUnit = null;
                    if (productionSlider != null) {
                        productionSlider.gameObject.SetActive(false);
                    }

                    _productionQueue.Enqueue(unitToProduce);
                    _isProducing = false;
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                if (productionSlider != null) {
                    float progress = elapsedTime / _currentProductionTime;
                    productionSlider.SetProgress(progress);
                }

                yield return null;
            }

            _currentProducingUnit = null;

            if (productionSlider != null) {
                productionSlider.gameObject.SetActive(false);
            }

            if (!IsOperational) {
                Queue<UnitData> tempQueue = new Queue<UnitData>();
                tempQueue.Enqueue(unitToProduce);
                while (_productionQueue.Count > 0) {
                    tempQueue.Enqueue(_productionQueue.Dequeue());
                }

                _productionQueue.Clear();
                while (tempQueue.Count > 0) {
                    _productionQueue.Enqueue(tempQueue.Dequeue());
                }

                _isProducing = false;
                yield break;
            }

            if (UnitManager.Instance != null && !UnitManager.Instance.CanSpawnUnit()) {
                _productionQueue.Enqueue(unitToProduce);
                continue;
            }

            GameObject unitObj = Instantiate(unitToProduce.unitPrefab, transform.position, Quaternion.identity,
                UnitManager.Instance.unitParent);
            UnitBase unitBase = unitObj.GetComponent<UnitBase>();
            if (unitBase != null) {
                unitBase.unitData = unitToProduce;

                if (NoiseManager.Instance != null && unitBase.unitType == UnitBase.UnitType.Ally) {
                    NoiseManager.Instance.RegisterUnit(unitBase);
                }
            }

            int unitIndex = FindUnitIndex(unitToProduce);
            if (unitIndex >= 0) {
                _producedUnitCounts.TryGetValue(unitIndex, out int producedCount);
                _producedUnitCounts[unitIndex] = producedCount + 1;
                int currentCount = GetCurrentUnitCount(unitIndex);
                int targetCount = GetTargetUnitCount(unitIndex);
                OnUnitTargetChanged?.Invoke(unitIndex, currentCount, targetCount);
            }

            OnUnitProduced?.Invoke(unitToProduce);

            if (TutorialManager.Instance != null) {
                string unitTypeName = unitToProduce.unitName;
                if (unitTypeName.Contains("Miner") || unitTypeName.Contains("채굴")) {
                    TutorialManager.Instance.OnUnitProduced("Miner");
                }
                else if (unitTypeName.Contains("Processor") || unitTypeName.Contains("가공")) {
                    TutorialManager.Instance.OnUnitProduced("Processor");
                }
            }
        }

        _isProducing = false;
        _productionCoroutine = null;
    }

    private bool HasPendingTargets()
    {
        if (producibleUnits == null) {
            return false;
        }

        for (int i = 0; i < producibleUnits.Count; i++) {
            int currentCount = GetCurrentUnitCount(i);
            int targetCount = GetTargetUnitCount(i);
            if (currentCount < targetCount) {
                return true;
            }
        }

        return false;
    }

    private void UpdateQueueFromTargets()
    {
        if (producibleUnits == null) {
            return;
        }

        for (int i = 0; i < producibleUnits.Count; i++) {
            int currentCount = GetCurrentUnitCount(i);
            int targetCount = GetTargetUnitCount(i);

            if (currentCount < targetCount) {
                int needed = targetCount - currentCount;
                int currentlyActive = GetTotalActiveCount(i);
                int toAdd = needed - currentlyActive;

                if (toAdd > 0) {
                    AddUnitsToQueue(i, toAdd);
                }
            }
        }
    }

    private int CountUnitsInQueue(int unitIndex)
    {
        if (producibleUnits == null ||
            unitIndex < 0 || unitIndex >= producibleUnits.Count) {
            return 0;
        }

        UnitData targetUnit = producibleUnits[unitIndex];
        int count = 0;
        foreach (UnitData unit in _productionQueue) {
            if (unit == targetUnit) {
                count++;
            }
        }

        return count;
    }

    private int FindUnitIndex(UnitData unitData)
    {
        if (producibleUnits == null) {
            return -1;
        }

        for (int i = 0; i < producibleUnits.Count; i++) {
            if (producibleUnits[i] == unitData) {
                return i;
            }
        }

        return -1;
    }

    public float GetProductionProgress()
    {
        if (_currentProducingUnit == null || !_isProducing) {
            return 0f;
        }

        float elapsed = Time.time - _productionStartTime;
        return Mathf.Clamp01(elapsed / _currentProductionTime);
    }

    public bool IsProducing()
    {
        return _isProducing && _currentProducingUnit != null;
    }

    public override bool TryAddResource(ResourceType type, int amount)
    {
        if (type == ResourceType.Electricity) {
            return false;
        }

        return base.TryAddResource(type, amount);
    }

    public void AddResourceToStorageOnly(ResourceType type, int amount)
    {
        int totalAmount = GetTotalCurrentAmount();
        bool wasFull = totalAmount >= maxStorageAmount;

        if (wasFull) {
            return;
        }

        int canAddAmount = Mathf.Min(amount, maxStorageAmount - totalAmount);
        currentResources[type] += canAddAmount;

        InvokeResourceChanged(type);
    }

    public void InitializeStorage(ResourceType type, int amount)
    {
        if (type == ResourceType.Electricity) {
            return;
        }

        if (currentResources.ContainsKey(type)) {
            currentResources[type] = Mathf.Min(amount, maxStorageAmount);
        }
    }

    public void UpdateStorageUI()
    {
        RefreshAllResourcesUI();
    }

    protected override void OnDestroy()
    {
        if (GameManager.Instance != null) {
            GameManager.Instance.GameOver(transform);
        }

        base.OnDestroy();
    }
}
