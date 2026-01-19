using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NightAttackManager : MonoBehaviour
{
    [Header("Night Attack Settings")]
    [SerializeField] private int numberOfEnemiesToActivate = 3;
    
    private void OnEnable()
    {
        DayNightCycleManager.OnNightStarted += HandleNightStarted;
        DayNightCycleManager.OnDayStarted += HandleDayStarted;
    }
    
    private void OnDisable()
    {
        DayNightCycleManager.OnNightStarted -= HandleNightStarted;
        DayNightCycleManager.OnDayStarted -= HandleDayStarted;
    }
    
    private void HandleNightStarted()
    {
        ActivateRandomEnemies();
    }
    
    private void HandleDayStarted()
    {
        DeactivateAllInfiniteAttackStates();
    }
    
    private void ActivateRandomEnemies()
    {
        if (UnitManager.Instance == null)
        {
            return;
        }
        
        List<EnemyUnitBase> enemyUnits = UnitManager.Instance.EnemyUnits
            .Where(u => u != null && u is EnemyUnitBase)
            .Cast<EnemyUnitBase>()
            .Where(e => !e.IsInInfiniteAttackState())
            .ToList();
        
        if (enemyUnits.Count == 0)
        {
            return;
        }
        
        // Get number of enemies to activate from DayNightCycleManager if available
        int countToActivate = numberOfEnemiesToActivate;
        if (DayNightCycleManager.Instance != null)
        {
            countToActivate = DayNightCycleManager.Instance.GetNumberOfEnemiesToActivate();
        }
        
        // Randomly select enemies
        int actualCount = Mathf.Min(countToActivate, enemyUnits.Count);
        List<EnemyUnitBase> selectedEnemies = new List<EnemyUnitBase>();
        
        for (int i = 0; i < actualCount; i++)
        {
            if (enemyUnits.Count == 0) break;
            
            int randomIndex = Random.Range(0, enemyUnits.Count);
            EnemyUnitBase selectedEnemy = enemyUnits[randomIndex];
            selectedEnemies.Add(selectedEnemy);
            enemyUnits.RemoveAt(randomIndex);
        }
        
        // Activate infinite attack state for selected enemies
        foreach (EnemyUnitBase enemy in selectedEnemies)
        {
            if (enemy != null)
            {
                enemy.ActivateInfiniteAttackState();
            }
        }
        
        Debug.Log($"[NightAttackManager] Activated infinite attack state for {selectedEnemies.Count} enemies.");
    }
    
    private void DeactivateAllInfiniteAttackStates()
    {
        if (UnitManager.Instance == null)
        {
            return;
        }
        
        List<EnemyUnitBase> enemyUnits = UnitManager.Instance.EnemyUnits
            .Where(u => u != null && u is EnemyUnitBase)
            .Cast<EnemyUnitBase>()
            .Where(e => e.IsInInfiniteAttackState())
            .ToList();
        
        foreach (EnemyUnitBase enemy in enemyUnits)
        {
            if (enemy != null)
            {
                enemy.DeactivateInfiniteAttackState();
            }
        }
        
        Debug.Log($"[NightAttackManager] Deactivated infinite attack state for {enemyUnits.Count} enemies.");
    }
    
    public void SetNumberOfEnemiesToActivate(int count)
    {
        numberOfEnemiesToActivate = count;
    }
}
