using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int enemiesPerHole = 3;
    [SerializeField] private float spawnRadiusOffset = 1f;

    public void SpawnEnemies()
    {
        if (enemyPrefab == null)
        {
            return;
        }
        if (GameManager.Instance == null || GameManager.Instance.mapGenerator == null)
        {
            return;
        }
        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null)
        {
            return;
        }
        
        MapGenerator mapGenerator = GameManager.Instance.mapGenerator;
        IReadOnlyList<Vector2Int> holes = mapGenerator.EnemySpawnHolePositions;
        if (holes == null || holes.Count == 0)
        {
            return;
        }
        
        Transform parent = BuildingManager.Instance.grid.transform;
        Grid grid = BuildingManager.Instance.grid;
        
        for (int i = 0; i < holes.Count; i++)
        {
            Vector3 center = mapGenerator.GetEnemySpawnHoleWorldPosition(holes[i]);
            for (int j = 0; j < enemiesPerHole; j++)
            {
                Vector3 spawnPos = FindValidSpawnPosition(center, spawnRadiusOffset, grid);
                if (spawnPos != Vector3.zero)
                {
                    Object.Instantiate(enemyPrefab, spawnPos, Quaternion.identity, parent);
                }
            }
        }
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