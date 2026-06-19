using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
    public static UnitManager Instance { get; private set; }

    public static event Action<ResourceType[]> OnMineableTypesChanged;
    public static event Action<EnemyUnitBase> OnEnemyUnitRemoved;
    public IReadOnlyList<ResourceType> CurrentMineableTypes => _currentMineableTypes;
    public Transform unitParent;
    
    private List<ResourceType> _currentMineableTypes = new();
    
    public IReadOnlyList<UnitBase> EnemyUnits => _enemyUnits;
    public IReadOnlyList<UnitBase> AllyUnits => _allyUnits;

    [Header("Population Settings")]
    [SerializeField] private int baseMaxPopulation = 50;

    private readonly List<UnitBase> _enemyUnits = new();
    private readonly List<UnitBase> _allyUnits = new();
    
    public static event Action<UnitBase> OnUnitCountChanged;
    public static event Action OnMaxPopulationBonusChanged;

    public int GetMaxPopulation()
    {
        int bonus = 0;
        if (UnitUpgradeProgress.Instance != null)
            bonus = UnitUpgradeProgress.Instance.GetMaxPopulationBonus();

        int techBonus = 0;
        if (ModuleEffectManager.Instance != null)
            techBonus = Mathf.RoundToInt(ModuleEffectManager.Instance.GetStatModifier(ModuleStatType.MaxPopulation));

        return baseMaxPopulation + bonus + techBonus;
    }

    public static void NotifyMaxPopulationBonusChanged()
    {
        OnMaxPopulationBonusChanged?.Invoke();
    }

    public int GetPopulationCountedAllyCount()
    {
        int count = 0;
        foreach (UnitBase ally in _allyUnits)
        {
            if (ally == null || ally is Unit_Player)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    public bool CanSpawnUnit()
    {
        return GetPopulationCountedAllyCount() < GetMaxPopulation();
    }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        _currentMineableTypes = ((ResourceType[])Enum.GetValues(typeof(ResourceType))).ToList();
    }
    
    private void Start()
    {
        RegisterExistingUnits();
    }

    private void Update()
    {
        UnitMovement.ProcessPathfindingQueues();
    }
    
    private void RegisterExistingUnits()
    {
        if (unitParent == null) return;
        
        UnitBase[] existingUnits = unitParent.GetComponentsInChildren<UnitBase>(true);
        
        foreach (UnitBase unit in existingUnits)
        {
            if (unit != null)
            {
                AddUnit(unit);
            }
        }
    }

    public void UpdateAllLifterMineableTypes(List<ResourceType> newTypes)
    {
        _currentMineableTypes = new List<ResourceType>(newTypes);

        OnMineableTypesChanged?.Invoke(newTypes.ToArray());
    }
    
    public void AddUnit(UnitBase unit)
    {
        if (unit.unitType == UnitBase.UnitType.Enemy && !_enemyUnits.Contains(unit))
        {
            _enemyUnits.Add(unit);
        }
        else if (unit.unitType == UnitBase.UnitType.Ally && !_allyUnits.Contains(unit))
        {
            if (!CanSpawnUnit())
            {
                if (unit.gameObject != null)
                {
                    Destroy(unit.gameObject);
                }
                return;
            }
            _allyUnits.Add(unit);
        }
        OnUnitCountChanged?.Invoke(unit);
    }

    public void RemoveUnit(UnitBase unit)
    {
        if (unit.unitType == UnitBase.UnitType.Enemy)
        {
            if (unit is EnemyUnitBase enemyUnit)
            {
                OnEnemyUnitRemoved?.Invoke(enemyUnit);
            }

            _enemyUnits.Remove(unit);
        }
        else if (unit.unitType == UnitBase.UnitType.Ally)
        {
            _allyUnits.Remove(unit);
        }
        OnUnitCountChanged?.Invoke(unit);
    }

    public void RemoveAllUnits()
    {
        List<UnitBase> allUnits = new List<UnitBase>();
        allUnits.AddRange(_allyUnits);
        allUnits.AddRange(_enemyUnits);

        foreach (UnitBase unit in allUnits)
        {
            if (unit != null && unit.gameObject != null)
            {
                Destroy(unit.gameObject);
            }
        }

        _allyUnits.Clear();
        _enemyUnits.Clear();
    }

    public List<UnitBase> GetAllyUnitsInArea(Vector3 center, float radius)
    {
        List<UnitBase> results = new List<UnitBase>();
        float radiusSquared = radius * radius;

        foreach (UnitBase unit in _allyUnits)
        {
            if (unit == null) continue;
            float distSquared = (center - unit.transform.position).sqrMagnitude;
            if (distSquared <= radiusSquared)
            {
                results.Add(unit);
            }
        }

        return results;
    }
}