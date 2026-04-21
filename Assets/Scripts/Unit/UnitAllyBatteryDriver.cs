using UnityEngine;

[DefaultExecutionOrder(-50)]
public class UnitAllyBatteryDriver : MonoBehaviour
{
    [SerializeField] private float approachStoppingDistance = 0.35f;

    private UnitBase _unit;
    private UnitMovement _movement;
    private UnitBattery _battery;
    private ChargingStation _targetStation;
    private bool _arrivalHandshakeComplete;
    private ChargingStation _boundStation;

    public UnitBattery Battery => _battery;

    public bool BlocksWorkLogic =>
        _battery != null &&
        _unit != null &&
        _unit.unitData != null &&
        _unit.unitData.useInternalBattery &&
        !(_unit is Unit_Player) &&
        (!_battery.IsWorkableChargeLevel || _battery.FlowState != AllyBatteryFlowState.Normal);

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

        _targetStation = null;
        _arrivalHandshakeComplete = false;

        if (_battery != null && _battery.FlowState != AllyBatteryFlowState.Normal)
        {
            _battery.SetFlowState(AllyBatteryFlowState.Normal);
        }
    }

    public void NotifyStationInvalid()
    {
        _boundStation = null;
        _targetStation = null;
        _arrivalHandshakeComplete = false;
        if (_battery != null)
        {
            _battery.SetFlowState(AllyBatteryFlowState.Normal);
        }

        _movement?.StopMovement();
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
        ChargingStation station = ChargingStationRegistry.GetNearest(transform.position);
        if (station == null)
        {
            return;
        }

        _targetStation = station;
        _arrivalHandshakeComplete = false;
        _battery.SetFlowState(AllyBatteryFlowState.GoingToStation);
        _movement?.SetNewTarget(station.transform.position, approachStoppingDistance);
    }

    private void UpdateGoingToStation()
    {
        if (_targetStation == null || !_targetStation.isActiveAndEnabled)
        {
            _battery.SetFlowState(AllyBatteryFlowState.Normal);
            _movement?.StopMovement();
            _targetStation = null;
            _arrivalHandshakeComplete = false;
            return;
        }

        float r = _targetStation.InteractionRadius;
        float dist = Vector3.Distance(transform.position, _targetStation.transform.position);
        bool inRange = dist <= r + 0.05f;
        bool pathDone = _movement != null && _movement.HasReachedTarget(Mathf.Max(approachStoppingDistance, 0.15f));

        if (!inRange && !pathDone)
        {
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
    }
}
