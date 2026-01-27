using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
    public static UnitManager Instance { get; private set; }

    public static event Action<ResourceType[]> OnMineableTypesChanged;
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

    public int GetMaxPopulation()
    {
        int maxPop = baseMaxPopulation;
        
        if (CoreRepairManager.Instance != null && !CoreRepairManager.Instance.IsPartRepaired(CorePart.Controller))
        {
            CorePartData controllerData = CoreRepairManager.Instance.GetPartData(CorePart.Controller);
            if (controllerData != null)
            {
                maxPop = Mathf.Max(1, Mathf.RoundToInt(baseMaxPopulation * (1f - controllerData.debuffValue)));
            }
        }
        
        return maxPop;
    }

    public bool CanSpawnUnit()
    {
        return _allyUnits.Count < GetMaxPopulation();
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
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive())
            {
                unit.gameObject.SetActive(false);
            }
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
}