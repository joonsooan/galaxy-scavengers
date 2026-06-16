using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuildingUnlockManager : MonoBehaviour
{
    public static BuildingUnlockManager Instance { get; private set; }

    private readonly HashSet<BuildingData> _unlockedBuildings = new HashSet<BuildingData>();

    [Header("Debug Info")]
    [SerializeField] private List<BuildingData> unlockedBuildingsList = new List<BuildingData>();

    public event Action<BuildingData> OnBuildingUnlocked;

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

    private void OnValidate()
    {
        UpdateUnlockedBuildingsList();
    }

    public bool IsBuildingUnlocked(BuildingData building)
    {
        if (building == null) return false;
        return _unlockedBuildings.Contains(building);
    }

    public void UnlockBuilding(BuildingData building)
    {
        if (building == null) return;

        if (!_unlockedBuildings.Contains(building))
        {
            _unlockedBuildings.Add(building);
            UpdateUnlockedBuildingsList();
            OnBuildingUnlocked?.Invoke(building);
        }
    }

    public void UnlockBuildings(BuildingData[] buildings)
    {
        if (buildings == null) return;

        bool anyUnlocked = false;
        foreach (BuildingData building in buildings)
        {
            if (building != null && !_unlockedBuildings.Contains(building))
            {
                _unlockedBuildings.Add(building);
                OnBuildingUnlocked?.Invoke(building);
                anyUnlocked = true;
            }
        }

        if (anyUnlocked)
        {
            UpdateUnlockedBuildingsList();
        }
    }

    public HashSet<BuildingData> GetUnlockedBuildings()
    {
        return new HashSet<BuildingData>(_unlockedBuildings);
    }

    public List<BuildingData> GetUnlockedBuildingsList()
    {
        return new List<BuildingData>(_unlockedBuildings);
    }

    private void UpdateUnlockedBuildingsList()
    {
        unlockedBuildingsList.Clear();
        unlockedBuildingsList.AddRange(_unlockedBuildings);
    }

    [ContextMenu("Log Unlocked Buildings")]
    public void LogUnlockedBuildings()
    {
        if (_unlockedBuildings.Count == 0)
        {
            Debug.Log("BuildingUnlockManager: No buildings are currently unlocked.");
            return;
        }

        Debug.Log($"BuildingUnlockManager: Currently unlocked buildings ({_unlockedBuildings.Count}):");
        foreach (BuildingData building in _unlockedBuildings.OrderBy(b => b.displayName))
        {
            Debug.Log($"  - {building.displayName} (ID: {building.name})");
        }
    }

    [ContextMenu("Unlock All Buildings (Debug)")]
    public void UnlockAllBuildingsDebug()
    {
        BuildingData[] allBuildings = Resources.LoadAll<BuildingData>("Building Data");
        foreach (BuildingData building in allBuildings)
        {
            UnlockBuilding(building);
        }

        Debug.Log($"BuildingUnlockManager: Unlocked all {allBuildings.Length} buildings (Debug mode)");
    }

    public void LockAllBuildings()
    {
        _unlockedBuildings.Clear();
        UpdateUnlockedBuildingsList();
        Debug.Log("BuildingUnlockManager: All buildings have been locked.");
    }
}
