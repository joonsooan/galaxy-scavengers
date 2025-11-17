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
        List<Vector2Int> edgeRooms = FindEdgeRooms();
        if (edgeRooms.Count == 0)
        {
            Debug.LogWarning("스폰할 수 있는 가장자리 방이 없습니다.");
            return;
        }

        int spawnCount = baseSpawnCount + (_currentWave - 1) * spawnCountIncreasePerWave;
        Debug.Log($"Wave {_currentWave}: {spawnCount}마리의 적을 스폰합니다.");

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2Int randomEdgeRoomCoords = edgeRooms[Random.Range(0, edgeRooms.Count)];
            Vector3 spawnPosition = GetRandomEdgeTilePosition(randomEdgeRoomCoords);
            
            Instantiate(enemyPrefab, spawnPosition, Quaternion.identity, BuildingManager.Instance.grid.transform);
        }
    }

    private List<Vector2Int> FindEdgeRooms()
    {
        List<Vector2Int> edgeRooms = new List<Vector2Int>();
        Vector2Int mapGridSize = GameManager.Instance.mapGenerator.MapGridSize;

        for (int x = 0; x < mapGridSize.x; x++)
        {
            for (int y = 0; y < mapGridSize.y; y++)
            {
                if (GameManager.Instance.mapGenerator.IsRoomUnlocked(x, y))
                {
                    if (!GameManager.Instance.mapGenerator.IsRoomUnlocked(x + 1, y) ||
                        !GameManager.Instance.mapGenerator.IsRoomUnlocked(x - 1, y) ||
                        !GameManager.Instance.mapGenerator.IsRoomUnlocked(x, y + 1) ||
                        !GameManager.Instance.mapGenerator.IsRoomUnlocked(x, y - 1))
                    {
                        edgeRooms.Add(new Vector2Int(x, y));
                    }
                }
            }
        }
        return edgeRooms;
    }

    // private Vector3 GetRandomPositionInRoom(Vector2Int roomCoords)
    // {
    //     Vector2Int roomSize = GameManager.Instance.mapGenerator.roomSize;
    //     Vector2Int mapGridSize = GameManager.Instance.mapGenerator.mapGridSize;
    //     
    //     float totalMapWidth = roomSize.x * mapGridSize.x;
    //     float totalMapHeight = roomSize.y * mapGridSize.y;
    //     float mapCenterXOffset = totalMapWidth / 2f;
    //     float mapCenterYOffset = totalMapHeight / 2f;
    //
    //     float roomStartX = roomCoords.x * roomSize.x - mapCenterXOffset;
    //     float roomStartY = roomCoords.y * roomSize.y - mapCenterYOffset;
    //     
    //     float randomX = Random.Range(roomStartX + 1, roomStartX + roomSize.x - 1);
    //     float randomY = Random.Range(roomStartY + 1, roomStartY + roomSize.y - 1);
    //     
    //     return GameManager.Instance.mapGenerator.tilemap.transform.TransformPoint(new Vector3(randomX, randomY, 0));
    // }
    
    private Vector3 GetRandomEdgeTilePosition(Vector2Int roomCoords)
    {
        var mapGenerator = GameManager.Instance.mapGenerator;
        Vector2Int roomSize = mapGenerator.RoomSize;
        Vector2Int mapGridSize = mapGenerator.MapGridSize;
        
        List<Vector2Int> validDirections = new List<Vector2Int>();
        if (!mapGenerator.IsRoomUnlocked(roomCoords.x + 1, roomCoords.y)) validDirections.Add(Vector2Int.right);
        if (!mapGenerator.IsRoomUnlocked(roomCoords.x - 1, roomCoords.y)) validDirections.Add(Vector2Int.left);
        if (!mapGenerator.IsRoomUnlocked(roomCoords.x, roomCoords.y + 1)) validDirections.Add(Vector2Int.up);
        if (!mapGenerator.IsRoomUnlocked(roomCoords.x, roomCoords.y - 1)) validDirections.Add(Vector2Int.down);

        Vector2Int chosenDirection = validDirections[Random.Range(0, validDirections.Count)];

        float totalMapWidth = roomSize.x * mapGridSize.x;
        float totalMapHeight = roomSize.y * mapGridSize.y;
        float mapCenterXOffset = totalMapWidth / 2f;
        float mapCenterYOffset = totalMapHeight / 2f;
        
        float roomStartX = roomCoords.x * roomSize.x - mapCenterXOffset;
        float roomStartY = roomCoords.y * roomSize.y - mapCenterYOffset;

        float spawnX = 0, spawnY = 0;

        if (chosenDirection == Vector2Int.right) // 오른쪽 경계
        {
            spawnX = roomStartX + roomSize.x - 1;
            spawnY = Random.Range(roomStartY, roomStartY + roomSize.y -1);
        }
        else if (chosenDirection == Vector2Int.left) // 왼쪽 경계
        {
            spawnX = roomStartX;
            spawnY = Random.Range(roomStartY, roomStartY + roomSize.y - 1);
        }
        else if (chosenDirection == Vector2Int.up) // 위쪽 경계
        {
            spawnX = Random.Range(roomStartX, roomStartX + roomSize.x - 1);
            spawnY = roomStartY + roomSize.y - 1;
        }
        else if (chosenDirection == Vector2Int.down) // 아래쪽 경계
        {
            spawnX = Random.Range(roomStartX, roomStartX + roomSize.x - 1);
            spawnY = roomStartY;
        }
        
        return mapGenerator.Tilemap.transform.TransformPoint(new Vector3(spawnX + 0.5f, spawnY + 0.5f, 0));
    }
}