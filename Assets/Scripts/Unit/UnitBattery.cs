using System;
using UnityEngine;

public class UnitBattery : MonoBehaviour
{
    [SerializeField] [Range(0f, 1f)] private float chargeThresholdNormalized = 0.25f;

    private float _currentAmount;
    private float _maxAmount = 1f;
    private float _drainPerSecond;
    private float _chargeDurationSecondsAtStation = 5f;
    private AllyBatteryFlowState _flowState = AllyBatteryFlowState.Normal;

    public event Action OnBatteryChanged;

    public AllyBatteryFlowState FlowState => _flowState;

    public float CurrentAmount => _currentAmount;

    public float MaxAmount => _maxAmount;

    public float ChargeThresholdNormalized
    {
        get => chargeThresholdNormalized;
        set
        {
            chargeThresholdNormalized = Mathf.Clamp01(value);
            OnBatteryChanged?.Invoke();
        }
    }

    public float NormalizedRatio => _maxAmount > 0.0001f ? Mathf.Clamp01(_currentAmount / _maxAmount) : 0f;

    public float ChargeDurationSecondsAtStation => _chargeDurationSecondsAtStation;

    public bool IsBatteryEmpty =>
        NormalizedRatio <= 0.0001f || _currentAmount <= 0.0001f;

    public bool IsWorkableChargeLevel =>
        _flowState == AllyBatteryFlowState.Normal &&
        !IsBatteryEmpty &&
        NormalizedRatio > chargeThresholdNormalized;

    public void ConfigureFromUnitData(UnitData data)
    {
        if (data == null || !data.useInternalBattery)
        {
            return;
        }

        _maxAmount = Mathf.Max(0.01f, data.maxBattery);
        _currentAmount = _maxAmount;
        _drainPerSecond = Mathf.Max(0f, data.batteryDrainPerSecond);
        _chargeDurationSecondsAtStation = Mathf.Max(0f, data.chargeDurationSecondsAtStation);
        OnBatteryChanged?.Invoke();
    }

    public void SetFlowState(AllyBatteryFlowState state)
    {
        if (_flowState == state)
        {
            return;
        }

        _flowState = state;
        OnBatteryChanged?.Invoke();
    }

    public void SetCurrentAmount(float value)
    {
        _currentAmount = Mathf.Clamp(value, 0f, _maxAmount);
        OnBatteryChanged?.Invoke();
    }

    public void TickDrain(float deltaTime)
    {
        if (_flowState == AllyBatteryFlowState.Charging)
        {
            return;
        }

        if (_drainPerSecond <= 0f || _maxAmount <= 0f)
        {
            return;
        }

        _currentAmount = Mathf.Max(0f, _currentAmount - _drainPerSecond * deltaTime);
        OnBatteryChanged?.Invoke();
    }

    public void RefillToMax()
    {
        _currentAmount = _maxAmount;
        OnBatteryChanged?.Invoke();
    }
}
