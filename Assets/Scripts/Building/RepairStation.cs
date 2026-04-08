using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RepairStation : Damageable, IElectricityConsumer
{
    [Header("Heal Settings")]
    [SerializeField] private float healInterval = 1f;
    [SerializeField] private float healRadius = 5f;
    [SerializeField] private int healAmount = 10;
    [Header("Electricity consumption")]
    [SerializeField] private int electricityConsumptionPerSecond = 1;
    
    private Coroutine _healCoroutine;
    private WaitForSeconds _healWait;
    private bool _isOperational = true;
    private ElectricityConsumptionManager _electricityConsumptionManager;
    
    public int ElectricityConsumptionPerSecond => electricityConsumptionPerSecond;
    public bool IsOperational => _isOperational;
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healRadius);
    }
    
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
        
        _healWait = new WaitForSeconds(healInterval);
        StartHealing();
    }
    
    protected override void OnDisable()
    {
        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.UnregisterConsumer(this);
        }
        
        base.OnDisable();
        
        StopHealing();
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
        if (_isOperational)
        {
            _isOperational = false;
            StopHealing();
        }
    }
    
    public void OnElectricityAvailable()
    {
        if (!_isOperational)
        {
            _isOperational = true;
            StartHealing();
        }
    }
    
    private void StartHealing()
    {
        if (_healCoroutine == null)
        {
            _healCoroutine = StartCoroutine(HealCoroutine());
        }
    }
    
    private void StopHealing()
    {
        if (_healCoroutine != null)
        {
            StopCoroutine(_healCoroutine);
            _healCoroutine = null;
        }
    }
    
    private IEnumerator HealCoroutine()
    {
        while (true)
        {
            if (_isOperational)
            {
                HealNearbyTargets();
            }
            yield return _healWait;
        }
    }
    
    private void HealNearbyTargets()
    {
        Vector3 stationPosition = transform.position;
        
        if (TargetManager.Instance != null)
        {
            foreach (Damageable building in TargetManager.Instance.AllTargets)
            {
                if (building == null || building == this) continue;
                if (!building.gameObject.activeInHierarchy) continue;
                
                float distance = Vector2.Distance(stationPosition, building.transform.position);
                if (distance <= healRadius)
                {
                    HealBuilding(building);
                }
            }
        }
        
        if (UnitManager.Instance != null)
        {
            foreach (UnitBase unit in UnitManager.Instance.AllyUnits)
            {
                if (unit == null) continue;
                if (!unit.gameObject.activeInHierarchy) continue;
                
                float distance = Vector2.Distance(stationPosition, unit.transform.position);
                if (distance <= healRadius)
                {
                    HealUnit(unit);
                }
            }
        }
    }
    
    private void HealBuilding(Damageable building)
    {
        building.Heal(healAmount);
    }
    
    private void HealUnit(UnitBase unit)
    {
        unit.Heal(healAmount);
    }
}
