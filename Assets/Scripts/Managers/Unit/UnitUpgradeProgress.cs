using System;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class UnitUpgradeProgress : MonoBehaviour
{
    public static UnitUpgradeProgress Instance { get; private set; }

    public static event Action OnUpgradeStateChanged;

    [SerializeField] private UnitUpgradeCatalog catalog;

    private int _levelMove;
    private int _levelWork;
    private int _levelStorage;
    private int _levelMaxPopulation;

    private UnitUpgradeStatType? _pendingType;
    private float _pendingCompleteUnscaledTime;

    private const string PrefsPrefix = "UnitUpgrade_Level_";

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadLevels();
    }

    private void OnDestroy()
    {
        if (Instance == this) {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!_pendingType.HasValue) {
            return;
        }

        if (Time.unscaledTime < _pendingCompleteUnscaledTime) {
            return;
        }

        UnitUpgradeStatType done = _pendingType.Value;
        _pendingType = null;
        IncrementLevel(done);
        SaveLevel(done);
        OnUpgradeStateChanged?.Invoke();
        if (done == UnitUpgradeStatType.MoveSpeed || done == UnitUpgradeStatType.WorkSpeed ||
            done == UnitUpgradeStatType.Storage) {
            ApplyToAllMiners();
        }
    }

    public UnitUpgradeCatalog Catalog => catalog;

    public int GetLevel(UnitUpgradeStatType type)
    {
        switch (type) {
        case UnitUpgradeStatType.MoveSpeed:
            return _levelMove;
        case UnitUpgradeStatType.WorkSpeed:
            return _levelWork;
        case UnitUpgradeStatType.Storage:
            return _levelStorage;
        case UnitUpgradeStatType.MaxPopulation:
            return _levelMaxPopulation;
        default:
            return 0;
        }
    }

    public float GetMoveSpeedMultiplier()
    {
        return 1f + SumFloatBonuses(catalog != null ? catalog.GetLine(UnitUpgradeStatType.MoveSpeed) : null, _levelMove);
    }

    public float GetWorkSpeedMultiplier()
    {
        return 1f + SumFloatBonuses(catalog != null ? catalog.GetLine(UnitUpgradeStatType.WorkSpeed) : null, _levelWork);
    }

    public int GetStorageCapacityBonus()
    {
        return SumIntBonuses(catalog != null ? catalog.GetLine(UnitUpgradeStatType.Storage) : null, _levelStorage);
    }

    public int GetMaxPopulationBonus()
    {
        return SumIntBonuses(catalog != null ? catalog.GetLine(UnitUpgradeStatType.MaxPopulation) : null, _levelMaxPopulation);
    }

    public bool IsUpgradeInProgress(UnitUpgradeStatType type)
    {
        return _pendingType.HasValue && _pendingType.Value == type;
    }

    public bool IsAnyUpgradeInProgress()
    {
        return _pendingType.HasValue;
    }

    public float GetPendingCompleteUnscaledTime()
    {
        return _pendingCompleteUnscaledTime;
    }

    public bool TryQueueUpgrade(UnitUpgradeLineData line)
    {
        if (line == null || ResourceManager.Instance == null) {
            return false;
        }

        if (_pendingType.HasValue) {
            return false;
        }

        if (line.tiers == null) {
            return false;
        }

        int level = GetLevel(line.statType);
        if (level >= line.tiers.Length) {
            return false;
        }

        UnitUpgradeTier tier = line.tiers[level];
        if (tier.costs != null && tier.costs.Length > 0) {
            if (!ResourceManager.Instance.HasEnoughResources(tier.costs)) {
                return false;
            }

            if (!ResourceManager.Instance.SpendResources(tier.costs)) {
                return false;
            }
        }

        _pendingType = line.statType;
        _pendingCompleteUnscaledTime = Time.unscaledTime + Mathf.Max(0f, tier.upgradeTime);
        OnUpgradeStateChanged?.Invoke();
        return true;
    }

    public bool CanAffordNextTier(UnitUpgradeLineData line)
    {
        if (line == null || ResourceManager.Instance == null) {
            return false;
        }

        if (line.tiers == null) {
            return false;
        }

        int level = GetLevel(line.statType);
        if (level >= line.tiers.Length) {
            return false;
        }

        UnitUpgradeTier tier = line.tiers[level];
        if (tier.costs == null || tier.costs.Length == 0) {
            return true;
        }

        return ResourceManager.Instance.HasEnoughResources(tier.costs);
    }

    private void IncrementLevel(UnitUpgradeStatType type)
    {
        switch (type) {
        case UnitUpgradeStatType.MoveSpeed:
            _levelMove++;
            break;
        case UnitUpgradeStatType.WorkSpeed:
            _levelWork++;
            break;
        case UnitUpgradeStatType.Storage:
            _levelStorage++;
            break;
        case UnitUpgradeStatType.MaxPopulation:
            _levelMaxPopulation++;
            break;
        }
    }

    private void ApplyToAllMiners()
    {
        if (UnitManager.Instance == null) {
            return;
        }

        foreach (UnitBase u in UnitManager.Instance.AllyUnits) {
            if (u is Unit_Miner miner) {
                miner.ApplyMinerUpgradeModifiers();
            }
        }
    }

    private static float SumFloatBonuses(UnitUpgradeLineData line, int completedLevels)
    {
        if (line == null || line.tiers == null) {
            return 0f;
        }

        float sum = 0f;
        int n = Mathf.Min(completedLevels, line.tiers.Length);
        for (int i = 0; i < n; i++) {
            sum += line.tiers[i].floatStatBonus;
        }

        return sum;
    }

    private static int SumIntBonuses(UnitUpgradeLineData line, int completedLevels)
    {
        if (line == null || line.tiers == null) {
            return 0;
        }

        int sum = 0;
        int n = Mathf.Min(completedLevels, line.tiers.Length);
        for (int i = 0; i < n; i++) {
            sum += line.tiers[i].intStatBonus;
        }

        return sum;
    }

    private void LoadLevels()
    {
        _levelMove = PlayerPrefs.GetInt(PrefsKey(UnitUpgradeStatType.MoveSpeed), 0);
        _levelWork = PlayerPrefs.GetInt(PrefsKey(UnitUpgradeStatType.WorkSpeed), 0);
        _levelStorage = PlayerPrefs.GetInt(PrefsKey(UnitUpgradeStatType.Storage), 0);
        _levelMaxPopulation = PlayerPrefs.GetInt(PrefsKey(UnitUpgradeStatType.MaxPopulation), 0);
    }

    private void SaveLevel(UnitUpgradeStatType type)
    {
        PlayerPrefs.SetInt(PrefsKey(type), GetLevel(type));
        PlayerPrefs.Save();
    }

    private static string PrefsKey(UnitUpgradeStatType type)
    {
        return PrefsPrefix + type;
    }
}
