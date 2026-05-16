using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
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
    [Header("Starting Units Configuration")]
    [SerializeField] private StartingUnitConfig[] startingUnits;

    [Header("Additional Starting Units")]
    [Tooltip("Always merged into the spawn list (replaces former quest-based unlocks).")]
    [SerializeField] private StartingUnitConfig[] additionalStartingUnits;

    [Header("Spawn Settings")]
    [Tooltip("Maximum attempts to find a valid spawn position")]
    [SerializeField] private int maxSpawnAttempts = 10;

    private List<StartingUnitConfig> _allStartingUnits;
    public static StartingUnitsManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _allStartingUnits = new List<StartingUnitConfig>();
    }

    public void SpawnStartingUnits()
    {
        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        if (mainStructure == null) {
            Debug.LogWarning("StartingUnitsManager: MainStructure not found. Cannot spawn starting units.");
            return;
        }

        if (UnitManager.Instance == null || UnitManager.Instance.unitParent == null) {
            Debug.LogWarning("StartingUnitsManager: UnitManager or unitParent not found. Cannot spawn starting units.");
            return;
        }

        RebuildAllStartingUnits();

        Vector3 centerPosition = mainStructure.transform.position;

        if (_allStartingUnits == null || _allStartingUnits.Count == 0) {
            return;
        }

        StartCoroutine(SpawnAllUnitsSequentially(centerPosition));
    }

    private IEnumerator SpawnAllUnitsSequentially(Vector3 centerPosition)
    {
        foreach (StartingUnitConfig config in _allStartingUnits) {
            if (config == null || config.unitData == null || config.count <= 0) {
                continue;
            }
            for (int i = 0; i < config.count; i++) {
                Vector3 spawnPosition = GetSpawnPositionAroundMainStructure(centerPosition, config.spawnRadius);

                if (spawnPosition != Vector3.zero) {
                    GameObject unitPrefab = config.unitData.unitPrefab;
                    if (unitPrefab != null) {
                        Instantiate(unitPrefab, spawnPosition, Quaternion.identity, UnitManager.Instance.unitParent);
                    }
                    else {
                        Debug.LogWarning($"StartingUnitsManager: Unit prefab is null for {config.unitData.unitName}");
                    }
                }

                yield return new WaitForSeconds(config.spawnInterval);
            }
        }
    }

    private void RebuildAllStartingUnits()
    {
        _allStartingUnits.Clear();

        if (startingUnits != null) {
            foreach (StartingUnitConfig config in startingUnits) {
                if (config == null || config.unitData == null || config.count <= 0) {
                    continue;
                }

                StartingUnitConfig copy = new StartingUnitConfig {
                    unitData = config.unitData,
                    count = config.count,
                    spawnRadius = config.spawnRadius,
                    spawnInterval = config.spawnInterval
                };

                _allStartingUnits.Add(copy);
            }
        }

        if (additionalStartingUnits != null) {
            foreach (StartingUnitConfig extra in additionalStartingUnits) {
                if (extra == null || extra.unitData == null || extra.count <= 0) {
                    continue;
                }

                StartingUnitConfig existingConfig = _allStartingUnits.Find(config =>
                    config != null && config.unitData != null && config.unitData == extra.unitData);

                if (existingConfig != null) {
                    existingConfig.count += extra.count;
                }
                else {
                    _allStartingUnits.Add(new StartingUnitConfig {
                        unitData = extra.unitData,
                        count = extra.count,
                        spawnRadius = extra.spawnRadius,
                        spawnInterval = extra.spawnInterval
                    });
                }
            }
        }
    }

    private Vector3 GetSpawnPositionAroundMainStructure(Vector3 center, float radius)
    {
        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null) {
            Debug.LogWarning("StartingUnitsManager: BuildingManager or grid not found.");
            return Vector3.zero;
        }

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++) {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(0f, radius);

            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 worldPosition = center + direction * distance;

            Vector3Int cellPosition = BuildingManager.Instance.grid.WorldToCell(worldPosition);

            if (IsValidSpawnPosition(cellPosition)) {
                return BuildingManager.Instance.grid.GetCellCenterWorld(cellPosition);
            }
        }

        for (int i = 0; i < 8; i++) {
            float angle = i * 45f * Mathf.Deg2Rad;
            float distance = radius * 0.7f;

            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 worldPosition = center + direction * distance;
            Vector3Int cellPosition = BuildingManager.Instance.grid.WorldToCell(worldPosition);

            if (IsValidSpawnPosition(cellPosition)) {
                return BuildingManager.Instance.grid.GetCellCenterWorld(cellPosition);
            }
        }

        Debug.LogWarning($"StartingUnitsManager: Could not find valid spawn position after {maxSpawnAttempts} attempts. Using center position.");
        return center;
    }

    private bool IsValidSpawnPosition(Vector3Int cellPosition)
    {
        if (BuildingManager.Instance == null) return false;
        if (!BuildingManager.Instance.IsCellWalkable(cellPosition)) return false;
        if (FogOfWarManager.Instance != null && !FogOfWarManager.Instance.CanPlaceBuilding(cellPosition)) return false;
        return true;
    }

#if UNITY_EDITOR
    public void SpawnUnitsForEditor(UnitData unitData, int count = 1)
    {
        if (unitData == null || unitData.unitPrefab == null || count <= 0) return;

        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        if (mainStructure == null) return;

        if (UnitManager.Instance == null || UnitManager.Instance.unitParent == null) return;

        Vector3 centerPosition = mainStructure.transform.position;
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = GetSpawnPositionAroundMainStructure(centerPosition, 3f);
            Instantiate(unitData.unitPrefab, spawnPosition, Quaternion.identity, UnitManager.Instance.unitParent);
        }
    }
#endif
}
