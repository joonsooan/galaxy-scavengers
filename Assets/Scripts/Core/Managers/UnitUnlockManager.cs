using System;
using System.Collections.Generic;
using UnityEngine;

public class UnitUnlockManager : MonoBehaviour
{
    public static UnitUnlockManager Instance { get; private set; }

    private readonly HashSet<UnitData> _unlockedUnits = new HashSet<UnitData>();

    public event Action<UnitData> OnUnitUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool IsUnitUnlocked(UnitData unit)
    {
        if (unit == null)
        {
            return false;
        }

        return _unlockedUnits.Contains(unit);
    }

    public void UnlockUnit(UnitData unit)
    {
        if (unit == null)
        {
            return;
        }

        if (_unlockedUnits.Contains(unit))
        {
            return;
        }

        _unlockedUnits.Add(unit);
        OnUnitUnlocked?.Invoke(unit);
    }
}
