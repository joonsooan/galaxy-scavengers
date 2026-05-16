using UnityEngine;

[DefaultExecutionOrder(-50)]
public class UnitAllyBatteryDriver : MonoBehaviour
{
    [SerializeField] private float approachStoppingDistance = 0.35f;
    [SerializeField] private float stationCenterArrivalDistance = 0.15f;
    [SerializeField] private float seekRetryCooldown = 0.75f;
    [SerializeField] [Range(0.05f, 1f)] private float movementSpeedMultiplierPowerEmpty = 0.5f;
    [SerializeField] [Range(0.05f, 1f)] private float workSpeedMultiplierPowerEmpty = 0.5f;

    private UnitBase _unit;
    private UnitMovement _movement;
    private UnitBattery _battery;
    private ChargingStation _targetStation;
    private bool _arrivalHandshakeComplete;
    private ChargingStation _boundStation;
    private bool _didInterruptForCharging;
    private float _nextSeekRetryTime;
    private float _nextRepathTime;

    public UnitBattery Battery => _battery;

    public bool BlocksWorkLogic =>
        _battery != null &&
        _unit != null &&
        _unit.unitData != null &&
        _unit.unitData.useInternalBattery &&
        !(_unit is Unit_Player) &&
        (_battery.FlowState != AllyBatteryFlowState.Normal || _battery.IsBatteryEmpty);

    private void Awake()
    {
        _unit = GetComponent<UnitBase>();
        _movement = GetComponent<UnitMovement>();
        _battery = GetComponent<UnitBattery>();
    }

    private void OnEnable()
    {
        if (_battery != null && _unit != null && _unit.unitData != null && _unit.unitData.useInternalBattery)
        {
            _battery.ConfigureFromUnitData(_unit.unitData);
        }
    }

    private void OnDisable()
    {
        if (_boundStation != null)
        {
            _boundStation.UnregisterDriver(this);
            _boundStation = null;
        }

        if (_targetStation != null)
        {
            _targetStation.ClearApproach(this);
            _targetStation = null;
        }

        _arrivalHandshakeComplete = false;
        _didInterruptForCharging = false;
        _nextSeekRetryTime = 0f;
        _nextRepathTime = 0f;

        if (_battery != null && _battery.FlowState != AllyBatteryFlowState.Normal)
        {
            _battery.SetFlowState(AllyBatteryFlowState.Normal);
        }
    }

    public void NotifyStationInvalid()
    {
        if (_targetStation != null)
        {
            _targetStation.ClearApproach(this);
        }

        _boundStation = null;
        _targetStation = null;
        _arrivalHandshakeComplete = false;
        _didInterruptForCharging = false;
        if (_battery != null)
        {
            _battery.SetFlowState(AllyBatteryFlowState.Normal);
        }

        _movement?.StopMovement();
        ForceUnitIdleAfterCharging();
    }

    private void Update()
    {
        if (_unit is Unit_Player || _battery == null || _unit == null || _unit.unitData == null ||
            !_unit.unitData.useInternalBattery)
        {
            return;
        }

        _battery.TickDrain(Time.deltaTime);

        switch (_battery.FlowState)
        {
        case AllyBatteryFlowState.Charging:
        case AllyBatteryFlowState.QueuedAtStation:
            return;
        case AllyBatteryFlowState.GoingToStation:
            UpdateGoingToStation();
            return;
        case AllyBatteryFlowState.Normal:
            if (Time.time < _nextSeekRetryTime)
            {
                return;
            }

            if (_battery.IsWorkableChargeLevel)
            {
                return;
            }

            BeginSeekChargingStation();
            break;
        }
    }

    private void BeginSeekChargingStation()
    {
        if (!ChargingStationRegistry.TryGetNearestStationForApproach(transform.position, this, out ChargingStation station))
        {
            _nextSeekRetryTime = Time.time + seekRetryCooldown;
            return;
        }

        PrepareUnitForCharging();
        _targetStation = station;
        _arrivalHandshakeComplete = false;
        _battery.SetFlowState(AllyBatteryFlowState.GoingToStation);
        bool hasPath = _movement != null && _movement.SetNewTarget(station.transform.position, approachStoppingDistance);
        if (!hasPath)
        {
            if (_targetStation != null)
            {
                _targetStation.ClearApproach(this);
                _targetStation = null;
            }

            _battery.SetFlowState(AllyBatteryFlowState.Normal);
            _didInterruptForCharging = false;
            _nextSeekRetryTime = Time.time + seekRetryCooldown;
        }
        else
        {
            _nextRepathTime = Time.time + seekRetryCooldown;
        }
    }

    private void UpdateGoingToStation()
    {
        if (_targetStation == null || !_targetStation.isActiveAndEnabled)
        {
            if (_targetStation != null)
            {
                _targetStation.ClearApproach(this);
            }

            _battery.SetFlowState(AllyBatteryFlowState.Normal);
            _movement?.StopMovement();
            _targetStation = null;
            _arrivalHandshakeComplete = false;
            _didInterruptForCharging = false;
            _nextSeekRetryTime = Time.time + seekRetryCooldown;
            return;
        }

        float dist = Vector3.Distance(transform.position, _targetStation.transform.position);
        bool atStationCenter = dist <= Mathf.Max(0.01f, stationCenterArrivalDistance);

        if (!atStationCenter)
        {
            if (_movement != null && !_movement.IsMoving && Time.time >= _nextRepathTime)
            {
                bool repath = _movement.SetNewTarget(_targetStation.transform.position, approachStoppingDistance);
                _nextRepathTime = Time.time + seekRetryCooldown;
                if (!repath)
                {
                    _targetStation.ClearApproach(this);
                    _battery.SetFlowState(AllyBatteryFlowState.Normal);
                    _targetStation = null;
                    _arrivalHandshakeComplete = false;
                    _didInterruptForCharging = false;
                    _nextSeekRetryTime = Time.time + seekRetryCooldown;
                }
            }

            return;
        }

        if (!_arrivalHandshakeComplete)
        {
            _arrivalHandshakeComplete = true;
            _boundStation = _targetStation;
            _targetStation.TryRegisterArrivedDriver(this);
        }
    }

    public void NotifyQueuedAtStation(ChargingStation station)
    {
        _boundStation = station;
        _battery.SetFlowState(AllyBatteryFlowState.QueuedAtStation);
        _movement?.StopMovement();
    }

    public void NotifyChargingStarted(ChargingStation station)
    {
        _boundStation = station;
        _battery.SetFlowState(AllyBatteryFlowState.Charging);
        _movement?.StopMovement();
    }

    public void NotifyChargingFinished(ChargingStation station)
    {
        if (_boundStation == station)
        {
            _boundStation = null;
        }

        _battery.SetFlowState(AllyBatteryFlowState.Normal);
        _targetStation = null;
        _arrivalHandshakeComplete = false;
        ForceUnitIdleAfterCharging();
    }

    private void PrepareUnitForCharging()
    {
        if (_didInterruptForCharging)
        {
            return;
        }

        gameObject.SendMessage("OnChargeStateEnter", SendMessageOptions.DontRequireReceiver);
        if (_unit != null)
        {
            _unit.currentState = UnitBase.UnitState.Idle;
        }

        _didInterruptForCharging = true;
    }

    private void ForceUnitIdleAfterCharging()
    {
        gameObject.SendMessage("OnChargeStateExit", SendMessageOptions.DontRequireReceiver);
        if (_unit != null)
        {
            _unit.currentState = UnitBase.UnitState.Idle;
        }

        _didInterruptForCharging = false;
    }

    public float GetMovementSpeedMultiplier()
    {
        if (_unit is Unit_Player || _battery == null || _unit == null || _unit.unitData == null ||
            !_unit.unitData.useInternalBattery)
        {
            return 1f;
        }

        if (_battery.IsBatteryEmpty && _battery.FlowState == AllyBatteryFlowState.Normal)
        {
            return movementSpeedMultiplierPowerEmpty;
        }

        return 1f;
    }

    public float GetWorkSpeedMultiplier()
    {
        if (_unit is Unit_Player || _battery == null || _unit == null || _unit.unitData == null ||
            !_unit.unitData.useInternalBattery)
        {
            return 1f;
        }

        if (_battery.IsBatteryEmpty && _battery.FlowState == AllyBatteryFlowState.Normal)
        {
            return workSpeedMultiplierPowerEmpty;
        }

        return 1f;
    }
}
