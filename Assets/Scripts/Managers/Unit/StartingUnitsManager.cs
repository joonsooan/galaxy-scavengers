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

[Serializable]
public class QuestUnitMapping
{
    [Header("Quest Configuration")]
    public int questId;

    [Header("Unit to Add")]
    public UnitData unitData;
    public int count = 1;
    public float spawnRadius = 3f;
    public float spawnInterval = 0.5f;
}

public class StartingUnitsManager : MonoBehaviour
{
    [Header("Starting Units Configuration")]
    [SerializeField] private StartingUnitConfig[] startingUnits;

    [Header("Quest-Based Unit Unlocks")]
    [SerializeField] private QuestUnitMapping[] questUnitMappings;

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

    private void Start()
    {
        StartCoroutine(SubscribeToQuestDataManagerWhenReady());
    }

    private void OnEnable()
    {
        if (QuestDataManager.Instance != null) {
            QuestDataManager.Instance.OnQuestStateChanged += OnQuestStateChanged;
        }
    }

    private void OnDisable()
    {
        if (QuestDataManager.Instance != null) {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChanged;
        }
    }

    private IEnumerator SubscribeToQuestDataManagerWhenReady()
    {
        while (QuestDataManager.Instance == null) {
            yield return null;
        }

        foreach (QuestUnitMapping mapping in questUnitMappings) {
            if (mapping != null && QuestDataManager.Instance.IsQuestCompleted(mapping.questId)) {
                AddUnitFromQuest(mapping);
            }
        }
    }

    private void OnQuestStateChanged(int questId)
    {
        if (QuestDataManager.Instance == null) {
            return;
        }

        QuestState state = QuestDataManager.Instance.GetQuestState(questId);
        if (state != QuestState.Completed) {
            return;
        }

        foreach (QuestUnitMapping mapping in questUnitMappings) {
            if (mapping != null && mapping.questId == questId) {
                AddUnitFromQuest(mapping);
            }
        }
    }

    private void AddUnitFromQuest(QuestUnitMapping mapping)
    {
        if (mapping == null || mapping.unitData == null || mapping.count <= 0) {
            return;
        }

        StartingUnitConfig existingConfig = _allStartingUnits.Find(config =>
            config != null && config.unitData != null && config.unitData == mapping.unitData);

        if (existingConfig != null) {
            existingConfig.count += mapping.count;
        }
        else {
            StartingUnitConfig newConfig = new StartingUnitConfig {
                unitData = mapping.unitData,
                count = mapping.count,
                spawnRadius = mapping.spawnRadius,
                spawnInterval = mapping.spawnInterval
            };
            _allStartingUnits.Add(newConfig);
        }
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
            // Debug.Log("StartingUnitsManager: No starting units configured.");
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
            if (IsTutorialMode() && config.unitData.unitPrefab != null && config.unitData.unitPrefab.GetComponent<Unit_Construct>() != null) {
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

        if (QuestDataManager.Instance != null && questUnitMappings != null) {
            foreach (QuestUnitMapping mapping in questUnitMappings) {
                if (mapping != null && QuestDataManager.Instance.IsQuestCompleted(mapping.questId)) {
                    AddUnitFromQuest(mapping);
                }
            }
        }

    }

    private static bool IsTutorialMode()
    {
        return TutorialManager.Instance != null &&
            (TutorialManager.Instance.IsTutorialActive() || TutorialManager.Instance.ShouldStartTutorial());
    }

    private Vector3 GetSpawnPositionAroundMainStructure(Vector3 center, float radius)
    {
        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null) {
            Debug.LogWarning("StartingUnitsManager: BuildingManager or grid not found.");
            return Vector3.zero;
        }

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++) {
            // Generate random angle and distance
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(0f, radius);

            // Calculate position
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 worldPosition = center + direction * distance;

            // Convert to grid cell
            Vector3Int cellPosition = BuildingManager.Instance.grid.WorldToCell(worldPosition);

            // Check if position is walkable
            if (IsValidSpawnPosition(cellPosition)) {
                return BuildingManager.Instance.grid.GetCellCenterWorld(cellPosition);
            }
        }

        // Fallback: try positions at fixed angles
        for (int i = 0; i < 8; i++) {
            float angle = i * 45f * Mathf.Deg2Rad;
            float distance = radius * 0.7f; // Use 70% of radius for fallback

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
