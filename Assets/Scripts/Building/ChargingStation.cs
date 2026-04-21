using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChargingStation : Damageable, IElectricityConsumer
{
    [Header("Charging")]
    [SerializeField] private float interactionRadius = 2.5f;
    [Header("Electricity consumption (grid)")]
    [SerializeField] private int idleElectricityConsumptionPerSecond = 1;
    [SerializeField] private int activeElectricityConsumptionPerSecond = 5;

    private ElectricityConsumptionManager _electricityConsumptionManager;
    private readonly Queue<UnitAllyBatteryDriver> _waitingDrivers = new Queue<UnitAllyBatteryDriver>();
    private UnitAllyBatteryDriver _activeDriver;
    private Coroutine _chargeCoroutine;
    private bool _consumerOperational = true;

    public int ElectricityConsumptionPerSecond =>
        _activeDriver != null ? activeElectricityConsumptionPerSecond : idleElectricityConsumptionPerSecond;

    public bool IsOperational => _consumerOperational;

    public float InteractionRadius => interactionRadius;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!BuildingManager.IsBuildingProperlyPlaced(transform))
        {
            return;
        }

        FindAndCacheElectricityManager();
        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.RegisterConsumer(this);
        }

        ChargingStationRegistry.Register(this);
    }

    protected override void OnDisable()
    {
        ChargingStationRegistry.Unregister(this);

        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.UnregisterConsumer(this);
        }

        if (_chargeCoroutine != null)
        {
            StopCoroutine(_chargeCoroutine);
            _chargeCoroutine = null;
        }

        while (_waitingDrivers.Count > 0)
        {
            UnitAllyBatteryDriver d = _waitingDrivers.Dequeue();
            d?.NotifyStationInvalid();
        }

        if (_activeDriver != null)
        {
            UnitAllyBatteryDriver wasActive = _activeDriver;
            _activeDriver = null;
            wasActive.NotifyStationInvalid();
        }

        base.OnDisable();
    }

    private void FindAndCacheElectricityManager()
    {
        if (_electricityConsumptionManager == null)
        {
            _electricityConsumptionManager = ElectricityConsumptionManager.Instance;
        }
    }

    public void OnElectricityUnavailable()
    {
        if (_consumerOperational)
        {
            _consumerOperational = false;
        }
    }

    public void OnElectricityAvailable()
    {
        if (!_consumerOperational)
        {
            _consumerOperational = true;
            TryProcessQueue();
        }
    }

    public bool TryRegisterArrivedDriver(UnitAllyBatteryDriver driver)
    {
        if (driver == null)
        {
            return false;
        }

        if (_activeDriver == driver)
        {
            return true;
        }

        foreach (UnitAllyBatteryDriver d in _waitingDrivers)
        {
            if (d == driver)
            {
                return true;
            }
        }

        if (_activeDriver == null && IsOperational)
        {
            _activeDriver = driver;
            driver.NotifyChargingStarted(this);
            _chargeCoroutine = StartCoroutine(ChargeRoutine(_activeDriver));
            return true;
        }

        _waitingDrivers.Enqueue(driver);
        driver.NotifyQueuedAtStation(this);
        return true;
    }

    public void UnregisterDriver(UnitAllyBatteryDriver driver)
    {
        if (driver == null)
        {
            return;
        }

        if (_activeDriver == driver)
        {
            StopActiveChargeAndClearSlot();
            TryProcessQueue();
            return;
        }

        Queue<UnitAllyBatteryDriver> rebuilt = new Queue<UnitAllyBatteryDriver>();
        while (_waitingDrivers.Count > 0)
        {
            UnitAllyBatteryDriver d = _waitingDrivers.Dequeue();
            if (d != null && d != driver)
            {
                rebuilt.Enqueue(d);
            }
        }

        _waitingDrivers.Clear();
        foreach (UnitAllyBatteryDriver d in rebuilt)
        {
            _waitingDrivers.Enqueue(d);
        }
    }

    private void TryProcessQueue()
    {
        if (_activeDriver != null || !IsOperational)
        {
            return;
        }

        while (_waitingDrivers.Count > 0)
        {
            UnitAllyBatteryDriver next = _waitingDrivers.Dequeue();
            if (next == null || !next.isActiveAndEnabled)
            {
                continue;
            }

            _activeDriver = next;
            _activeDriver.NotifyChargingStarted(this);
            _chargeCoroutine = StartCoroutine(ChargeRoutine(_activeDriver));
            return;
        }
    }

    private IEnumerator ChargeRoutine(UnitAllyBatteryDriver driver)
    {
        UnitBattery battery = driver != null ? driver.Battery : null;
        if (battery == null)
        {
            ClearActiveChargeSlot();
            TryProcessQueue();
            yield break;
        }

        float duration = battery.ChargeDurationSecondsAtStation;
        float start = battery.CurrentAmount;
        float max = battery.MaxAmount;

        if (duration <= 0f)
        {
            battery.RefillToMax();
            driver.NotifyChargingFinished(this);
            ClearActiveChargeSlot();
            TryProcessQueue();
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            if (!IsOperational)
            {
                yield return null;
                continue;
            }

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            battery.SetCurrentAmount(Mathf.Lerp(start, max, u));
            yield return null;
        }

        battery.RefillToMax();
        driver.NotifyChargingFinished(this);
        ClearActiveChargeSlot();
        TryProcessQueue();
    }

    private void ClearActiveChargeSlot()
    {
        _activeDriver = null;
        _chargeCoroutine = null;
    }

    private void StopActiveChargeAndClearSlot()
    {
        if (_chargeCoroutine != null)
        {
            StopCoroutine(_chargeCoroutine);
        }

        ClearActiveChargeSlot();
    }
}
