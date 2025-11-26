using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour
{
    public Vector2Int RoomSize => roomSize;
    public Vector2Int MapGridSize => mapGridSize;
    public Tilemap Tilemap => tilemap;
    
    [Header("Tiles")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase wallTile;

    [Header("Map Generation Settings")]
    [SerializeField] private Vector2Int roomSize = new(50, 30);
    [SerializeField] private Vector2Int mapGridSize = new(5, 5);

    private int _totalMapWidth;
    private int _totalMapHeight;
    private int _mapCenterXOffset;
    private int _mapCenterYOffset;

    public void GenerateMap()
    {
        CalculateMapDimensions();

        // Generate all rooms as unlocked (no expansion mechanic)
        for (int x = 0; x < mapGridSize.x; x++)
        {
            for (int y = 0; y < mapGridSize.y; y++)
            {
                DrawRoom(x, y);
            }
        }
        
        tilemap.CompressBounds();
        SetupCameraController();
    }

    private void DrawRoom(int roomX, int roomY)
    {
        Vector3Int roomOrigin = new Vector3Int(roomX * roomSize.x - _mapCenterXOffset, roomY * roomSize.y - _mapCenterYOffset, 0);
        BoundsInt roomBounds = new BoundsInt(roomOrigin, new Vector3Int(roomSize.x, roomSize.y, 1));
        
        TileBase[] tiles = new TileBase[roomSize.x * roomSize.y];

        for (int i = 0; i < roomSize.x; i++)
        {
            for (int j = 0; j < roomSize.y; j++)
            {
                tiles[i + j * roomSize.x] = GetTileForPosition(i, j);
            }
        }
        
        tilemap.SetTilesBlock(roomBounds, tiles);
    }

    private void CalculateMapDimensions()
    {
        _totalMapWidth = roomSize.x * mapGridSize.x;
        _totalMapHeight = roomSize.y * mapGridSize.y;
        _mapCenterXOffset = _totalMapWidth / 2;
        _mapCenterYOffset = _totalMapHeight / 2;
    }
    
    private void SetupCameraController()
    {
        Bounds localBounds = tilemap.localBounds;
        Vector3 worldCenter = tilemap.transform.TransformPoint(localBounds.center);
        Vector3 worldSize = Vector3.Scale(localBounds.size, tilemap.transform.lossyScale);
        Bounds mapWorldBounds = new Bounds(worldCenter, worldSize);

        if (Camera.main != null && Camera.main.TryGetComponent<CameraController>(out var cameraController))
        {
            cameraController.SetBounds(mapWorldBounds);
        }
    }
    
    private TileBase GetTileForPosition(int x, int y)
    {
        bool isWall = x == 0 || x == roomSize.x - 1 || y == 0 || y == roomSize.y - 1;
        return isWall ? wallTile : groundTile;
    }

    public Vector2Int GetRoomCoordinates(Vector3 worldPosition)
    {
        Vector3 localPos = tilemap.transform.InverseTransformPoint(worldPosition);

        int cellX = Mathf.FloorToInt(localPos.x + _mapCenterXOffset);
        int cellY = Mathf.FloorToInt(localPos.y + _mapCenterYOffset);

        int roomX = cellX / roomSize.x;
        int roomY = cellY / roomSize.y;

        return new Vector2Int(
            Mathf.Clamp(roomX, 0, mapGridSize.x - 1),
            Mathf.Clamp(roomY, 0, mapGridSize.y - 1)
        );
    }
    
    // Compatibility method - always returns true since all rooms are unlocked
    public bool IsRoomUnlocked(int x, int y)
    {
        return x >= 0 && x < mapGridSize.x && y >= 0 && y < mapGridSize.y;
    }
    
}