using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StartingUnitConfig
{
    [Header("Unit Configuration")]
    public UnitData unitData;
    public int count = 1;
    
    [Header("Spawn Settings")]
    [Tooltip("Radius around MainStructure to spawn units")]
    public float spawnRadius = 3f;
    
    [Tooltip("Interval between each unit spawn (seconds)")]
    public float spawnInterval = 0.5f;
}

public class StartingUnitsManager : MonoBehaviour
{
    public static StartingUnitsManager Instance { get; private set; }
    
    [Header("Starting Units Configuration")]
    [SerializeField] private StartingUnitConfig[] startingUnits;
    
    [Header("Spawn Settings")]
    [Tooltip("Maximum attempts to find a valid spawn position")]
    [SerializeField] private int maxSpawnAttempts = 10;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }
    
    public void SpawnStartingUnits()
    {
        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        if (mainStructure == null)
        {
            Debug.LogWarning("StartingUnitsManager: MainStructure not found. Cannot spawn starting units.");
            return;
        }
        
        if (UnitManager.Instance == null || UnitManager.Instance.unitParent == null)
        {
            Debug.LogWarning("StartingUnitsManager: UnitManager or unitParent not found. Cannot spawn starting units.");
            return;
        }
        
        Vector3 centerPosition = mainStructure.transform.position;
        
        if (startingUnits == null || startingUnits.Length == 0)
        {
            Debug.Log("StartingUnitsManager: No starting units configured.");
            return;
        }
        
        foreach (StartingUnitConfig config in startingUnits)
        {
            if (config == null || config.unitData == null || config.count <= 0)
            {
                continue;
            }
            
            StartCoroutine(SpawnUnitsWithInterval(config, centerPosition));
        }
    }
    
    private IEnumerator SpawnUnitsWithInterval(StartingUnitConfig config, Vector3 centerPosition)
    {
        for (int i = 0; i < config.count; i++)
        {
            Vector3 spawnPosition = GetSpawnPositionAroundMainStructure(centerPosition, config.spawnRadius);
            
            if (spawnPosition != Vector3.zero)
            {
                GameObject unitPrefab = config.unitData.unitPrefab;
                if (unitPrefab != null)
                {
                    Instantiate(unitPrefab, spawnPosition, Quaternion.identity, UnitManager.Instance.unitParent);
                }
                else
                {
                    Debug.LogWarning($"StartingUnitsManager: Unit prefab is null for {config.unitData.unitName}");
                }
            }
            
            if (i < config.count - 1)
            {
                yield return new WaitForSeconds(config.spawnInterval);
            }
        }
    }
    
    private Vector3 GetSpawnPositionAroundMainStructure(Vector3 center, float radius)
    {
        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null)
        {
            Debug.LogWarning("StartingUnitsManager: BuildingManager or grid not found.");
            return Vector3.zero;
        }
        
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Generate random angle and distance
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(0f, radius);
            
            // Calculate position
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 worldPosition = center + direction * distance;
            
            // Convert to grid cell
            Vector3Int cellPosition = BuildingManager.Instance.grid.WorldToCell(worldPosition);
            
            // Check if position is walkable
            if (IsValidSpawnPosition(cellPosition))
            {
                return BuildingManager.Instance.grid.GetCellCenterWorld(cellPosition);
            }
        }
        
        // Fallback: try positions at fixed angles
        for (int i = 0; i < 8; i++)
        {
            float angle = (i * 45f) * Mathf.Deg2Rad;
            float distance = radius * 0.7f; // Use 70% of radius for fallback
            
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 worldPosition = center + direction * distance;
            Vector3Int cellPosition = BuildingManager.Instance.grid.WorldToCell(worldPosition);
            
            if (IsValidSpawnPosition(cellPosition))
            {
                return BuildingManager.Instance.grid.GetCellCenterWorld(cellPosition);
            }
        }
        
        Debug.LogWarning($"StartingUnitsManager: Could not find valid spawn position after {maxSpawnAttempts} attempts. Using center position.");
        return center;
    }
    
    private bool IsValidSpawnPosition(Vector3Int cellPosition)
    {
        if (BuildingManager.Instance == null)
        {
            return false;
        }
        
        // Check if cell is walkable (not a building tile, not a resource tile, can place building)
        if (BuildingManager.Instance.IsBuildingTile(cellPosition) ||
            BuildingManager.Instance.IsResourceTile(cellPosition))
        {
            return false;
        }
        
        // Check if we can place a building here (indicates walkable terrain)
        if (!BuildingManager.Instance.CanPlaceBuilding(cellPosition))
        {
            return false;
        }
        
        // Check if there's already a building at this position
        if (BuildingManager.Instance.GetBuildingAt(cellPosition, out _))
        {
            return false;
        }
        
        return true;
    }
}
