using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RepairStation : Damageable
{
    [Header("Heal Settings")]
    [SerializeField] private float healInterval = 1f;
    [SerializeField] private float healRadius = 5f;
    [SerializeField] private int healAmount = 10;
    
    private Coroutine _healCoroutine;
    private WaitForSeconds _healWait;
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healRadius);
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        
        _healWait = new WaitForSeconds(healInterval);
        StartHealing();
    }
    
    protected override void OnDisable()
    {
        base.OnDisable();
        
        StopHealing();
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
            HealNearbyTargets();
            yield return _healWait;
        }
    }
    
    private void HealNearbyTargets()
    {
        Vector3 stationPosition = transform.position;
        
        // Heal nearby buildings (Damageable)
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
        
        // Heal nearby ally units (UnitBase)
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
