using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NoiseManager : MonoBehaviour
{
    public static NoiseManager Instance { get; private set; }

    [Header("Noise Settings")]
    [SerializeField] private float maxNoiseValue = 100f;

    [Header("Zone Thresholds (Percentage)")]
    [SerializeField] private float dangerThreshold = 100f;
    [SerializeField] private float warningThreshold = 70f;
    [SerializeField] private float cautionThreshold = 50f;

#if UNITY_EDITOR
    [Header("Test (Editor Only)")]
    [SerializeField] private bool useTestNoiseValue;
    [SerializeField] [Range(0f, 100f)] private float testNoisePercentage;
    private float _lastTestNoisePercentage = -1f;
#endif

    private readonly HashSet<Damageable> _registeredBuildings = new HashSet<Damageable>();
    private readonly HashSet<UnitBase> _registeredUnits = new HashSet<UnitBase>();
    private float _barrierNoiseCoefficient;
    private float _totalNoise;

    public bool IsCheatNoise100Active
    {
        get
        {
#if UNITY_EDITOR
            return useTestNoiseValue && testNoisePercentage >= 100f;
#else
            return false;
#endif
        }
    }

    public event Action<float> OnNoiseChanged;
    public float NoisePercentage
    {
        get
        {
#if UNITY_EDITOR
            if (useTestNoiseValue) return testNoisePercentage;
#endif
            return Mathf.Clamp01(_totalNoise / maxNoiseValue) * 100f;
        }
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (useTestNoiseValue)
        {
            if (Mathf.Abs(_lastTestNoisePercentage - testNoisePercentage) > 0.001f)
            {
                _lastTestNoisePercentage = testNoisePercentage;
                OnNoiseChanged?.Invoke(testNoisePercentage);
                CheckAndLogZoneChange();
            }
        }
        else if (_lastTestNoisePercentage >= 0f)
        {
            _lastTestNoisePercentage = -1f;
            float real = Mathf.Clamp01(_totalNoise / maxNoiseValue) * 100f;
            OnNoiseChanged?.Invoke(real);
            CheckAndLogZoneChange();
        }
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterBuilding(Damageable building)
    {
        if (building == null) return;
        _registeredBuildings.Add(building);
        RecalculateNoise();
    }

    public void UnregisterBuilding(Damageable building)
    {
        if (building == null) return;
        _registeredBuildings.Remove(building);
        RecalculateNoise();
    }

    public void RegisterUnit(UnitBase unit)
    {
        if (unit == null || unit.unitType != UnitBase.UnitType.Ally) return;
        _registeredUnits.Add(unit);
        RecalculateNoise();
    }

    public void UnregisterUnit(UnitBase unit)
    {
        if (unit == null) return;
        _registeredUnits.Remove(unit);
        RecalculateNoise();
    }

    public void SetBarrierNoiseCoefficient(float noiseValue)
    {
        _barrierNoiseCoefficient = noiseValue;
        RecalculateNoise();
    }

    private void RecalculateNoise()
    {
        float buildingNoise = 0f;
        foreach (Damageable building in _registeredBuildings)
        {
            if (building == null) continue;

            BuildingDataHolder dataHolder = building.GetComponent<BuildingDataHolder>();
            if (dataHolder != null && dataHolder.buildingData != null)
            {
                buildingNoise += dataHolder.buildingData.noiseCoefficient;
            }
        }

        float unitNoise = 0f;
        foreach (UnitBase unit in _registeredUnits)
        {
            if (unit == null) continue;

            UnitData unitData = GetUnitData(unit);
            if (unitData != null)
            {
                unitNoise += unitData.noiseCoefficient;
            }
        }

        _totalNoise = buildingNoise + unitNoise + _barrierNoiseCoefficient;
        _totalNoise = Mathf.Clamp(_totalNoise, 0f, maxNoiseValue);

        float pct = NoisePercentage;
        OnNoiseChanged?.Invoke(pct);
        CheckAndLogZoneChange();
    }

    private void CheckAndLogZoneChange()
    {
        NoiseZone current = GetCurrentNoiseZone();
    }

    private UnitData GetUnitData(UnitBase unit)
    {
        if (unit != null && unit.unitData != null)
        {
            return unit.unitData;
        }
        return null;
    }

    public NoiseZone GetCurrentNoiseZone()
    {
        float percentage = NoisePercentage;

        if (percentage >= dangerThreshold)
        {
            return NoiseZone.Danger;
        }
        if (percentage >= warningThreshold)
        {
            return NoiseZone.Warning;
        }
        if (percentage >= cautionThreshold)
        {
            return NoiseZone.Caution;
        }
        return NoiseZone.Safe;
    }

    public enum NoiseZone
    {
        Safe,
        Caution,
        Warning,
        Danger
    }
}
