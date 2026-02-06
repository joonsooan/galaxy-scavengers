using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NoiseManager : MonoBehaviour
{
    public static NoiseManager Instance { get; private set; }

    [Header("Noise Settings")]
    [SerializeField] private float maxNoiseValue = 100f;

    private readonly HashSet<Damageable> _registeredBuildings = new HashSet<Damageable>();
    private readonly HashSet<UnitBase> _registeredUnits = new HashSet<UnitBase>();
    private float _barrierNoiseCoefficient = 0f;
    private float _totalNoise = 0f;

#if UNITY_EDITOR
    private bool _cheatNoise100Active = false;
    public bool IsCheatNoise100Active => _cheatNoise100Active;
#else
    public bool IsCheatNoise100Active => false;
#endif

    public event Action<float> OnNoiseChanged;
    public float TotalNoise => _totalNoise;
    public float NoisePercentage
    {
        get
        {
#if UNITY_EDITOR
            if (_cheatNoise100Active) return 100f;
#endif
            return Mathf.Clamp01(_totalNoise / maxNoiseValue) * 100f;
        }
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            _cheatNoise100Active = !_cheatNoise100Active;
            OnNoiseChanged?.Invoke(NoisePercentage);
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

        OnNoiseChanged?.Invoke(NoisePercentage);
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
        
        if (percentage >= 100f)
        {
            return NoiseZone.Danger;
        }
        else if (percentage >= 70f)
        {
            return NoiseZone.Warning;
        }
        else if (percentage >= 30f)
        {
            return NoiseZone.Caution;
        }
        else
        {
            return NoiseZone.Safe;
        }
    }

    public enum NoiseZone
    {
        Safe,
        Caution,
        Warning,
        Danger
    }
}
