using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawning Settings")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float initialSpawnDelay = 60f;
    [SerializeField] private float spawnInterval = 120f;
    [SerializeField] private int baseSpawnCount = 1;
    [SerializeField] private int spawnCountIncreasePerWave = 1;

    private int _currentWave = 0;

    private void Start()
    {
        StartCoroutine(SpawnCoroutine());
    }

    private IEnumerator SpawnCoroutine()
    {
        yield return new WaitForSeconds(initialSpawnDelay);

        while (true)
        {
            _currentWave++;
            SpawnWave();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnWave()
    {
        int spawnCount = baseSpawnCount + (_currentWave - 1) * spawnCountIncreasePerWave;
        Debug.Log($"Wave {_currentWave}: {spawnCount}마리의 적을 스폰합니다.");

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPosition = GetRandomEdgeTilePosition();
            Instantiate(enemyPrefab, spawnPosition, Quaternion.identity, BuildingManager.Instance.grid.transform);
        }
    }

    private Vector3 GetRandomEdgeTilePosition()
    {
        var mapGenerator = GameManager.Instance.mapGenerator;
        Vector2Int mapSize = mapGenerator.MapSize;
        
        // Choose a random edge (left, right, top, bottom)
        Vector2Int[] directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
        Vector2Int chosenDirection = directions[Random.Range(0, directions.Length)];

        float mapCenterXOffset = mapSize.x / 2f;
        float mapCenterYOffset = mapSize.y / 2f;

        float spawnX = 0, spawnY = 0;

        if (chosenDirection == Vector2Int.right) // Right edge
        {
            spawnX = mapSize.x - 1 - mapCenterXOffset;
            spawnY = Random.Range(-mapCenterYOffset, mapSize.y - 1 - mapCenterYOffset);
        }
        else if (chosenDirection == Vector2Int.left) // Left edge
        {
            spawnX = -mapCenterXOffset;
            spawnY = Random.Range(-mapCenterYOffset, mapSize.y - 1 - mapCenterYOffset);
        }
        else if (chosenDirection == Vector2Int.up) // Top edge
        {
            spawnX = Random.Range(-mapCenterXOffset, mapSize.x - 1 - mapCenterXOffset);
            spawnY = mapSize.y - 1 - mapCenterYOffset;
        }
        else if (chosenDirection == Vector2Int.down) // Bottom edge
        {
            spawnX = Random.Range(-mapCenterXOffset, mapSize.x - 1 - mapCenterXOffset);
            spawnY = -mapCenterYOffset;
        }
        
        return mapGenerator.Tilemap.transform.TransformPoint(new Vector3(spawnX + 0.5f, spawnY + 0.5f, 0));
    }
}