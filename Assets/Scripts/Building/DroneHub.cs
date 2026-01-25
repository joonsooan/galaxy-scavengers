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
    [SerializeField] private GameObject progressSliderPrefab;
    [SerializeField] private Vector3 progressSliderOffset = new Vector3(0f, 1f, 0f);
    private readonly Dictionary<int, int> _producedUnitCounts = new Dictionary<int, int>();

    private readonly Queue<UnitData> _productionQueue = new Queue<UnitData>();
    private readonly Dictionary<int, int> _targetUnitCounts = new Dictionary<int, int>();
    private AetherConsumptionManager _aetherConsumptionManager;
    private UnitData _currentProducingUnit;
    private float _currentProductionTime;
    private bool _isManualQueueProcessing;
    private bool _isProducing;
    private float _productionStartTime;
    private ProductionProgressSlider _progressSlider;

    public DroneHubData DroneHubData {
        get {
            return droneHubData;
        }
    }

    private void Start()
    {
        if (IsProperlyPlacedBuilding() && progressSliderPrefab != null) {
            GameObject sliderObj = Instantiate(progressSliderPrefab);
            _progressSlider = sliderObj.GetComponent<ProductionProgressSlider>();
            if (_progressSlider != null) {
                _progressSlider.Initialize(this, progressSliderOffset);
                _progressSlider.gameObject.SetActive(false);
            }
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

        if (_progressSlider != null) {
            Destroy(_progressSlider.gameObject);
            _progressSlider = null;
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
                    StartCoroutine(ProcessProductionQueue());
                }
                else if (HasPendingTargets()) {
                    UpdateQueueFromTargets();
                    if (_productionQueue.Count > 0) {
                        StartCoroutine(ProcessProductionQueue());
                    }
                }
            }
        }
    }

    public void OnClicked()
    {
        OnDroneHubClicked?.Invoke(this);
    }

    public static event Action<DroneHub> OnDroneHubClicked;
    public static event Action<int, int, int> OnUnitTargetChanged;
    public static event Action<UnitData> OnUnitProduced;

    private void StopProduction()
    {
        if (_isProducing) {
            StopCoroutine(ProcessProductionQueue());
            _isProducing = false;
        }
    }

    private void OnResourceAmountChanged(ResourceType type, int amount)
    {
        if (_isManualQueueProcessing) return;

        if (!_isProducing && HasPendingTargets() && IsOperational) {
            UpdateQueueFromTargets();
            if (_productionQueue.Count > 0) {
                StartCoroutine(ProcessProductionQueue());
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
        _producedUnitCounts.TryGetValue(unitIndex, out int count);
        return count;
    }

    public int GetTargetUnitCount(int unitIndex)
    {
        _targetUnitCounts.TryGetValue(unitIndex, out int count);
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
        int currentlyQueued = CountUnitsInQueue(unitIndex);

        if (neededInTotal > currentlyQueued) {
            int unitsToAdd = neededInTotal - currentlyQueued;
            AddUnitsToQueue(unitIndex, unitsToAdd);
        }
        else if (neededInTotal < currentlyQueued) {
            int unitsToRemove = currentlyQueued - neededInTotal;
            RemoveUnitsFromQueue(unitIndex, unitsToRemove);
        }

        OnUnitTargetChanged?.Invoke(unitIndex, currentProduced, targetCount);

        if (!_isProducing && _productionQueue.Count > 0 && IsOperational) {
            StartCoroutine(ProcessProductionQueue());
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
                yield break;
            }

            if (_productionQueue.Count == 0) {
                UpdateQueueFromTargets();
            }

            if (_productionQueue.Count == 0) {
                if (!HasPendingTargets()) {
                    _isProducing = false;
                    yield break;
                }

                yield return new WaitForSeconds(1f);
                continue;
            }

            UnitData unitToProduce = _productionQueue.Dequeue();
            _currentProducingUnit = unitToProduce;
            _productionStartTime = Time.time;
            _currentProductionTime = unitToProduce.productionTime;

            yield return new WaitForSeconds(unitToProduce.productionTime);

            _currentProducingUnit = null;

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

            int unitIndex = FindUnitIndex(unitToProduce);
            if (unitIndex >= 0) {
                _producedUnitCounts.TryGetValue(unitIndex, out int currentCount);
                _producedUnitCounts[unitIndex] = currentCount + 1;

                int targetCount = GetTargetUnitCount(unitIndex);
                OnUnitTargetChanged?.Invoke(unitIndex, currentCount + 1, targetCount);
            }

            Instantiate(unitToProduce.unitPrefab, transform.position, Quaternion.identity, UnitManager.Instance.unitParent);

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
                int queued = CountUnitsInQueue(i);
                int toAdd = needed - queued;

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
