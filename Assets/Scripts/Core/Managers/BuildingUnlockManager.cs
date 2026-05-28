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
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        LoadUnlockedBuildings();
        UnlockAllBuildings();
    }

    private void UnlockAllBuildings()
    {
        BuildingData[] allBuildings = Resources.LoadAll<BuildingData>("Building Data");
        bool anyUnlocked = false;
        foreach (BuildingData building in allBuildings)
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
            SaveUnlockedBuildings(true);
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveUnlockedBuildings(true);
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveUnlockedBuildings(true);
        }
    }
    
    private void OnDestroy()
    {
        SaveUnlockedBuildings(true);
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
            SaveUnlockedBuildings(false);
            // Debug.Log($"BuildingUnlockManager: Unlocked building '{building.displayName}'");
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
            SaveUnlockedBuildings(true);
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
        PlayerPrefs.Save();
        Debug.Log($"BuildingUnlockManager: Unlocked all {allBuildings.Length} buildings (Debug mode)");
    }
    
    public void LockAllBuildings()
    {
        _unlockedBuildings.Clear();
        UpdateUnlockedBuildingsList();
        ClearSavedUnlockedBuildings();
        Debug.Log("BuildingUnlockManager: All buildings have been locked.");
    }
    
    private void SaveUnlockedBuildings(bool saveToDisk = false)
    {
        List<string> unlockedBuildingNames = new List<string>();
        foreach (BuildingData building in _unlockedBuildings)
        {
            if (building != null)
            {
                // Use name as unique identifier (ScriptableObject asset name)
                unlockedBuildingNames.Add(building.name);
            }
        }
        
        string savedData = string.Join(",", unlockedBuildingNames);
        PlayerPrefs.SetString("UnlockedBuildings", savedData);
        if (saveToDisk)
        {
            PlayerPrefs.Save();
        }
    }
    
    private void LoadUnlockedBuildings()
    {
        if (!PlayerPrefs.HasKey("UnlockedBuildings"))
        {
            return;
        }
        
        string savedData = PlayerPrefs.GetString("UnlockedBuildings");
        if (string.IsNullOrEmpty(savedData))
        {
            return;
        }
        
        string[] buildingNames = savedData.Split(',');
        BuildingData[] allBuildings = Resources.LoadAll<BuildingData>("Building Data");
        
        foreach (string buildingName in buildingNames)
        {
            if (string.IsNullOrEmpty(buildingName))
            {
                continue;
            }
            
            // Find building by name
            BuildingData building = System.Array.Find(allBuildings, b => b != null && b.name == buildingName);
            if (building != null)
            {
                _unlockedBuildings.Add(building);
            }
        }
        
        UpdateUnlockedBuildingsList();
    }
    
    private void ClearSavedUnlockedBuildings()
    {
        if (PlayerPrefs.HasKey("UnlockedBuildings"))
        {
            PlayerPrefs.DeleteKey("UnlockedBuildings");
            PlayerPrefs.Save();
        }
    }
}
