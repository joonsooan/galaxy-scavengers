using System;
using System.Collections.Generic;
using UnityEngine;

public class NoiseManager : MonoBehaviour
{
    public static NoiseManager Instance { get; private set; }

    [Header("Noise Settings")]
    [SerializeField] private float maxNoiseValue = 100f;
    [SerializeField] private float timeBasedNoiseIncreasePerHour = 0f;

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
    private readonly Dictionary<Damageable, float> _buildingNoiseCache = new Dictionary<Damageable, float>();
    private readonly HashSet<UnitBase> _registeredUnits = new HashSet<UnitBase>();
    private float _barrierNoiseCoefficient;
    private float _totalNoise;
    private float _lastCheckedElapsedHours = -1f;

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

    private void Update()
    {
#if UNITY_EDITOR
        if (useTestNoiseValue)
        {
            if (Mathf.Abs(_lastTestNoisePercentage - testNoisePercentage) > 0.001f)
            {
                _lastTestNoisePercentage = testNoisePercentage;
                OnNoiseChanged?.Invoke(testNoisePercentage);
            }
            return;
        }
        else if (_lastTestNoisePercentage >= 0f)
        {
            _lastTestNoisePercentage = -1f;
            float real = Mathf.Clamp01(_totalNoise / maxNoiseValue) * 100f;
            OnNoiseChanged?.Invoke(real);
        }
#endif

        if (timeBasedNoiseIncreasePerHour > 0f && DayNightCycleManager.Instance != null)
        {
            float currentElapsedHours = DayNightCycleManager.Instance.GetTotalElapsedInGameHours();
            
            if (Mathf.Abs(currentElapsedHours - _lastCheckedElapsedHours) > 0.0001f)
            {
                RecalculateNoise();
                _lastCheckedElapsedHours = currentElapsedHours;
            }
        }
    }

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

    private void Start()
    {
        if (timeBasedNoiseIncreasePerHour > 0f && DayNightCycleManager.Instance != null)
        {
            _lastCheckedElapsedHours = DayNightCycleManager.Instance.GetTotalElapsedInGameHours();
        }
    }

    public void RegisterBuilding(Damageable building)
    {
        if (building == null) return;
        _registeredBuildings.Add(building);

        float coef = 0f;
        BuildingDataHolder dataHolder = building.GetComponent<BuildingDataHolder>();
        if (dataHolder != null && dataHolder.buildingData != null)
        {
            coef = dataHolder.buildingData.noiseCoefficient;
        }
        _buildingNoiseCache[building] = coef;

        RecalculateNoise();
    }

    public void UnregisterBuilding(Damageable building)
    {
        if (building == null) return;
        _registeredBuildings.Remove(building);
        _buildingNoiseCache.Remove(building);
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

            if (_buildingNoiseCache.TryGetValue(building, out float coef))
            {
                buildingNoise += coef;
            }
            else
            {
                BuildingDataHolder dataHolder = building.GetComponent<BuildingDataHolder>();
                if (dataHolder != null && dataHolder.buildingData != null)
                {
                    coef = dataHolder.buildingData.noiseCoefficient;
                }
                _buildingNoiseCache[building] = coef;
                buildingNoise += coef;
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

        float timeBasedNoise = 0f;
        if (timeBasedNoiseIncreasePerHour > 0f && DayNightCycleManager.Instance != null)
        {
            float totalElapsedHours = DayNightCycleManager.Instance.GetTotalElapsedInGameHours();
            timeBasedNoise = totalElapsedHours * timeBasedNoiseIncreasePerHour;
        }

        _totalNoise = buildingNoise + unitNoise + _barrierNoiseCoefficient + timeBasedNoise;
        _totalNoise = Mathf.Clamp(_totalNoise, 0f, maxNoiseValue);

        float pct = NoisePercentage;
        OnNoiseChanged?.Invoke(pct);
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
