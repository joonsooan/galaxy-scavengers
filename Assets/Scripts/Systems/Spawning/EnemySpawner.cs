using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Systems.Jobs;

[System.Serializable]
public class EnemySpawnData
{
    public GameObject enemyPrefab;
    [Range(0f, 100f)]
    public float spawnProbability = 50f;
}

    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private List<EnemySpawnData> enemyPrefabs = new ();
        [SerializeField] private int minEnemiesPerHole = 5;
        [SerializeField] private int maxEnemiesPerHole = 7;
        [SerializeField] private float spawnRadiusOffset = 1f;

        public void SpawnEnemies()
        {
            MapGenerator mapGenerator = GameManager.Instance.mapGenerator;
            IReadOnlyList<Vector2Int> holes = mapGenerator.EnemySpawnHolePositions;
            if (holes == null || holes.Count == 0)
            {
                return;
            }
            
            Grid grid = BuildingManager.Instance.grid;
            
            for (int i = 0; i < holes.Count; i++)
            {
                Vector2Int holePos = holes[i];
                Vector3 center = mapGenerator.GetEnemySpawnHoleWorldPosition(holePos);
                (float homeRadius, float territoryRadius) = mapGenerator.GetHoleRadiusValues(holePos);
                
                if (homeRadius <= 0f || territoryRadius <= 0f)
                {
                    continue;
                }

                int enemiesPerHole = Random.Range(minEnemiesPerHole, maxEnemiesPerHole);
                
                for (int j = 0; j < enemiesPerHole; j++)
                {
                    GameObject selectedPrefab = SelectEnemyPrefab();
                    if (selectedPrefab == null)
                    {
                        continue;
                    }
                    
                    Vector3 spawnPos = FindValidSpawnPosition(center, spawnRadiusOffset, grid);
                    if (spawnPos != Vector3.zero && center != Vector3.zero)
                    {
                        string poolTag = GetPoolTagFromPrefab(selectedPrefab);
                        GameObject enemy = ObjectPooler.Instance.SpawnFromPool(poolTag, spawnPos, Quaternion.identity);
                        if (enemy != null)
                        {
                            EnemyUnitBase enemyScript = enemy.GetComponent<EnemyUnitBase>();
                            if (enemyScript != null)
                            {
                                enemyScript.SetTerritoryCenter(center, homeRadius, territoryRadius);
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
            if (holes == null || holes.Count == 0)
            {
                yield break;
            }
            
            Grid grid = BuildingManager.Instance.grid;
            int totalHoles = holes.Count;
            
            progress.UpdateProgress(0.0f, "적대적 생명체 탐색 중...");

            for (int i = 0; i < totalHoles; i++)
            {
                Vector2Int holePos = holes[i];
                Vector3 center = mapGenerator.GetEnemySpawnHoleWorldPosition(holePos);
                (float homeRadius, float territoryRadius) = mapGenerator.GetHoleRadiusValues(holePos);
                
                if (homeRadius > 0f && territoryRadius > 0f)
                {
                    int enemiesPerHole = Random.Range(minEnemiesPerHole, maxEnemiesPerHole);
                    
                    for (int j = 0; j < enemiesPerHole; j++)
                    {
                        GameObject selectedPrefab = SelectEnemyPrefab();
                        if (selectedPrefab == null)
                        {
                            continue;
                        }
                        
                        Vector3 spawnPos = FindValidSpawnPosition(center, spawnRadiusOffset, grid);
                        if (spawnPos != Vector3.zero && center != Vector3.zero)
                        {
                            string poolTag = GetPoolTagFromPrefab(selectedPrefab);
                            GameObject enemy = ObjectPooler.Instance.SpawnFromPool(poolTag, spawnPos, Quaternion.identity);
                            if (enemy != null)
                            {
                                EnemyUnitBase enemyScript = enemy.GetComponent<EnemyUnitBase>();
                                if (enemyScript != null)
                                {
                                    enemyScript.SetTerritoryCenter(center, homeRadius, territoryRadius);
                                }
                            }
                        }
                    }
                }
                
                if (i % 2 == 0)
                {
                    yield return null;
                }
            }
        }
    
    private GameObject SelectEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            return null;
        }
        
        List<EnemySpawnData> validEnemies = enemyPrefabs.Where(e => e != null && e.enemyPrefab != null && e.spawnProbability > 0f).ToList();
        if (validEnemies.Count == 0)
        {
            return null;
        }
        
        float totalProbability = validEnemies.Sum(e => e.spawnProbability);
        if (totalProbability <= 0f)
        {
            return validEnemies[0].enemyPrefab;
        }
        
        float randomValue = Random.Range(0f, totalProbability);
        float cumulativeProbability = 0f;
        
        foreach (EnemySpawnData enemyData in validEnemies)
        {
            cumulativeProbability += enemyData.spawnProbability;
            if (randomValue <= cumulativeProbability)
            {
                return enemyData.enemyPrefab;
            }
        }
        
        return validEnemies[validEnemies.Count - 1].enemyPrefab;
    }
    
    public static string GetPoolTagFromPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return string.Empty;
        }
        
        EnemyUnitBase enemyUnit = prefab.GetComponent<EnemyUnitBase>();
        if (enemyUnit == null)
        {
            return string.Empty;
        }
        
        if (enemyUnit is Unit_Enemy_0)
        {
        }
        else if (enemyUnit is Unit_Enemy_1)
        {
            return "Enemy_1";
        }
        else if (enemyUnit is Unit_Enemy_2)
        {
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
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * maxRadius;
            Vector3 candidatePos = new Vector3(center.x + offset.x, center.y + offset.y, center.z);
            Vector3Int cell = grid.WorldToCell(candidatePos);
            
            if (!BuildingManager.Instance.IsResourceTile(cell))
            {
                return grid.GetCellCenterWorld(cell);
            }
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
                    {
                        return grid.GetCellCenterWorld(testCell);
                    }
                }
            }
        }
        
        return center;
    }
}
