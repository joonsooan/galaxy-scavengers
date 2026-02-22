using System;
using System.Collections;
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
    [SerializeField] private int maxSpawnCountPerWave = 50;
    [SerializeField] private float spawnRadiusOffset = 1f;

    [Header("Budget Formula (basePoints + noise*noiseAlpha) * (1 + time*timeBeta)")]
    [SerializeField] [Range(0f, 500f)] private float basePoints = 50f;
    [SerializeField] [Range(0f, 1f)] private float noiseAlpha = 1f;
    [SerializeField] [Range(0f, 1f)] private float timeBeta = 0.1f;

    [Header("Wave Budget Settings")]
    [SerializeField] private float noise100WaveCooldown = 30f;
    [SerializeField] private int noise100AreaCount = 3;
    [SerializeField] [Range(0f, 1f)] private float noise100BudgetMultiplier = 1.5f;
    [SerializeField] [Range(0f, 5f)] private float emergencyWaveMultiplierIncrement = 0.1f;
    [SerializeField] [Range(0f, 1f)] private float noiseCautionBudgetMultiplier = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float noiseWarningBudgetMultiplier = 1f;

    private bool _isEmergencyWaveSpawn;
    private bool _wasNoise100 = false;
    private bool _noiseEmergencyActive;
    private bool _countdownEmergencyActive;
    private float _noiseEmergencyMultiplierBonus;
    private float _countdownEmergencyMultiplierBonus;
    private Coroutine _emergencyWaveCoroutine;
    private readonly HashSet<int> _persistentEnemyInstanceIds = new HashSet<int>();
    private string _currentEmergencyTriggerSource = string.Empty;
    private bool _isSubscribedToEvents;

    private void OnEnable()
    {
        if (_isSubscribedToEvents)
        {
            return;
        }

        DayNightCycleManager.OnNightStarted += OnNightStarted;
        DayNightCycleManager.OnDayStarted += OnDayStarted;
        LaunchUIController.OnLaunchCountdownStarted += OnLaunchCountdownStarted;
        LaunchUIController.OnLaunchCountdownFinished += OnLaunchCountdownFinished;

        if (NoiseManager.Instance != null) NoiseManager.Instance.OnNoiseChanged += OnNoiseChanged;
        _isSubscribedToEvents = true;
    }

    private void OnDisable()
    {
        if (!_isSubscribedToEvents)
        {
            return;
        }

        DayNightCycleManager.OnNightStarted -= OnNightStarted;
        DayNightCycleManager.OnDayStarted -= OnDayStarted;
        LaunchUIController.OnLaunchCountdownStarted -= OnLaunchCountdownStarted;
        LaunchUIController.OnLaunchCountdownFinished -= OnLaunchCountdownFinished;

        if (NoiseManager.Instance != null) NoiseManager.Instance.OnNoiseChanged -= OnNoiseChanged;
        _isSubscribedToEvents = false;
        ResetEmergencyState();
    }

    private void OnNightStarted()
    {
        if (NoiseManager.Instance == null) return;
        if (IsAnyEmergencyActive()) return;

        float noisePercentage = NoiseManager.Instance.NoisePercentage;
        if (noisePercentage < 100f)
        {
            SpawnWaveFromBudget();
        }

        NoiseManager.NoiseZone zone = NoiseManager.Instance.GetCurrentNoiseZone();
        if (zone == NoiseManager.NoiseZone.Caution)
        {
            ActivateExistingEnemiesFromBudget(noiseCautionBudgetMultiplier);
        }
        else if (zone == NoiseManager.NoiseZone.Warning)
        {
            ActivateExistingEnemiesFromBudget(noiseWarningBudgetMultiplier);
        }
    }

    private void OnNoiseChanged(float noisePercentage)
    {
        if (noisePercentage >= 100f)
        {
            if (!_wasNoise100)
            {
                _wasNoise100 = true;
                Debug.Log($"[EnemySpawner] Emergency trigger: Noise reached {noisePercentage:0.##}%. Starting emergency spawn.");
                StartNoiseEmergency();
            }
        }
        else
        {
            _wasNoise100 = false;
            if (_noiseEmergencyActive)
            {
                Debug.Log($"[EnemySpawner] Emergency trigger ended: Noise dropped below 100% ({noisePercentage:0.##}%). Stopping noise emergency spawn and resetting noise multiplier bonus.");
            }
            StopNoiseEmergency();
        }
    }

    private void ActivateExistingEnemiesFromBudget(float multiplier, bool markAsPersistent = false)
    {
        if (UnitManager.Instance == null) return;

        List<EnemyUnitBase> allEnemies = UnitManager.Instance.EnemyUnits
            .Where(u => u != null && u is EnemyUnitBase)
            .Cast<EnemyUnitBase>()
            .ToList();
        int totalSpawned = allEnemies.Count;
        if (totalSpawned == 0) return;

        int targetAttackCount = Mathf.FloorToInt(totalSpawned * multiplier);
        int alreadyInAttack = allEnemies.Count(e => e.IsInInfiniteAttackState());
        int slotsRemaining = Mathf.Max(0, targetAttackCount - alreadyInAttack);

        List<EnemyUnitBase> candidates = allEnemies
            .Where(e => !e.IsInInfiniteAttackState())
            .ToList();
        if (candidates.Count == 0 || slotsRemaining <= 0) return;

        int toActivate = Mathf.Min(slotsRemaining, candidates.Count);
        for (int i = 0; i < toActivate; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            EnemyUnitBase selected = candidates[idx];
            candidates.RemoveAt(idx);
            if (markAsPersistent)
            {
                MarkPersistentEnemy(selected);
            }
            selected.ActivateInfiniteAttackState();
        }
        LogEnemyStats();
    }

    private void ActivateExistingEnemiesFromBudget()
    {
        ActivateExistingEnemiesFromBudget(noise100BudgetMultiplier, false);
    }

    private void OnDayStarted()
    {
        if (UnitManager.Instance == null) return;

        List<UnitBase> enemies = new List<UnitBase>(UnitManager.Instance.EnemyUnits);
        foreach (UnitBase unit in enemies)
        {
            if (unit == null) continue;
            EnemyUnitBase enemyUnit = unit as EnemyUnitBase;
            if (enemyUnit != null && IsPersistentEnemy(enemyUnit)) continue;
            if (unit is Damageable damageable)
            {
                damageable.RestoreToFullHealth();
            }

            EnemyDaySinkingEffect sinkingEffect = unit.GetComponent<EnemyDaySinkingEffect>();
            if (sinkingEffect != null)
            {
                sinkingEffect.StartSinking(() =>
                {
                    if (unit != null && unit.gameObject != null)
                    {
                        unit.gameObject.SetActive(false);
                    }
                });
            }
            else
            {
                unit.gameObject.SetActive(false);
            }
        }
    }

    private float CalculateWaveBudget(float multiplier = 1f)
    {
        float noise = NoiseManager.Instance != null ? NoiseManager.Instance.NoisePercentage : 0f;
        float totalElapsedHours = DayNightCycleManager.Instance != null ? DayNightCycleManager.Instance.GetTotalElapsedInGameHours() : 0f;
        float rawResult = (basePoints + (noise * noiseAlpha)) * (1f + (totalElapsedHours * timeBeta));
        float result = rawResult * multiplier;

        // Debug.Log($"[EnemySpawner] Budget calc: basePoints={basePoints}, noise={noise}, totalElapsedInGameHours={totalElapsedHours}, \n"
        //     + $"noiseAlpha={noiseAlpha}, timeBeta={timeBeta}, rawResult={rawResult}, multiplier={multiplier}, finalBudget={result}");

        return result;
    }

    private void SpawnWaveFromBudget()
    {
        float multiplier = _isEmergencyWaveSpawn ? GetCurrentEmergencyMultiplier() : 1f;
        float totalBudget = CalculateWaveBudget(multiplier);
        int spawnedEnemyCount = 0;

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
        if (_isEmergencyWaveSpawn)
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

        if (BuildingManager.Instance == null) return;
        Grid grid = BuildingManager.Instance.grid;
        if (grid == null) return;

        List<(EnemySpawnData data, Vector2Int pos)> allSpawnsUncapped = new List<(EnemySpawnData, Vector2Int)>();

        for (int holeIdx = 0; holeIdx < selectedHoles.Count; holeIdx++)
        {
            Vector2Int holePos = selectedHoles[holeIdx];
            float remainingBudget = budgetPerHole;

            while (remainingBudget > 0f)
            {
                List<EnemySpawnData> affordable = validEnemies.Where(e => e.cost <= remainingBudget).ToList();
                if (affordable.Count == 0) break;

                EnemySpawnData selectedEnemy = affordable[Random.Range(0, affordable.Count)];
                allSpawnsUncapped.Add((selectedEnemy, holePos));
                remainingBudget -= selectedEnemy.cost;
            }
        }

        float enhancementBudget = 0f;
        List<(EnemySpawnData data, Vector2Int pos)> finalSpawns;

        if (allSpawnsUncapped.Count > maxSpawnCountPerWave)
        {
            for (int i = allSpawnsUncapped.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = allSpawnsUncapped[i];
                allSpawnsUncapped[i] = allSpawnsUncapped[j];
                allSpawnsUncapped[j] = temp;
            }

            for (int i = maxSpawnCountPerWave; i < allSpawnsUncapped.Count; i++)
            {
                enhancementBudget += allSpawnsUncapped[i].data.cost;
            }

            finalSpawns = new List<(EnemySpawnData, Vector2Int)>();
            for (int i = 0; i < maxSpawnCountPerWave; i++)
            {
                finalSpawns.Add(allSpawnsUncapped[i]);
            }
        }
        else
        {
            finalSpawns = new List<(EnemySpawnData, Vector2Int)>(allSpawnsUncapped);
        }

        for (int i = finalSpawns.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = finalSpawns[i];
            finalSpawns[i] = finalSpawns[j];
            finalSpawns[j] = temp;
        }

        List<(EnemySpawnData data, Vector2Int pos, bool isEnhanced)> enhancedSpawns = new List<(EnemySpawnData, Vector2Int, bool)>();
        float remainingEnhancementBudget = enhancementBudget;
        foreach (var spawn in finalSpawns)
        {
            bool isEnhanced = false;
            if (remainingEnhancementBudget >= spawn.data.enhancementCost && spawn.data.enhancementCost > 0)
            {
                remainingEnhancementBudget -= spawn.data.enhancementCost;
                isEnhanced = true;
            }
            enhancedSpawns.Add((spawn.data, spawn.pos, isEnhanced));
        }

        Dictionary<int, Dictionary<string, int>> holeSpawnCounts = new Dictionary<int, Dictionary<string, int>>();
        for (int holeIdx = 0; holeIdx < selectedHoles.Count; holeIdx++)
        {
            holeSpawnCounts[holeIdx] = new Dictionary<string, int>();
        }

        foreach (var spawn in enhancedSpawns)
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
                    spawnedEnemyCount++;
                    EnemyUnitBase enemyScript = enemy.GetComponent<EnemyUnitBase>();
                    if (enemyScript != null)
                    {
                        UnitMovement movement = enemy.GetComponent<UnitMovement>();
                        if (movement != null) movement.StopMovement();
                        enemyScript.SetTerritoryCenter(center, homeRadius, territoryRadius);
                        if (spawn.isEnhanced)
                        {
                            float moveMult = 1f + spawn.data.moveSpeedBonusPercent / 100f;
                            float attackMult = 1f + spawn.data.attackBonusPercent / 100f;
                            float healthMult = 1f + spawn.data.healthBonusPercent / 100f;
                            enemyScript.ApplyEnhancement(spawn.data.enhancedSpriteColor, moveMult, attackMult, healthMult);
                        }
                        if (_isEmergencyWaveSpawn)
                        {
                            MarkPersistentEnemy(enemyScript);
                            enemyScript.ActivateInfiniteAttackState();
                        }
                        else
                        {
                            UnmarkPersistentEnemy(enemyScript);
                        }
                        if (TutorialManager.Instance != null)
                        {
                            TutorialManager.Instance.RegisterSpawnedEnemy(enemyScript);
                        }
                    }

                    int holeIdx = selectedHoles.IndexOf(spawn.pos);
                    if (holeIdx >= 0 && holeIdx < holeSpawnCounts.Count)
                    {
                        if (!holeSpawnCounts[holeIdx].ContainsKey(poolTag))
                            holeSpawnCounts[holeIdx][poolTag] = 0;
                        holeSpawnCounts[holeIdx][poolTag]++;
                    }
                }
            }
        }
        if (_isEmergencyWaveSpawn)
        {
            Debug.Log($"[EnemySpawner] Emergency wave spawned. Trigger={_currentEmergencyTriggerSource}, Spawned={spawnedEnemyCount}, Multiplier={multiplier:0.##}, AreaCount={selectedHoles.Count}, NoiseEmergency={_noiseEmergencyActive}, CountdownEmergency={_countdownEmergencyActive}");
        }
        LogEnemyStats();
    }

    private void OnLaunchCountdownStarted()
    {
        Debug.Log("[EnemySpawner] Emergency trigger: Launch countdown started. Starting emergency spawn.");
        StartCountdownEmergency();
    }

    private void OnLaunchCountdownFinished()
    {
        if (_countdownEmergencyActive)
        {
            Debug.Log("[EnemySpawner] Emergency trigger ended: Launch countdown finished. Stopping countdown emergency spawn and resetting countdown multiplier bonus.");
        }
        StopCountdownEmergency();
    }

    private void StartNoiseEmergency()
    {
        if (_noiseEmergencyActive) return;

        _noiseEmergencyActive = true;
        _currentEmergencyTriggerSource = "Noise100";
        ActivateExistingEnemiesFromBudget(noise100BudgetMultiplier, true);
        SpawnEmergencyWave();
        EnsureEmergencyWaveLoopRunning();
    }

    private void StopNoiseEmergency()
    {
        if (!_noiseEmergencyActive) return;

        _noiseEmergencyActive = false;
        _noiseEmergencyMultiplierBonus = 0f;
        TryStopEmergencyWaveLoop();
    }

    private void StartCountdownEmergency()
    {
        if (_countdownEmergencyActive) return;

        _countdownEmergencyActive = true;
        _currentEmergencyTriggerSource = "LaunchCountdown";
        ActivateExistingEnemiesFromBudget(noise100BudgetMultiplier, true);
        SpawnEmergencyWave();
        EnsureEmergencyWaveLoopRunning();
    }

    private void StopCountdownEmergency()
    {
        if (!_countdownEmergencyActive) return;

        _countdownEmergencyActive = false;
        _countdownEmergencyMultiplierBonus = 0f;
        TryStopEmergencyWaveLoop();
    }

    private void EnsureEmergencyWaveLoopRunning()
    {
        if (!IsAnyEmergencyActive()) return;
        if (_emergencyWaveCoroutine != null) return;
        _emergencyWaveCoroutine = StartCoroutine(EmergencyWaveLoop());
    }

    private void TryStopEmergencyWaveLoop()
    {
        if (IsAnyEmergencyActive()) return;
        if (_emergencyWaveCoroutine == null) return;
        StopCoroutine(_emergencyWaveCoroutine);
        _emergencyWaveCoroutine = null;
    }

    private IEnumerator EmergencyWaveLoop()
    {
        while (IsAnyEmergencyActive())
        {
            yield return CoroutineCache.GetWaitForSeconds(noise100WaveCooldown);
            if (!IsAnyEmergencyActive()) break;
            SpawnEmergencyWave();
        }

        _emergencyWaveCoroutine = null;
    }

    private void SpawnEmergencyWave()
    {
        if (_countdownEmergencyActive && _noiseEmergencyActive)
        {
            _currentEmergencyTriggerSource = "Noise100+LaunchCountdown";
        }
        else if (_countdownEmergencyActive)
        {
            _currentEmergencyTriggerSource = "LaunchCountdown";
        }
        else if (_noiseEmergencyActive)
        {
            _currentEmergencyTriggerSource = "Noise100";
        }

        _isEmergencyWaveSpawn = true;
        SpawnWaveFromBudget();
        _isEmergencyWaveSpawn = false;

        if (_countdownEmergencyActive)
        {
            _countdownEmergencyMultiplierBonus += emergencyWaveMultiplierIncrement;
        }
        if (_noiseEmergencyActive)
        {
            _noiseEmergencyMultiplierBonus += emergencyWaveMultiplierIncrement;
        }
    }

    private float GetCurrentEmergencyMultiplier()
    {
        float multiplier = noise100BudgetMultiplier;
        if (_countdownEmergencyActive)
        {
            multiplier += _countdownEmergencyMultiplierBonus;
        }
        if (_noiseEmergencyActive)
        {
            multiplier += _noiseEmergencyMultiplierBonus;
        }

        return Mathf.Max(0f, multiplier);
    }

    private bool IsAnyEmergencyActive()
    {
        return _countdownEmergencyActive || _noiseEmergencyActive;
    }

    private void MarkPersistentEnemy(EnemyUnitBase enemy)
    {
        if (enemy == null) return;
        _persistentEnemyInstanceIds.Add(enemy.GetInstanceID());
    }

    private void UnmarkPersistentEnemy(EnemyUnitBase enemy)
    {
        if (enemy == null) return;
        _persistentEnemyInstanceIds.Remove(enemy.GetInstanceID());
    }

    private bool IsPersistentEnemy(EnemyUnitBase enemy)
    {
        if (enemy == null) return false;
        return _persistentEnemyInstanceIds.Contains(enemy.GetInstanceID());
    }

    private void ResetEmergencyState()
    {
        _wasNoise100 = false;
        _noiseEmergencyActive = false;
        _countdownEmergencyActive = false;
        _noiseEmergencyMultiplierBonus = 0f;
        _countdownEmergencyMultiplierBonus = 0f;
        _isEmergencyWaveSpawn = false;

        if (_emergencyWaveCoroutine != null)
        {
            StopCoroutine(_emergencyWaveCoroutine);
            _emergencyWaveCoroutine = null;
        }

        _persistentEnemyInstanceIds.Clear();
    }

    private static void LogEnemyStats()
    {
        if (UnitManager.Instance == null) return;

        var enemies = UnitManager.Instance.EnemyUnits;
        int total = 0;
        int attackStateCount = 0;
        Dictionary<string, int> totalByType = new Dictionary<string, int>();
        Dictionary<string, int> attackByType = new Dictionary<string, int>();
        Dictionary<string, int> enhancedByType = new Dictionary<string, int>();

        foreach (UnitBase unit in enemies)
        {
            if (unit == null) continue;
            if (!(unit is EnemyUnitBase enemy)) continue;

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

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[EnemySpawner] Enemy Stats | Total: {total}, AttackState: {attackStateCount}");
        sb.Append("  By Type - Total: ");
        foreach (var kvp in totalByType) sb.Append($"{kvp.Key}={kvp.Value} ");
        sb.Append("\n  By Type - Attack: ");
        foreach (var kvp in attackByType) sb.Append($"{kvp.Key}={kvp.Value} ");
        sb.Append("\n  By Type - Enhanced: ");
        foreach (var kvp in enhancedByType) sb.Append($"{kvp.Key}={kvp.Value} ");
        Debug.Log(sb.ToString());
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
