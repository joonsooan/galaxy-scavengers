using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

[System.Serializable]
public class EnemySpawnData
{
    public GameObject enemyPrefab;
    [Header("Wave Budget")]
    public int cost = 10;

    [Header("Enhancement")]
    public int enhancementCost = 15;
    [Range(0f, 100f)] public float moveSpeedBonusPercent = 20f;
    [Range(0f, 100f)] public float attackBonusPercent = 20f;
    [Range(0f, 100f)] public float healthBonusPercent = 20f;
    public Color enhancedSpriteColor = new Color(1f, 0.8f, 0.4f);
}

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private List<EnemySpawnData> enemyPrefabs = new List<EnemySpawnData>();
    [SerializeField] private int maxEnemiesPerHole = 7;
    [SerializeField] private float spawnRadiusOffset = 1f;

    [Header("Initial Spawn")]
    [SerializeField][Min(0f)] private float initialBudgetPerZone = 15f;

    [Header("Noise Delta Spawning")]
    [Tooltip("노이즈 1% 상승 시 스폰에 사용할 예산")]
    [SerializeField][Min(0f)] private float noiseSpawnBudgetPerPercent = 0.5f;

    [Header("Death Respawn")]
    [SerializeField][Min(0.5f)] private float enemyDeathRespawnDelaySeconds = 12f;

    [Header("Attack Activation (Noise Zone)")]
    [SerializeField][Min(0.1f)] private float attackActivationIntervalSeconds = 6f;
    [SerializeField][Range(0f, 100f)] private float noiseCautionBudgetMultiplierPercent = 10f;
    [SerializeField][Range(0f, 100f)] private float noiseWarningBudgetMultiplierPercent = 20f;

    private bool _wasNoise100;
    private bool _isSubscribedToEvents;
    private float _lastNoisePercentageForDelta = -1f;
    private float _pendingNoiseBudget = 0f;

    private readonly Dictionary<Vector2Int, int> _holeCurrentCount = new Dictionary<Vector2Int, int>();
    private readonly List<(Vector2Int hole, float respawnAt)> _deathRespawnQueue =
        new List<(Vector2Int, float)>();
    private Coroutine _deathRespawnCoroutine;

    private Coroutine _attackActivationCoroutine;
    private NoiseManager.NoiseZone _activeAttackZone = NoiseManager.NoiseZone.Safe;
    private float _activeAttackMultiplier;

    private void OnEnable()
    {
        if (_isSubscribedToEvents) return;

        GameManager.OnGameSceneInitialized += OnGameSceneInitialized;
        DayNightCycleManager.OnDayStarted += OnDayStarted;
        UnitManager.OnEnemyUnitRemoved -= HandleEnemyUnitRemoved;
        UnitManager.OnEnemyUnitRemoved += HandleEnemyUnitRemoved;

        _isSubscribedToEvents = true;

        if (_deathRespawnCoroutine == null)
            _deathRespawnCoroutine = StartCoroutine(DeathRespawnRoutine());

        if (GameManager.Instance != null && GameManager.Instance.IsGameSceneInitialized)
            OnGameSceneInitialized();
    }

    private void OnDisable()
    {
        UnitManager.OnEnemyUnitRemoved -= HandleEnemyUnitRemoved;

        if (_deathRespawnCoroutine != null)
        {
            StopCoroutine(_deathRespawnCoroutine);
            _deathRespawnCoroutine = null;
        }

        StopAttackActivationCoroutine();

        if (!_isSubscribedToEvents) return;

        GameManager.OnGameSceneInitialized -= OnGameSceneInitialized;
        DayNightCycleManager.OnDayStarted -= OnDayStarted;
        if (NoiseManager.Instance != null) NoiseManager.Instance.OnNoiseChanged -= OnNoiseChanged;
        _isSubscribedToEvents = false;
    }

    private void OnGameSceneInitialized()
    {
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseChanged -= OnNoiseChanged;
            NoiseManager.Instance.OnNoiseChanged += OnNoiseChanged;
            _lastNoisePercentageForDelta = NoiseManager.Instance.NoisePercentage;
        }

        if (!ShouldBlockSpawnsForTutorial())
            InitialSpawnAllZones();

        SyncAttackActivationToCurrentNoise();
    }

    private void InitialSpawnAllZones()
    {
        MapGenerator mapGenerator = GameManager.Instance != null ? GameManager.Instance.mapGenerator : null;

        IReadOnlyList<Vector2Int> holes = mapGenerator.EnemySpawnHolePositions;

        float noisePct = NoiseManager.Instance != null ? NoiseManager.Instance.NoisePercentage : 0f;
        int costCeiling = GetCostCeilingForNoise(noisePct);

        foreach (Vector2Int hole in holes.Distinct())
        {
            _holeCurrentCount[hole] = 0;
            int spawned = SpawnInZone(hole, initialBudgetPerZone, costCeiling);
        }

        LogEnemyStats();
    }

    private void OnNoiseChanged(float noisePercentage)
    {
        if (ShouldBlockSpawnsForTutorial())
        {
            _lastNoisePercentageForDelta = noisePercentage;
            StopAttackActivationCoroutine();
            return;
        }

        if (noisePercentage >= 100f)
        {
            if (!_wasNoise100)
            {
                _wasNoise100 = true;
                _pendingNoiseBudget = 0f;
                TriggerNoise100Emergency();
            }
        }
        else
        {
            _wasNoise100 = false;
            // 노이즈 감소 시 누적 예산 리셋
            if (_lastNoisePercentageForDelta >= 0f && noisePercentage < _lastNoisePercentageForDelta - 0.0001f)
                _pendingNoiseBudget = 0f;
        }

        if (_lastNoisePercentageForDelta >= 0f && noisePercentage > _lastNoisePercentageForDelta + 0.0001f)
        {
            float delta = noisePercentage - _lastNoisePercentageForDelta;
            _pendingNoiseBudget += delta * noiseSpawnBudgetPerPercent;

            int costCeiling = GetCostCeilingForNoise(noisePercentage);

            if (_pendingNoiseBudget >= costCeiling)
            {
                SpawnNoiseWave(_pendingNoiseBudget, costCeiling);
                _pendingNoiseBudget = 0f;
            }
        }

        _lastNoisePercentageForDelta = noisePercentage;
        SyncAttackActivationToCurrentNoise();
    }

    private void TriggerNoise100Emergency()
    {
        MapGenerator mapGenerator = GameManager.Instance != null ? GameManager.Instance.mapGenerator : null;
        if (mapGenerator == null) return;

        IReadOnlyList<Vector2Int> holes = mapGenerator.EnemySpawnHolePositions;
        if (holes == null || holes.Count == 0) return;

        int costCeiling = GetCostCeilingForNoise(100f);
        int maxCost = GetMaxEnemyCost();

        foreach (Vector2Int hole in holes.Distinct())
        {
            int currentCount = _holeCurrentCount.TryGetValue(hole, out int c) ? c : 0;
            int slots = maxEnemiesPerHole - currentCount;
            if (slots <= 0) continue;

            SpawnInZone(hole, slots * maxCost, costCeiling);
        }

        Debug.Log("[EnemySpawner] Noise 100% emergency: all zones filled to cap.");
        LogEnemyStats();
    }

    private void SpawnNoiseWave(float totalBudget, int costCeiling)
    {
        MapGenerator mapGenerator = GameManager.Instance != null ? GameManager.Instance.mapGenerator : null;
        if (mapGenerator == null) return;

        IReadOnlyList<Vector2Int> holes = mapGenerator.EnemySpawnHolePositions;
        if (holes == null || holes.Count == 0) return;

        List<Vector2Int> availableHoles = holes.Distinct()
            .Where(h => (_holeCurrentCount.TryGetValue(h, out int c) ? c : 0) < maxEnemiesPerHole)
            .ToList();

        if (availableHoles.Count == 0) return;

        int maxCost = GetMaxEnemyCost();
        foreach (Vector2Int hole in availableHoles)
            SpawnInZone(hole, maxCost, costCeiling, maxSpawns: 1);
    }

    private int SpawnInZone(Vector2Int holeKey, float budget, int costCeiling, int maxSpawns = int.MaxValue)
    {
        if (ShouldBlockSpawnsForTutorial()) return 0;

        int currentCount = _holeCurrentCount.TryGetValue(holeKey, out int c) ? c : 0;
        int slotsAvailable = Mathf.Min(maxEnemiesPerHole - currentCount, maxSpawns);

        List<EnemySpawnData> validEnemies = enemyPrefabs
            .Where(e => e != null && e.enemyPrefab != null && e.cost > 0)
            .ToList();

        MapGenerator mapGenerator = GameManager.Instance != null ? GameManager.Instance.mapGenerator : null;

        Grid grid = BuildingManager.Instance != null ? BuildingManager.Instance.grid : null;

        Vector3 center = mapGenerator.GetEnemySpawnHoleWorldPosition(holeKey);
        (float homeRadius, float territoryRadius) = mapGenerator.GetHoleRadiusValues(holeKey);

        float noisePct = NoiseManager.Instance != null ? NoiseManager.Instance.NoisePercentage : 0f;
        NoiseManager.NoiseZone noiseZone = NoiseManager.Instance != null
            ? NoiseManager.Instance.GetCurrentNoiseZone()
            : NoiseManager.NoiseZone.Safe;
        bool shouldEnhance = noiseZone == NoiseManager.NoiseZone.Warning || noiseZone == NoiseManager.NoiseZone.Danger;

        int spawned = 0;
        float remainingBudget = budget;

        while (remainingBudget > 0f && spawned < slotsAvailable)
        {
            List<EnemySpawnData> affordable = validEnemies.Where(e => e.cost <= remainingBudget).ToList();
            if (affordable.Count == 0) break;

            EnemySpawnData pick = PickEnemyByNoiseBias(affordable, noisePct);

            Vector3 spawnPos = FindValidSpawnPosition(center, spawnRadiusOffset, grid);
            string poolTag = GetPoolTagFromPrefab(pick.enemyPrefab);
            GameObject enemy = ObjectPooler.Instance.SpawnFromPool(poolTag, spawnPos, Quaternion.identity);

            UnitMovement movement = enemy.GetComponent<UnitMovement>();
            if (movement != null) movement.StopMovement();

            EnemyUnitBase enemyScript = enemy.GetComponent<EnemyUnitBase>();
            if (enemyScript != null)
            {
                enemyScript.SetTerritoryCenter(center, homeRadius, territoryRadius);
                enemyScript.SetSpawnHoleMetadata(holeKey, pick.cost);

                if (shouldEnhance)
                {
                    float moveMult = 1f + pick.moveSpeedBonusPercent / 100f;
                    float attackMult = 1f + pick.attackBonusPercent / 100f;
                    float healthMult = 1f + pick.healthBonusPercent / 100f;
                    enemyScript.ApplyEnhancement(pick.enhancedSpriteColor, moveMult, attackMult, healthMult);
                }
            }

            if (!_holeCurrentCount.ContainsKey(holeKey)) _holeCurrentCount[holeKey] = 0;
            _holeCurrentCount[holeKey]++;
            remainingBudget -= pick.cost;
            spawned++;
        }

        return spawned;
    }

    private static EnemySpawnData PickEnemyByNoiseBias(List<EnemySpawnData> pool, float noisePct)
    {
        if (pool.Count == 1) return pool[0];

        // At low noise, strong inverse-cost bias (prefer cheap enemies).
        // At 100% noise, biasStrength = 0 → uniform random.
        float biasStrength = Mathf.Lerp(3f, 0f, noisePct / 100f);

        float totalWeight = 0f;
        float[] weights = new float[pool.Count];
        for (int i = 0; i < pool.Count; i++)
        {
            float w = biasStrength > 0f ? Mathf.Pow(1f / pool[i].cost, biasStrength) : 1f;
            weights[i] = w;
            totalWeight += w;
        }

        float rand = Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative) return pool[i];
        }

        return pool[pool.Count - 1];
    }

    private void HandleEnemyUnitRemoved(EnemyUnitBase enemy)
    {
        if (enemy == null) return;

        if (!enemy.HasSpawnHoleKey) return;

        Vector2Int holeKey = enemy.SpawnHoleKey;
        if (_holeCurrentCount.ContainsKey(holeKey))
            _holeCurrentCount[holeKey] = Mathf.Max(0, _holeCurrentCount[holeKey] - 1);

        if (!ShouldBlockSpawnsForTutorial())
            _deathRespawnQueue.Add((holeKey, Time.time + enemyDeathRespawnDelaySeconds));
    }

    private IEnumerator DeathRespawnRoutine()
    {
        WaitForSeconds wait = CoroutineCache.GetWaitForSeconds(1f);
        while (enabled)
        {
            yield return wait;
            ProcessDeathRespawnQueue();
        }
    }

    private void ProcessDeathRespawnQueue()
    {
        if (_deathRespawnQueue.Count == 0) return;

        float now = Time.time;
        for (int i = _deathRespawnQueue.Count - 1; i >= 0; i--)
        {
            (Vector2Int hole, float respawnAt) = _deathRespawnQueue[i];
            if (now < respawnAt) continue;

            _deathRespawnQueue.RemoveAt(i);

            if (ShouldBlockSpawnsForTutorial()) continue;

            int currentCount = _holeCurrentCount.TryGetValue(hole, out int c) ? c : 0;
            if (currentCount >= maxEnemiesPerHole) continue;

            float noisePct = NoiseManager.Instance != null ? NoiseManager.Instance.NoisePercentage : 0f;
            int costCeiling = GetCostCeilingForNoise(noisePct);
            SpawnInZone(hole, costCeiling, costCeiling);
        }
    }

    private int GetCostCeilingForNoise(float noisePct)
    {
        List<int> validCosts = enemyPrefabs
            .Where(e => e != null && e.cost > 0)
            .Select(e => e.cost)
            .ToList();
        if (validCosts.Count == 0) return 1;

        int minCost = validCosts.Min();
        int maxCost = validCosts.Max();
        float t = Mathf.Clamp01(noisePct / 100f);
        return Mathf.Max(minCost, Mathf.RoundToInt(Mathf.Lerp(minCost, maxCost, t)));
    }

    private int GetMaxEnemyCost()
    {
        return enemyPrefabs
            .Where(e => e != null && e.cost > 0)
            .Select(e => e.cost)
            .DefaultIfEmpty(1)
            .Max();
    }

    private int GetMinEnemyCost()
    {
        return enemyPrefabs
            .Where(e => e != null && e.cost > 0)
            .Select(e => e.cost)
            .DefaultIfEmpty(1)
            .Min();
    }

    private void OnDayStarted()
    {
        if (UnitManager.Instance == null) return;

        foreach (UnitBase unit in new List<UnitBase>(UnitManager.Instance.EnemyUnits))
        {
            if (unit == null) continue;
            if (unit is Damageable damageable)
                damageable.RestoreToFullHealth();
        }
    }

    private void SyncAttackActivationToCurrentNoise()
    {
        if (ShouldBlockSpawnsForTutorial())
        {
            StopAttackActivationCoroutine();
            _activeAttackZone = NoiseManager.NoiseZone.Safe;
            _activeAttackMultiplier = 0f;
            return;
        }

        if (NoiseManager.Instance == null)
        {
            StopAttackActivationCoroutine();
            _activeAttackZone = NoiseManager.NoiseZone.Safe;
            _activeAttackMultiplier = 0f;
            return;
        }

        NoiseManager.NoiseZone zone = NoiseManager.Instance.GetCurrentNoiseZone();
        float desiredMultiplier;
        if (zone == NoiseManager.NoiseZone.Caution)
            desiredMultiplier = noiseCautionBudgetMultiplierPercent * 0.01f;
        else if (zone == NoiseManager.NoiseZone.Warning || zone == NoiseManager.NoiseZone.Danger)
            desiredMultiplier = noiseWarningBudgetMultiplierPercent * 0.01f;
        else
            desiredMultiplier = 0f;

        if (zone == _activeAttackZone && Mathf.Abs(desiredMultiplier - _activeAttackMultiplier) < 0.0001f)
            return;

        _activeAttackZone = zone;
        _activeAttackMultiplier = desiredMultiplier;
        StopAttackActivationCoroutine();

        ActivateExistingEnemiesFromBudget(_activeAttackMultiplier);

        if (_activeAttackMultiplier > 0f && _activeAttackZone != NoiseManager.NoiseZone.Safe)
            _attackActivationCoroutine = StartCoroutine(AttackActivationLoop());
    }

    private void StopAttackActivationCoroutine()
    {
        if (_attackActivationCoroutine == null) return;
        StopCoroutine(_attackActivationCoroutine);
        _attackActivationCoroutine = null;
    }

    private IEnumerator AttackActivationLoop()
    {
        WaitForSeconds wait = CoroutineCache.GetWaitForSeconds(Mathf.Max(0.1f, attackActivationIntervalSeconds));

        while (enabled && !ShouldBlockSpawnsForTutorial() && _activeAttackZone != NoiseManager.NoiseZone.Safe)
        {
            ActivateExistingEnemiesFromBudget(_activeAttackMultiplier);
            yield return wait;
        }

        _attackActivationCoroutine = null;
    }

    private void ActivateExistingEnemiesFromBudget(float multiplier)
    {
        if (ShouldBlockSpawnsForTutorial()) return;
        if (UnitManager.Instance == null) return;

        List<EnemyUnitBase> allEnemies = UnitManager.Instance.EnemyUnits
            .Where(u => u != null && u is EnemyUnitBase)
            .Cast<EnemyUnitBase>()
            .ToList();

        if (allEnemies.Count == 0) return;

        int targetAttackCount = Mathf.Max(0, Mathf.FloorToInt(allEnemies.Count * multiplier));
        int alreadyInAttack = allEnemies.Count(e => e.IsInInfiniteAttackState());

        bool didChange = false;

        if (alreadyInAttack > targetAttackCount)
        {
            List<EnemyUnitBase> attacking = allEnemies.Where(e => e.IsInInfiniteAttackState()).ToList();
            int toDeactivate = Mathf.Min(alreadyInAttack - targetAttackCount, attacking.Count);
            for (int i = 0; i < toDeactivate; i++)
            {
                int idx = Random.Range(0, attacking.Count);
                attacking[idx].DeactivateInfiniteAttackState();
                attacking.RemoveAt(idx);
                didChange = true;
            }
        }
        else if (alreadyInAttack < targetAttackCount)
        {
            List<EnemyUnitBase> candidates = allEnemies.Where(e => !e.IsInInfiniteAttackState()).ToList();
            int toActivate = Mathf.Min(targetAttackCount - alreadyInAttack, candidates.Count);
            for (int i = 0; i < toActivate; i++)
            {
                int idx = Random.Range(0, candidates.Count);
                candidates[idx].ActivateInfiniteAttackState();
                candidates.RemoveAt(idx);
                didChange = true;
            }
        }

        if (didChange) LogEnemyStats();
    }

    private static bool ShouldBlockSpawnsForTutorial()
    {
        return TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive();
    }

    private static void LogEnemyStats()
    {
        if (UnitManager.Instance == null) return;

        int total = 0, attackStateCount = 0;
        Dictionary<string, int> totalByType = new Dictionary<string, int>();
        Dictionary<string, int> attackByType = new Dictionary<string, int>();
        Dictionary<string, int> enhancedByType = new Dictionary<string, int>();

        foreach (UnitBase unit in UnitManager.Instance.EnemyUnits)
        {
            if (unit == null || !(unit is EnemyUnitBase enemy)) continue;
            total++;
            string typeName = enemy.GetType().Name;
            if (!totalByType.ContainsKey(typeName)) totalByType[typeName] = 0;
            totalByType[typeName]++;

            if (enemy.IsInInfiniteAttackState())
            {
                attackStateCount++;
                if (!attackByType.ContainsKey(typeName)) attackByType[typeName] = 0;
                attackByType[typeName]++;
            }

            if (enemy.IsEnhanced)
            {
                if (!enhancedByType.ContainsKey(typeName)) enhancedByType[typeName] = 0;
                enhancedByType[typeName]++;
            }
        }
    }

    private Vector3 FindValidSpawnPosition(Vector3 center, float maxRadius, Grid grid)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * maxRadius;
            Vector3 candidatePos = new Vector3(center.x + offset.x, center.y + offset.y, center.z);
            Vector3Int cell = grid.WorldToCell(candidatePos);
            if (!BuildingManager.Instance.IsResourceTile(cell))
                return grid.GetCellCenterWorld(cell);
        }

        for (int radius = 1; radius <= Mathf.CeilToInt(maxRadius); radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                    Vector3Int testCell = grid.WorldToCell(center) + new Vector3Int(x, y, 0);
                    float distance = Vector3.Distance(center, grid.GetCellCenterWorld(testCell));
                    if (distance <= maxRadius && !BuildingManager.Instance.IsResourceTile(testCell))
                        return grid.GetCellCenterWorld(testCell);
                }
            }
        }

        return center;
    }

    public static string GetPoolTagFromPrefab(GameObject prefab)
    {
        if (prefab == null) return string.Empty;

        EnemyUnitBase enemyUnit = prefab.GetComponent<EnemyUnitBase>();
        if (enemyUnit == null) return string.Empty;

        if (enemyUnit is Unit_Enemy_1) return "Enemy_1";
        if (enemyUnit is Unit_Enemy_2) return "Enemy_2";
        return "Enemy_0";
    }

    public List<EnemySpawnData> GetEnemyPrefabs() => enemyPrefabs;

    public int GetMaxEnemiesPerHole() => maxEnemiesPerHole;
}
