using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneHub : Damageable, IClickable, IAetherConsumer
{
    [SerializeField] private DroneHubData droneHubData;
    [Header("Aether Consumption")]
    [SerializeField] private int aetherConsumptionPerSecond = 1;
    [Header("Production Progress UI")]
    [SerializeField] private ProductionProgressSlider productionSlider;

    private readonly Dictionary<int, int> _producedUnitCounts = new ();
    private readonly Queue<UnitData> _productionQueue = new ();
    private readonly Dictionary<int, int> _targetUnitCounts = new ();
    
    private AetherConsumptionManager _aetherConsumptionManager;
    private UnitData _currentProducingUnit;
    
    private float _currentProductionTime;
    private float _productionStartTime;
    private bool _isManualQueueProcessing;
    private bool _isProducing;
    
    private Coroutine _productionCoroutine;
    private WaitForSeconds _productionWaitWait;
    private ProductionProgressSlider _sliderInstance;

    public DroneHubData DroneHubData {
        get {
            return droneHubData;
        }
    }

    private void Start()
    {
        _productionWaitWait = CoroutineCache.GetWaitForSeconds(1f);
        
        if (productionSlider != null) {
            productionSlider.Initialize(transform);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!IsProperlyPlacedBuilding()) {
            return;
        }

        FindAndCacheAetherManager();
        if (_aetherConsumptionManager != null) {
            _aetherConsumptionManager.RegisterConsumer(this);
        }

        ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;
    }

    protected override void OnDisable()
    {
        if (_aetherConsumptionManager != null) {
            _aetherConsumptionManager.UnregisterConsumer(this);
        }
        ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;

        if (productionSlider != null) {
            productionSlider.gameObject.SetActive(false);
        }
        base.OnDisable();
    }

    public int AetherConsumptionPerSecond {
        get {
            return aetherConsumptionPerSecond;
        }
    }

    public bool IsOperational { get; private set; } = true;

    public void OnAetherUnavailable()
    {
        if (IsOperational) {
            IsOperational = false;
            StopProduction();
        }
    }

    public void OnAetherAvailable()
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

    public void OnClicked()
    {
        OnDroneHubClicked?.Invoke(this);
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

    public static event Action<DroneHub> OnDroneHubClicked;
    public static event Action<int, int, int> OnUnitTargetChanged;
    public static event Action<UnitData> OnUnitProduced;

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
        if (_isManualQueueProcessing) return;

        if (!_isProducing && HasPendingTargets() && IsOperational) {
            UpdateQueueFromTargets();
            if (_productionQueue.Count > 0) {
                _productionCoroutine = StartCoroutine(ProcessProductionQueue());
            }
        }
    }

    private void FindAndCacheAetherManager()
    {
        if (_aetherConsumptionManager == null) {
            _aetherConsumptionManager = FindFirstObjectByType<AetherConsumptionManager>();
        }
    }

    private bool IsProperlyPlacedBuilding()
    {
        return BuildingManager.IsBuildingProperlyPlaced(transform);
    }

    public int GetCurrentUnitCount(int unitIndex)
    {
        if (droneHubData != null &&
            droneHubData.ProducibleUnits != null &&
            unitIndex >= 0 &&
            unitIndex < droneHubData.ProducibleUnits.Count &&
            UnitManager.Instance != null &&
            UnitManager.Instance.AllyUnits != null)
        {
            UnitData targetUnitData = droneHubData.ProducibleUnits[unitIndex];
            int count = 0;
            IReadOnlyList<UnitBase> allyUnits = UnitManager.Instance.AllyUnits;
            for (int i = 0; i < allyUnits.Count; i++)
            {
                UnitBase unit = allyUnits[i];
                if (unit == null) continue;
                if (unit.unitData == targetUnitData) count++;
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
        if (droneHubData == null || droneHubData.ProducibleUnits == null) return 0;

        int count = CountUnitsInQueue(unitIndex);

        if (_currentProducingUnit != null && _currentProducingUnit == droneHubData.ProducibleUnits[unitIndex]) {
            count++;
        }

        return count;
    }

    public void SetTargetUnitCount(int unitIndex, int targetCount)
    {
        if (droneHubData == null || droneHubData.ProducibleUnits == null ||
            unitIndex < 0 || unitIndex >= droneHubData.ProducibleUnits.Count) {
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
        if (droneHubData == null || droneHubData.ProducibleUnits == null) {
            return;
        }

        if (unitIndex < 0 || unitIndex >= droneHubData.ProducibleUnits.Count) {
            return;
        }

        UnitData unitData = droneHubData.ProducibleUnits[unitIndex];
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
        if (droneHubData == null || droneHubData.ProducibleUnits == null ||
            unitIndex < 0 || unitIndex >= droneHubData.ProducibleUnits.Count) {
            return;
        }

        UnitData targetUnit = droneHubData.ProducibleUnits[unitIndex];
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
                if (productionSlider != null) productionSlider.gameObject.SetActive(false);
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
                _sliderInstance?.gameObject.SetActive(false);
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

            GameObject unitObj = Instantiate(unitToProduce.unitPrefab, transform.position, Quaternion.identity, UnitManager.Instance.unitParent);
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
        if (droneHubData == null || droneHubData.ProducibleUnits == null) {
            return false;
        }

        for (int i = 0; i < droneHubData.ProducibleUnits.Count; i++) {
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
        if (droneHubData == null || droneHubData.ProducibleUnits == null) {
            return;
        }

        for (int i = 0; i < droneHubData.ProducibleUnits.Count; i++) {
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
        if (droneHubData == null || droneHubData.ProducibleUnits == null) {
            return 0;
        }

        if (unitIndex < 0 || unitIndex >= droneHubData.ProducibleUnits.Count) {
            return 0;
        }

        UnitData targetUnit = droneHubData.ProducibleUnits[unitIndex];
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
        if (droneHubData == null || droneHubData.ProducibleUnits == null) {
            return -1;
        }

        for (int i = 0; i < droneHubData.ProducibleUnits.Count; i++) {
            if (droneHubData.ProducibleUnits[i] == unitData) {
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
}
