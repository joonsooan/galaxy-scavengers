using System;
using System.Collections.Generic;
using UnityEngine;

public class DataExtractor : Damageable, IClickable, IElectricityConsumer
{
    [SerializeField] private ExtractorData extractorData;
    [Header("Connection")]
    [Tooltip("NxN cells centered on building footprint (same idea as ResourceGenerator supply range).")]
    [SerializeField] [Range(1, 50)] private int connectionRangeCells = 5;

    private ElectricityConsumptionManager _electricityConsumptionManager;
    private bool _isInitialized;
    private bool _isOperational = true;
    private float _currentExtractedPercent;
    private float _cycleProgress01;
    private readonly HashSet<int> _selectedTierIndices = new HashSet<int>();

    public ExtractorData ExtractorDataAsset => extractorData;
    public float CurrentExtractedPercent => _currentExtractedPercent;
    public float MaxExtractablePercent => extractorData != null ? extractorData.maxExtractablePercent : 0f;
    public float CycleProgress01 => _cycleProgress01;

    public int ElectricityConsumptionPerSecond =>
        extractorData != null ? extractorData.electricityConsumptionPerSecond : 0;

    public bool IsOperational => _isOperational;

    public static event Action<DataExtractor> OnDataExtractorClicked;

    public event Action OnExtractorStateChanged;

    public void OnClicked()
    {
        OnDataExtractorClicked?.Invoke(this);
    }

    public ExtractorInputTier GetTier(int index)
    {
        if (extractorData == null || extractorData.inputTiers == null) {
            return null;
        }

        if (index < 0 || index >= extractorData.inputTiers.Count) {
            return null;
        }

        return extractorData.inputTiers[index];
    }

    public bool IsTierSelected(int index)
    {
        return _selectedTierIndices.Contains(index);
    }

    public IReadOnlyCollection<int> GetSelectedTierIndices()
    {
        return _selectedTierIndices;
    }

    public void ToggleTierSelection(int index)
    {
        if (extractorData?.inputTiers == null) {
            return;
        }

        if (index < 0 || index >= extractorData.inputTiers.Count) {
            return;
        }

        if (_selectedTierIndices.Contains(index)) {
            _selectedTierIndices.Remove(index);
        }
        else {
            _selectedTierIndices.Add(index);
        }

        EnsureAtLeastOneTierSelected();
        _cycleProgress01 = 0f;
        OnExtractorStateChanged?.Invoke();
    }

    public int GetTierCount()
    {
        return extractorData != null && extractorData.inputTiers != null ? extractorData.inputTiers.Count : 0;
    }

    public int GetExpectedOutputPerCycle()
    {
        int sum = 0;
        if (extractorData?.inputTiers == null) {
            return 0;
        }

        foreach (int idx in _selectedTierIndices) {
            ExtractorInputTier tier = GetTier(idx);
            if (tier != null) {
                sum += Mathf.Max(0, tier.outputDataItemsPerCycle);
            }
        }

        return sum;
    }

    public int GetAvailableInConnectedStorages(ResourceType type)
    {
        int sum = 0;
        foreach (IStorage storage in CollectStoragesInConnectionRange()) {
            if (storage != null) {
                sum += storage.GetCurrentResourceAmount(type);
            }
        }

        return sum;
    }

    public List<IStorage> GetConnectedStoragesSnapshot()
    {
        return CollectStoragesInConnectionRange();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!BuildingManager.IsBuildingProperlyPlaced(transform)) {
            _isInitialized = false;
            return;
        }

        FindAndCacheElectricityManager();
        if (_electricityConsumptionManager != null && ElectricityConsumptionPerSecond > 0) {
            _electricityConsumptionManager.RegisterConsumer(this);
        }

        ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;
        _isInitialized = true;
        EnsureDefaultTierSelection();
        OnExtractorStateChanged?.Invoke();
    }

    protected override void OnDisable()
    {
        ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;

        if (_electricityConsumptionManager != null) {
            _electricityConsumptionManager.UnregisterConsumer(this);
        }

        base.OnDisable();
    }

    private void OnResourceAmountChanged(ResourceType type, int amount)
    {
        OnExtractorStateChanged?.Invoke();
    }

    private void Update()
    {
        if (!_isInitialized || extractorData == null) {
            return;
        }

        if (_currentExtractedPercent >= extractorData.maxExtractablePercent - 0.0001f) {
            _cycleProgress01 = 0f;
            return;
        }

        if (!_isOperational) {
            return;
        }

        List<int> selectedConsumable = GetConsumableSelectedTierIndices();
        if (selectedConsumable.Count == 0) {
            _cycleProgress01 = 0f;
            OnExtractorStateChanged?.Invoke();
            return;
        }

        float cycleDuration = GetCycleDurationForSelectedTiers(selectedConsumable);
        if (cycleDuration <= 0f) {
            _cycleProgress01 = 0f;
            return;
        }

        _cycleProgress01 += Time.deltaTime / cycleDuration;

        if (_cycleProgress01 < 1f) {
            return;
        }

        _cycleProgress01 = 0f;
        TryCompleteCycle(selectedConsumable);
    }

    private void TryCompleteCycle(List<int> selectedTierIndices)
    {
        List<IStorage> storages = CollectStoragesInConnectionRange();
        storages.Sort((a, b) => {
            float da = a != null ? (transform.position - a.GetPosition()).sqrMagnitude : float.MaxValue;
            float db = b != null ? (transform.position - b.GetPosition()).sqrMagnitude : float.MaxValue;
            return da.CompareTo(db);
        });

        selectedTierIndices.Sort((a, b) => GetTierEfficiencyScore(b).CompareTo(GetTierEfficiencyScore(a)));

        bool consumedAtLeastOne = false;
        foreach (int tierIndex in selectedTierIndices) {
            ExtractorInputTier tier = GetTier(tierIndex);
            if (tier == null) {
                continue;
            }

            if (!TryWithdrawAcrossStorages(storages, tier.inputResource, tier.amountConsumedPerCycle)) {
                continue;
            }

            consumedAtLeastOne = true;
            float gain = tier.dataPercentGainedPerCycle;
            float max = extractorData.maxExtractablePercent;
            float next = Mathf.Min(_currentExtractedPercent + gain, max);
            _currentExtractedPercent = next;

            if (tier.outputDataItemsPerCycle > 0 && ResourceManager.Instance != null) {
                ResourceManager.Instance.AddResource(extractorData.outputResourceType, tier.outputDataItemsPerCycle);
            }

            if (_currentExtractedPercent >= extractorData.maxExtractablePercent - 0.0001f) {
                break;
            }
        }

        if (!consumedAtLeastOne) {
            _cycleProgress01 = 0f;
        }

        OnExtractorStateChanged?.Invoke();
    }

    private static bool TryWithdrawAcrossStorages(List<IStorage> storages, ResourceType type, int need)
    {
        if (need <= 0) {
            return true;
        }

        int remaining = need;
        foreach (IStorage storage in storages) {
            if (storage == null || remaining <= 0) {
                continue;
            }

            if (storage.TryWithdrawResource(type, remaining, out int withdrawn)) {
                remaining -= withdrawn;
            }
        }

        return remaining <= 0;
    }

    private List<int> GetConsumableSelectedTierIndices()
    {
        List<int> result = new List<int>();
        foreach (int idx in _selectedTierIndices) {
            ExtractorInputTier tier = GetTier(idx);
            if (tier == null) {
                continue;
            }

            if (GetAvailableInConnectedStorages(tier.inputResource) >= tier.amountConsumedPerCycle) {
                result.Add(idx);
            }
        }

        return result;
    }

    private float GetCycleDurationForSelectedTiers(List<int> selectedTierIndices)
    {
        float minDuration = float.MaxValue;
        foreach (int idx in selectedTierIndices) {
            ExtractorInputTier tier = GetTier(idx);
            if (tier == null) {
                continue;
            }

            float duration = Mathf.Max(0.01f, tier.cycleDurationSeconds);
            if (duration < minDuration) {
                minDuration = duration;
            }
        }

        return minDuration == float.MaxValue ? 0f : minDuration;
    }

    private float GetTierEfficiencyScore(int tierIndex)
    {
        ExtractorInputTier tier = GetTier(tierIndex);
        if (tier == null) {
            return 0f;
        }

        float cost = Mathf.Max(1f, tier.amountConsumedPerCycle);
        return tier.dataPercentGainedPerCycle / cost;
    }

    private void EnsureDefaultTierSelection()
    {
        if (extractorData?.inputTiers == null || extractorData.inputTiers.Count == 0) {
            _selectedTierIndices.Clear();
            return;
        }

        EnsureAtLeastOneTierSelected();
    }

    private void EnsureAtLeastOneTierSelected()
    {
        if (extractorData?.inputTiers == null || extractorData.inputTiers.Count == 0) {
            _selectedTierIndices.Clear();
            return;
        }

        _selectedTierIndices.RemoveWhere(idx => idx < 0 || idx >= extractorData.inputTiers.Count);
        if (_selectedTierIndices.Count == 0) {
            _selectedTierIndices.Add(0);
        }
    }

    private List<IStorage> CollectStoragesInConnectionRange()
    {
        List<IStorage> fromGrid = CollectStoragesFromGridCoverage();
        if (fromGrid.Count > 0) {
            return fromGrid;
        }

        return CollectStoragesByWorldRadiusFallback();
    }

    private List<IStorage> CollectStoragesFromGridCoverage()
    {
        HashSet<IStorage> set = new HashSet<IStorage>();
        BuildingManager bm = BuildingManager.Instance;
        if (bm == null || !bm.TryGetBuildingAnchorCells(transform, out _, out List<Vector3Int> occupied) ||
            occupied == null || occupied.Count == 0) {
            return new List<IStorage>();
        }

        BoundsInt coverage = PowerGridGeometry.ComputeSquareCoverageCenteredOnFootprint(bm.grid, occupied, connectionRangeCells);
        foreach (Vector3Int cell in coverage.allPositionsWithin) {
            BuildingPiece piece = bm.GetPieceAt(cell);
            if (piece != null) {
                MainStructure mainStructure = piece.GetComponentInParent<MainStructure>();
                if (mainStructure != null && BuildingManager.IsBuildingProperlyPlaced(mainStructure.transform)) {
                    set.Add(mainStructure);
                }

                Storage storage = piece.GetComponentInParent<Storage>();
                if (storage != null && BuildingManager.IsBuildingProperlyPlaced(storage.transform)) {
                    set.Add(storage);
                }
            }
            else if (bm.TryGetMainStructureAtCell(cell, out MainStructure mainAtCell)) {
                set.Add(mainAtCell);
            }
        }

        return new List<IStorage>(set);
    }

    /// <summary>
    /// When footprint lookup fails (prefab/hierarchy) or grid finds no storages, match ResourceGenerator-style
    /// reach using world distance so UI still lists nearby inventories.
    /// </summary>
    private List<IStorage> CollectStoragesByWorldRadiusFallback()
    {
        List<IStorage> result = new List<IStorage>();
        if (ResourceManager.Instance == null) {
            return result;
        }

        float maxDist = GetConnectionReachWorldRadius();
        Vector3 origin = transform.position;
        foreach (IStorage storage in ResourceManager.Instance.GetAllStorages()) {
            if (storage == null) {
                continue;
            }

            Component c = storage as Component;
            if (c != null && !BuildingManager.IsBuildingProperlyPlaced(c.transform)) {
                continue;
            }

            if (Vector3.Distance(origin, storage.GetPosition()) <= maxDist) {
                result.Add(storage);
            }
        }

        return result;
    }

    private float GetConnectionReachWorldRadius()
    {
        BuildingManager bm = BuildingManager.Instance;
        float cell = 1f;
        if (bm != null && bm.grid != null) {
            Vector3 cs = bm.grid.cellSize;
            cell = Mathf.Max(cs.x, cs.y, 0.01f);
        }

        return connectionRangeCells * cell;
    }

    private void FindAndCacheElectricityManager()
    {
        if (_electricityConsumptionManager == null) {
            _electricityConsumptionManager = ElectricityConsumptionManager.Instance;
        }
    }

    public void OnElectricityUnavailable()
    {
        if (_isOperational) {
            _isOperational = false;
            _cycleProgress01 = 0f;
            OnExtractorStateChanged?.Invoke();
        }
    }

    public void OnElectricityAvailable()
    {
        if (!_isOperational) {
            _isOperational = true;
            OnExtractorStateChanged?.Invoke();
        }
    }
}
