using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class EnemySpawnData
{
    public GameObject enemyPrefab;
    [Header("Wave Budget")]
    public int cost = 10;
}

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private List<EnemySpawnData> enemyPrefabs = new List<EnemySpawnData>();
    [SerializeField] private int maxEnemiesPerHole = 7;
    [SerializeField] private float spawnRadiusOffset = 1f;

    [Header("Budget Formula (basePoints + noise*noiseAlpha) * (1 + time*timeBeta)")]
    [SerializeField] [Range(0f, 500f)] private float basePoints = 50f;
    [SerializeField] [Range(0f, 10f)] private float noiseAlpha = 1f;
    [SerializeField] [Range(0f, 1f)] private float timeBeta = 0.1f;

    [Header("Wave Budget Settings")]
    [SerializeField] private float noise100WaveCooldown = 30f;
    [SerializeField] private int noise100AreaCount = 3;
    [SerializeField] [Range(1f, 3f)] private float noise100BudgetMultiplier = 1.5f;

    private float _lastNoise100WaveTime = -1f;
    private bool _isWaveFromNoise100 = false;
    private bool _wasNoise100 = false;

    private void Start()
    {
        if (DayNightCycleManager.Instance != null)
        {
            DayNightCycleManager.OnNightStarted += OnNightStarted;
            DayNightCycleManager.OnDayStarted += OnDayStarted;
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
            DayNightCycleManager.OnDayStarted -= OnDayStarted;
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
            if (!_wasNoise100)
            {
                _wasNoise100 = true;
                float timeSinceLastWave = _lastNoise100WaveTime < 0f ? float.MaxValue : Time.time - _lastNoise100WaveTime;
                if (timeSinceLastWave >= noise100WaveCooldown)
                {
                    _isWaveFromNoise100 = true;
                    SpawnWaveFromBudget();
                    _lastNoise100WaveTime = Time.time;
                }
            }
        }
        else
        {
            _wasNoise100 = false;
        }
    }

    private void OnDayStarted()
    {
        if (UnitManager.Instance == null) return;

        List<UnitBase> enemies = new List<UnitBase>(UnitManager.Instance.EnemyUnits);
        foreach (UnitBase unit in enemies)
        {
            if (unit == null) continue;
            if (unit is Damageable damageable)
            {
                damageable.RestoreToFullHealth();
            }
            unit.gameObject.SetActive(false);
        }
    }

    private float CalculateWaveBudget(float multiplier = 1f)
    {
        float noise = NoiseManager.Instance != null ? NoiseManager.Instance.NoisePercentage : 0f;
        float totalElapsedHours = DayNightCycleManager.Instance != null ? DayNightCycleManager.Instance.GetTotalElapsedInGameHours() : 0f;
        float rawResult = (basePoints + (noise * noiseAlpha)) * (1f + (totalElapsedHours * timeBeta));
        float result = rawResult * multiplier;

        Debug.Log($"[EnemySpawner] Budget calc: basePoints={basePoints}, noise={noise}, totalElapsedInGameHours={totalElapsedHours}, \n"
            + $"noiseAlpha={noiseAlpha}, timeBeta={timeBeta}, rawResult={rawResult}, multiplier={multiplier}, finalBudget={result}");

        return result;
    }

    private void SpawnWaveFromBudget()
    {
        float multiplier = _isWaveFromNoise100 ? noise100BudgetMultiplier : 1f;
        float totalBudget = CalculateWaveBudget(multiplier);

        List<EnemySpawnData> validEnemies = enemyPrefabs
            .Where(e => e != null && e.enemyPrefab != null && e.cost > 0)
            .ToList();

        if (validEnemies.Count == 0) return;

        MapGenerator mapGenerator = GameManager.Instance.mapGenerator;
        if (mapGenerator == null) return;

        IReadOnlyList<Vector2Int> holes = mapGenerator.EnemySpawnHolePositions;
        if (holes == null || holes.Count == 0) return;

        List<Vector2Int> uniqueHoles = holes.Distinct().ToList();
        List<Vector2Int> selectedHoles;
        if (_isWaveFromNoise100)
        {
            List<Vector2Int> availableHoles = new List<Vector2Int>(uniqueHoles);
            selectedHoles = new List<Vector2Int>();
            int count = Mathf.Min(noise100AreaCount, availableHoles.Count);
            for (int i = 0; i < count && availableHoles.Count > 0; i++)
            {
                int idx = Random.Range(0, availableHoles.Count);
                selectedHoles.Add(availableHoles[idx]);
                availableHoles.RemoveAt(idx);
            }
        }
        else
        {
            selectedHoles = new List<Vector2Int>(uniqueHoles);
        }

        float budgetPerHole = selectedHoles.Count > 0 ? totalBudget / selectedHoles.Count : 0f;
        Debug.Log($"[EnemySpawner] Spawn wave: isNoise100={_isWaveFromNoise100}, totalBudget={totalBudget}, holes={selectedHoles.Count}, budgetPerHole={budgetPerHole}");

        if (BuildingManager.Instance == null) return;
        Grid grid = BuildingManager.Instance.grid;
        if (grid == null) return;
        Dictionary<int, Dictionary<string, int>> holeSpawnCounts = new Dictionary<int, Dictionary<string, int>>();

        for (int holeIdx = 0; holeIdx < selectedHoles.Count; holeIdx++)
        {
            Vector2Int holePos = selectedHoles[holeIdx];
            List<(EnemySpawnData data, Vector2Int pos)> holeSpawnList = new List<(EnemySpawnData, Vector2Int)>();
            float remainingBudget = budgetPerHole;

            while (remainingBudget > 0f)
            {
                List<EnemySpawnData> affordable = validEnemies.Where(e => e.cost <= remainingBudget).ToList();
                if (affordable.Count == 0) break;

                EnemySpawnData selectedEnemy = affordable[Random.Range(0, affordable.Count)];
                holeSpawnList.Add((selectedEnemy, holePos));
                remainingBudget -= selectedEnemy.cost;
            }

            holeSpawnCounts[holeIdx] = new Dictionary<string, int>();
            foreach (var spawn in holeSpawnList)
            {
                Vector3 center = mapGenerator.GetEnemySpawnHoleWorldPosition(spawn.pos);
                (float homeRadius, float territoryRadius) = mapGenerator.GetHoleRadiusValues(spawn.pos);

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

                        if (!holeSpawnCounts[holeIdx].ContainsKey(poolTag))
                            holeSpawnCounts[holeIdx][poolTag] = 0;
                        holeSpawnCounts[holeIdx][poolTag]++;
                    }
                }
            }
        }

        foreach (var kvp in holeSpawnCounts)
        {
            string counts = string.Join(", ", kvp.Value.Select(x => $"{x.Key} x{x.Value}"));
            Debug.Log($"[EnemySpawner] Hole {kvp.Key}: {counts}");
        }

        _isWaveFromNoise100 = false;
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
