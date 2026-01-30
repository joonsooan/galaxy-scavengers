using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Systems.Jobs;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class EnemySpawnData
{
    public GameObject enemyPrefab;
    [Range(0f, 100f)]
    public float spawnProbability = 50f;
    [Header("Wave Budget")]
    [Tooltip("Cost of this enemy unit in wave budget system")]
    public int cost = 10;
}

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private List<EnemySpawnData> enemyPrefabs = new List<EnemySpawnData>();
    [SerializeField] private int minEnemiesPerHole = 5;
    [SerializeField] private int maxEnemiesPerHole = 7;
    [SerializeField] private float spawnRadiusOffset = 1f;

    [Header("Wave Budget Settings")]
    [SerializeField] private float basePoints = 50f;
    [SerializeField] private float noiseAlpha = 1f;
    [SerializeField] private float timeBeta = 0.1f;
    [SerializeField] private float noise100TriggerDuration = 10f;
    [SerializeField] private float noise100WaveCooldown = 30f;

    private float _noise100StartTime = -1f;
    private float _lastNoise100WaveTime = -1f;
    private bool _isWaveFromNoise100 = false;

    private void Start()
    {
        if (DayNightCycleManager.Instance != null)
        {
            DayNightCycleManager.OnNightStarted += OnNightStarted;
        }

        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseChanged += OnNoiseChanged;
        }
    }

    private void OnDestroy()
    {
        if (DayNightCycleManager.Instance != null)
        {
            DayNightCycleManager.OnNightStarted -= OnNightStarted;
        }

        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseChanged -= OnNoiseChanged;
        }
    }

    private void OnNightStarted()
    {
        if (NoiseManager.Instance == null) return;

        float noisePercentage = NoiseManager.Instance.NoisePercentage;
        if (noisePercentage < 100f)
        {
            SpawnWaveFromBudget();
        }
    }

    private void OnNoiseChanged(float noisePercentage)
    {
        if (noisePercentage >= 100f)
        {
            if (_noise100StartTime < 0f)
            {
                _noise100StartTime = Time.time;
            }

            float timeAt100 = Time.time - _noise100StartTime;
            float timeSinceLastWave = _lastNoise100WaveTime < 0f ? float.MaxValue : Time.time - _lastNoise100WaveTime;

            if (timeAt100 >= noise100TriggerDuration && timeSinceLastWave >= noise100WaveCooldown)
            {
                _isWaveFromNoise100 = true;
                SpawnWaveFromBudget();
                _lastNoise100WaveTime = Time.time;
            }
        }
        else
        {
            _noise100StartTime = -1f;
        }
    }

    private float CalculateWaveBudget()
    {
        if (NoiseManager.Instance == null) return basePoints;

        float noise = NoiseManager.Instance.NoisePercentage;
        float time = DayNightCycleManager.Instance != null ? DayNightCycleManager.Instance.GetTime() / 60f : 0f;

        return (basePoints + (noise * noiseAlpha)) * (1f + (time * timeBeta));
    }

    private void SpawnWaveFromBudget()
    {
        float budget = CalculateWaveBudget();
        List<EnemySpawnData> validEnemies = enemyPrefabs
            .Where(e => e != null && e.enemyPrefab != null && e.cost > 0)
            .OrderBy(e => e.cost)
            .ToList();

        if (validEnemies.Count == 0) return;

        List<(EnemySpawnData data, Vector2Int holePos)> spawnList = new List<(EnemySpawnData, Vector2Int)>();
        MapGenerator mapGenerator = GameManager.Instance.mapGenerator;
        if (mapGenerator == null) return;

        IReadOnlyList<Vector2Int> holes = mapGenerator.EnemySpawnHolePositions;
        if (holes == null || holes.Count == 0) return;

        List<Vector2Int> selectedHoles = new List<Vector2Int>();
        int holesToSelect = Mathf.Max(1, Mathf.RoundToInt(holes.Count * 0.5f));
        List<Vector2Int> availableHoles = new List<Vector2Int>(holes);

        for (int i = 0; i < holesToSelect && availableHoles.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, availableHoles.Count);
            selectedHoles.Add(availableHoles[randomIndex]);
            availableHoles.RemoveAt(randomIndex);
        }

        float remainingBudget = budget;
        Grid grid = BuildingManager.Instance.grid;

        while (remainingBudget > 0f && selectedHoles.Count > 0)
        {
            EnemySpawnData selectedEnemy = null;
            foreach (EnemySpawnData enemy in validEnemies)
            {
                if (enemy.cost <= remainingBudget)
                {
                    selectedEnemy = enemy;
                    break;
                }
            }

            if (selectedEnemy == null) break;

            int holeIndex = Random.Range(0, selectedHoles.Count);
            Vector2Int holePos = selectedHoles[holeIndex];
            spawnList.Add((selectedEnemy, holePos));
            remainingBudget -= selectedEnemy.cost;
        }

        foreach (var spawn in spawnList)
        {
            Vector3 center = mapGenerator.GetEnemySpawnHoleWorldPosition(spawn.holePos);
            (float homeRadius, float territoryRadius) = mapGenerator.GetHoleRadiusValues(spawn.holePos);

            if (homeRadius <= 0f || territoryRadius <= 0f) continue;

            Vector3 spawnPos = FindValidSpawnPosition(center, spawnRadiusOffset, grid);
            if (spawnPos != Vector3.zero && center != Vector3.zero)
            {
                string poolTag = GetPoolTagFromPrefab(spawn.data.enemyPrefab);
                GameObject enemy = ObjectPooler.Instance.SpawnFromPool(poolTag, spawnPos, Quaternion.identity);
                if (enemy != null)
                {
                    EnemyUnitBase enemyScript = enemy.GetComponent<EnemyUnitBase>();
                    if (enemyScript != null)
                    {
                        enemyScript.SetTerritoryCenter(center, homeRadius, territoryRadius);
                        if (_isWaveFromNoise100)
                        {
                            enemyScript.ActivateInfiniteAttackState();
                        }
                        if (TutorialManager.Instance != null)
                        {
                            TutorialManager.Instance.RegisterSpawnedEnemy(enemyScript);
                        }
                    }
                }
            }
        }

        _isWaveFromNoise100 = false;
    }

    public void SpawnEnemies()
    {
        MapGenerator mapGenerator = GameManager.Instance.mapGenerator;
        IReadOnlyList<Vector2Int> holes = mapGenerator.EnemySpawnHolePositions;
        if (holes == null || holes.Count == 0) {
            return;
        }

        Grid grid = BuildingManager.Instance.grid;

        for (int i = 0; i < holes.Count; i++) {
            Vector2Int holePos = holes[i];
            Vector3 center = mapGenerator.GetEnemySpawnHoleWorldPosition(holePos);
            (float homeRadius, float territoryRadius) = mapGenerator.GetHoleRadiusValues(holePos);

            if (homeRadius <= 0f || territoryRadius <= 0f) {
                continue;
            }

            int enemiesPerHole = Random.Range(minEnemiesPerHole, maxEnemiesPerHole);

            for (int j = 0; j < enemiesPerHole; j++) {
                GameObject selectedPrefab = SelectEnemyPrefab();
                if (selectedPrefab == null) {
                    continue;
                }

                Vector3 spawnPos = FindValidSpawnPosition(center, spawnRadiusOffset, grid);
                if (spawnPos != Vector3.zero && center != Vector3.zero) {
                    string poolTag = GetPoolTagFromPrefab(selectedPrefab);
                    GameObject enemy = ObjectPooler.Instance.SpawnFromPool(poolTag, spawnPos, Quaternion.identity);
                    if (enemy != null) {
                        EnemyUnitBase enemyScript = enemy.GetComponent<EnemyUnitBase>();
                        if (enemyScript != null) {
                            enemyScript.SetTerritoryCenter(center, homeRadius, territoryRadius);
                            if (TutorialManager.Instance != null)
                            {
                                TutorialManager.Instance.RegisterSpawnedEnemy(enemyScript);
                            }
                        }
                    }
                }
            }
        }
    }

    public IEnumerator SpawnEnemiesAsync(IInitializationProgress progress = null)
    {
        MapGenerator mapGenerator = GameManager.Instance.mapGenerator;
        IReadOnlyList<Vector2Int> holes = mapGenerator.EnemySpawnHolePositions;
        if (holes == null || holes.Count == 0) {
            yield break;
        }

        Grid grid = BuildingManager.Instance.grid;
        int totalHoles = holes.Count;

        progress.UpdateProgress(0.0f, "적대적 생명체 탐색 중...");
        yield return CoroutineCache.GetWaitForSeconds(0.5f);

        for (int i = 0; i < totalHoles; i++) {
            Vector2Int holePos = holes[i];
            Vector3 center = mapGenerator.GetEnemySpawnHoleWorldPosition(holePos);
            (float homeRadius, float territoryRadius) = mapGenerator.GetHoleRadiusValues(holePos);

            if (homeRadius > 0f && territoryRadius > 0f) {
                int enemiesPerHole = Random.Range(minEnemiesPerHole, maxEnemiesPerHole);

                for (int j = 0; j < enemiesPerHole; j++) {
                    GameObject selectedPrefab = SelectEnemyPrefab();
                    if (selectedPrefab == null) {
                        continue;
                    }

                    Vector3 spawnPos = FindValidSpawnPosition(center, spawnRadiusOffset, grid);
                    if (spawnPos != Vector3.zero && center != Vector3.zero) {
                    string poolTag = GetPoolTagFromPrefab(selectedPrefab);
                    GameObject enemy = ObjectPooler.Instance.SpawnFromPool(poolTag, spawnPos, Quaternion.identity);
                    if (enemy != null) {
                        EnemyUnitBase enemyScript = enemy.GetComponent<EnemyUnitBase>();
                        if (enemyScript != null) {
                            enemyScript.SetTerritoryCenter(center, homeRadius, territoryRadius);
                            if (TutorialManager.Instance != null)
                            {
                                TutorialManager.Instance.RegisterSpawnedEnemy(enemyScript);
                            }
                        }
                    }
                    }
                }
            }

            if (i % 2 == 0) {
                yield return null;
            }
        }
    }

    private GameObject SelectEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0) {
            return null;
        }

        List<EnemySpawnData> validEnemies = enemyPrefabs.Where(e => e != null && e.enemyPrefab != null && e.spawnProbability > 0f).ToList();
        if (validEnemies.Count == 0) {
            return null;
        }

        float totalProbability = validEnemies.Sum(e => e.spawnProbability);
        if (totalProbability <= 0f) {
            return validEnemies[0].enemyPrefab;
        }

        float randomValue = Random.Range(0f, totalProbability);
        float cumulativeProbability = 0f;

        foreach (EnemySpawnData enemyData in validEnemies) {
            cumulativeProbability += enemyData.spawnProbability;
            if (randomValue <= cumulativeProbability) {
                return enemyData.enemyPrefab;
            }
        }

        return validEnemies[validEnemies.Count - 1].enemyPrefab;
    }

    public static string GetPoolTagFromPrefab(GameObject prefab)
    {
        if (prefab == null) {
            return string.Empty;
        }

        EnemyUnitBase enemyUnit = prefab.GetComponent<EnemyUnitBase>();
        if (enemyUnit == null) {
            return string.Empty;
        }

        if (enemyUnit is Unit_Enemy_0) {
        }
        else if (enemyUnit is Unit_Enemy_1) {
            return "Enemy_1";
        }
        else if (enemyUnit is Unit_Enemy_2) {
            return "Enemy_2";
        }

        return "Enemy_0";
    }

    public List<EnemySpawnData> GetEnemyPrefabs()
    {
        return enemyPrefabs;
    }

    public int GetMaxEnemiesPerHole()
    {
        return maxEnemiesPerHole;
    }

    private Vector3 FindValidSpawnPosition(Vector3 center, float maxRadius, Grid grid)
    {
        int maxAttempts = 50;

        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            Vector2 offset = Random.insideUnitCircle * maxRadius;
            Vector3 candidatePos = new Vector3(center.x + offset.x, center.y + offset.y, center.z);
            Vector3Int cell = grid.WorldToCell(candidatePos);

            if (!BuildingManager.Instance.IsResourceTile(cell)) {
                return grid.GetCellCenterWorld(cell);
            }
        }

        for (int radius = 1; radius <= Mathf.CeilToInt(maxRadius); radius++) {
            for (int x = -radius; x <= radius; x++) {
                for (int y = -radius; y <= radius; y++) {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;

                    Vector3Int testCell = grid.WorldToCell(center) + new Vector3Int(x, y, 0);
                    float distance = Vector3.Distance(center, grid.GetCellCenterWorld(testCell));

                    if (distance <= maxRadius && !BuildingManager.Instance.IsResourceTile(testCell)) {
                        return grid.GetCellCenterWorld(testCell);
                    }
                }
            }
        }

        return center;
    }
}
