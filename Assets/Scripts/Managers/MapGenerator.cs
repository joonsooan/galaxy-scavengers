using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum GradientMode
{
    Texture,
    Procedural
}

public class MapGenerator : MonoBehaviour
{
    public Vector2Int MapSize => new (width, height);
    public Tilemap GroundTilemap => groundTilemap;
    public Tilemap WallTilemap => lowWallTilemap;
    public IReadOnlyList<Vector2Int> EnemySpawnHolePositions => _enemySpawnHolePositions;
    
    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap lowWallTilemap;
    [SerializeField] private Tilemap highWallTilemap;
    [SerializeField] private Grid grid;
    
    [Header("Tiles")]
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase lowWallTile;
    [SerializeField] private TileBase highWallTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase enemyHomeTile;
    [SerializeField] private TileBase enemyTerritoryTile;

    [Header("Map Generation Settings")]
    [SerializeField] private int width = 250;
    [SerializeField] private int height = 150;

    [Header("Procedural Terrain Settings")]
    [SerializeField] private bool useProceduralTerrain = true;
    [SerializeField] private int seed = 0;
    [SerializeField] private bool randomSeed = true;
    
    [Header("Perlin Noise Settings")]
    [SerializeField] private float noiseScale = 50f;
    [SerializeField] private Vector2 noiseOffset = Vector2.zero;
    
    [Header("Gradient Map Settings")]
    [SerializeField] private bool useGradientMap;
    [SerializeField] private GradientMode gradientMode = GradientMode.Procedural;
    [SerializeField] private Texture2D gradientTexture;
    
    [Range(0f, 1f)]
    [SerializeField] private float gradientStrength = 0.5f;
    
    [SerializeField] private Vector2 gradientCenter = new (0.5f, 0.5f);
    
    [Range(1f, 10f)]
    [SerializeField] private float falloffExponent = 2f;
    
    [Header("Terrain Thresholds")]
    [Range(0f, 1f)]
    [SerializeField] private float lowWallThreshold = 0.5f;
    
    [Range(0f, 1f)]
    [SerializeField] private float highWallThreshold = 0.75f;
    
    [Header("Map Polishing Settings")]
    [SerializeField] private bool enableCenterCircle = true;
    [SerializeField] private int centerCircleRadius = 10;
    [SerializeField] private bool fillDisconnectedAreas = true;
    [SerializeField] private bool enableEnemySpawnHoles = true;
    [SerializeField] private int enemySpawnHoleMinimalAmount = 3;
    [SerializeField] private float enemySpawnPunchHoleRadius = 5f;

    [Header("Enemy Spawn Hole - Concentric Circle Settings")]
    [Range(1, 10)]
    [SerializeField] private int enemySpawnHolesPerSector = 1;
    
    [Range(0, 20)]
    [SerializeField] private int enemySpawnHoleStartCircleIndex = 3;
    
    [Header("Enemy Spawn Hole - Gizmo Settings")]
    [SerializeField] private bool showEnemySpawnHoleGizmos = true;
    [SerializeField] private Color enemySpawnHoleGizmoColor = new (1f, 0f, 0f, 0.5f);
    [SerializeField] private Color concentricCircleGizmoColor = new (1f, 1f, 0f, 0.3f);
    
    [Header("Enemy Territory Settings")]
    [SerializeField] private float enemyHomeRadius = 3f;
    [SerializeField] private float enemyTerritoryRadius = 6f;
    [SerializeField] private bool drawEnemyTerritoryTiles = true;

    private int _mapCenterXOffset;
    private int _mapCenterYOffset;
    private float[,] _noiseMap;
    private float[,] _gradientMap;
    
    private readonly List<Vector2Int> _enemySpawnHolePositions = new ();

    public Vector3 GetEnemySpawnHoleWorldPosition(Vector2Int holePosition)
    {
        if (grid == null)
        {
            return Vector3.zero;
        }
        Vector3Int cellPos = new Vector3Int(holePosition.x - _mapCenterXOffset, holePosition.y - _mapCenterYOffset, 0);
        return grid.GetCellCenterWorld(cellPos);
    }

    private void SetGroundTile(Vector3Int cellPosition, TileBase tile)
    {
        if (groundTilemap != null)
        {
            groundTilemap.SetTile(cellPosition, tile);
        }
        
        if (lowWallTilemap != null) lowWallTilemap.SetTile(cellPosition, null);
        if (highWallTilemap != null) highWallTilemap.SetTile(cellPosition, null);
    }

    private void SetWallTile(Vector3Int cellPosition, TileBase tile)
    {
        if (groundTilemap != null && !groundTilemap.HasTile(cellPosition))
        {
            groundTilemap.SetTile(cellPosition, groundTile);
        }

        if (tile == null)
        {
            if (lowWallTilemap != null) lowWallTilemap.SetTile(cellPosition, null);
            if (highWallTilemap != null) highWallTilemap.SetTile(cellPosition, null);
            return;
        }

        if (tile == highWallTile)
        {
            if (lowWallTilemap != null) lowWallTilemap.SetTile(cellPosition, lowWallTile);
            if (highWallTilemap != null) highWallTilemap.SetTile(cellPosition, highWallTile);
        }
        else if (tile == lowWallTile)
        {
            if (lowWallTilemap != null) lowWallTilemap.SetTile(cellPosition, lowWallTile);
            if (highWallTilemap != null) highWallTilemap.SetTile(cellPosition, null);
        }
        else if (tile == wallTile)
        {
            if (lowWallTilemap != null) lowWallTilemap.SetTile(cellPosition, wallTile);
            if (highWallTilemap != null) highWallTilemap.SetTile(cellPosition, wallTile);
        }
    }

    private TileBase GetTileAtPosition(Vector3Int cellPosition)
    {
        if (highWallTilemap != null && highWallTilemap.HasTile(cellPosition))
        {
            return highWallTilemap.GetTile(cellPosition);
        }
        if (lowWallTilemap != null && lowWallTilemap.HasTile(cellPosition))
        {
            return lowWallTilemap.GetTile(cellPosition);
        }
        if (groundTilemap != null && groundTilemap.HasTile(cellPosition))
        {
            return groundTilemap.GetTile(cellPosition);
        }
        return null;
    }

    public void GenerateMap()
    {
        CalculateMapDimensions();

        if (randomSeed)
        {
            seed = Random.Range(int.MinValue, int.MaxValue);
        }

        if (useProceduralTerrain)
        {
            GenerateNoiseMap();
        }

        if (useGradientMap)
        {
            GenerateGradientMap();
        }

        Vector3Int mapOrigin = new Vector3Int(-_mapCenterXOffset, -_mapCenterYOffset, 0);
        BoundsInt mapBounds = new BoundsInt(mapOrigin, new Vector3Int(width, height, 1));
        
        TileBase[] groundTiles = new TileBase[width * height];
        TileBase[] lowWallTiles = new TileBase[width * height];
        TileBase[] highWallTiles = new TileBase[width * height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int index = x + y * width;
                
                groundTiles[index] = groundTile;
                bool isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                if (isBorder)
                {
                    lowWallTiles[index] = wallTile;
                    continue;
                }
                
                float noiseVal = GetNoiseValueAt(x, y);

                if (noiseVal >= lowWallThreshold)
                {
                    lowWallTiles[index] = lowWallTile;
                }
                else
                {
                    lowWallTiles[index] = null;
                }

                if (noiseVal >= highWallThreshold)
                {
                    highWallTiles[index] = highWallTile;
                }
                else
                {
                    highWallTiles[index] = null;
                }
            }
        }
        
        if (groundTilemap != null) groundTilemap.SetTilesBlock(mapBounds, groundTiles);
        if (lowWallTilemap != null) lowWallTilemap.SetTilesBlock(mapBounds, lowWallTiles);
        if (highWallTilemap != null) highWallTilemap.SetTilesBlock(mapBounds, highWallTiles);
        
        PolishMap();
        CopyTilesToBuildingManagerGroundTilemap(mapBounds);
        
        if (groundTilemap != null) groundTilemap.CompressBounds();
        if (lowWallTilemap != null) lowWallTilemap.CompressBounds();
        if (highWallTilemap != null) highWallTilemap.CompressBounds();
        
        if (FogOfWarManager.Instance != null)
        {
            FogOfWarManager.Instance.RefreshFogOfWar();
        }
    }
    
    private float GetNoiseValueAt(int x, int y)
    {
        if (!useProceduralTerrain || _noiseMap == null) return 0f;

        float noiseValue = _noiseMap[x, y];
        
        if (useGradientMap && _gradientMap != null)
        {
            float gradientValue = _gradientMap[x, y];
            float combinedValue = noiseValue + (gradientValue - 0.5f) * gradientStrength;
            noiseValue = Mathf.Clamp01(combinedValue);
        }
        
        return 1f - noiseValue;
    }

    private void PolishMap()
    {
        if (enableCenterCircle) PunchCenterCircle();
        if (fillDisconnectedAreas) FillDisconnectedAreas();
        if (enableEnemySpawnHoles) PunchEnemySpawnHoles();
        if (fillDisconnectedAreas) FillDisconnectedAreas();
        ConnectWallsToBorders();
    }

    private TileBase GetTileForSmoothTransition(int x, int y, float transitionFactor, TileBase originalTile)
    {
        if (transitionFactor < 0.5f) return groundTile;
        
        if (originalTile == highWallTile)
        {
            if (transitionFactor < 0.8f) return lowWallTile; 
            return highWallTile;
        }
        
        if (originalTile == lowWallTile)
        {
            return lowWallTile;
        }

        return groundTile;
    }

    private void PunchCenterCircle()
    {
        int centerX = width / 2;
        int centerY = height / 2;
        
        float transitionStartRatio = 0.7f;
        float transitionEnd = centerCircleRadius;
        float transitionStart = centerCircleRadius * transitionStartRatio;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    continue;
                
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                
                if (distance <= centerCircleRadius)
                {
                    Vector3Int cellPos = new Vector3Int(x - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                    
                    if (distance <= transitionStart)
                    {
                        SetGroundTile(cellPos, groundTile);
                    }
                    else
                    {
                        TileBase originalTile = GetTileForPosition(x, y);
                        float transitionFactor = (distance - transitionStart) / (transitionEnd - transitionStart);
                        TileBase transitionTile = GetTileForSmoothTransition(x, y, transitionFactor, originalTile);
                        
                        if (IsTerrainTile(transitionTile))
                        {
                            SetWallTile(cellPos, transitionTile);
                        }
                        else
                        {
                            SetGroundTile(cellPos, transitionTile);
                        }
                    }
                }
            }
        }
    }

    private void FillDisconnectedAreas()
    {
        bool[,] connectedToCenter = new bool[width, height];
        
        int centerX = width / 2;
        int centerY = height / 2;
        
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        
        Vector3Int centerCellPos = new Vector3Int(centerX - _mapCenterXOffset, centerY - _mapCenterYOffset, 0);
        TileBase centerTile = GetTileAtPosition(centerCellPos);
        
        if (!IsTerrainTile(centerTile))
        {
            queue.Enqueue(new Vector2Int(centerX, centerY));
            connectedToCenter[centerX, centerY] = true;
        }
        
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int x = current.x;
            int y = current.y;
            
            Vector2Int[] neighbors = {
                new (x + 1, y),
                new (x - 1, y),
                new (x, y + 1),
                new (x, y - 1)
            };
            
            foreach (Vector2Int neighbor in neighbors)
            {
                int nx = neighbor.x;
                int ny = neighbor.y;
                
                if (nx < 1 || nx >= width - 1 || ny < 1 || ny >= height - 1)
                    continue;
                
                if (connectedToCenter[nx, ny])
                    continue;
                
                Vector3Int neighborCellPos = new Vector3Int(nx - _mapCenterXOffset, ny - _mapCenterYOffset, 0);
                TileBase neighborTile = GetTileAtPosition(neighborCellPos);
                
                if (!IsTerrainTile(neighborTile))
                {
                    connectedToCenter[nx, ny] = true;
                    queue.Enqueue(neighbor);
                }
            }
        }
        
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                Vector3Int cellPos = new Vector3Int(x - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                
                if (!connectedToCenter[x, y])
                {
                    if (!IsTerrainCell(cellPos))
                    {
                        SetWallTile(cellPos, lowWallTile != null ? lowWallTile : wallTile);
                    }
                }
            }
        }
    }

    private void PunchEnemySpawnHoles()
    {
        _enemySpawnHolePositions.Clear();
        
        MapObjectSpawner mapObjectSpawner = FindFirstObjectByType<MapObjectSpawner>();
        if (mapObjectSpawner == null)
        {
            return;
        }
        
        List<float> divisionRadii = mapObjectSpawner.GetDivisionRadii();
        if (divisionRadii == null || divisionRadii.Count < 2)
        {
            Debug.LogWarning("[MapGenerator] MapObjectSpawner concentric circles not initialized. Enemy spawn holes may not work correctly.");
            return;
        }
        
        int centerX = width / 2;
        int centerY = height / 2;
        
        int startIndex = Mathf.Clamp(enemySpawnHoleStartCircleIndex, 0, divisionRadii.Count - 2);
        
        for (int sectorIndex = startIndex; sectorIndex < divisionRadii.Count - 1; sectorIndex++)
        {
            float minRadius = divisionRadii[sectorIndex];
            float maxRadius = divisionRadii[sectorIndex + 1];
            
            for (int holeIndex = 0; holeIndex < enemySpawnHolesPerSector; holeIndex++)
            {
                Vector2Int? validHolePos = FindValidHolePosition(centerX, centerY, minRadius, maxRadius, enemySpawnPunchHoleRadius);
                if (validHolePos.HasValue)
                {
                    _enemySpawnHolePositions.Add(validHolePos.Value);
                    PunchHoleWithThreshold(validHolePos.Value.x, validHolePos.Value.y, enemySpawnPunchHoleRadius);
                }
            }
        }
        
        while (_enemySpawnHolePositions.Count < enemySpawnHoleMinimalAmount && divisionRadii.Count > 1)
        {
            float minRadius = divisionRadii[startIndex];
            float maxRadius = divisionRadii[divisionRadii.Count - 1];
            
            Vector2Int? validHolePos = FindValidHolePosition(centerX, centerY, minRadius, maxRadius, enemySpawnPunchHoleRadius);
            if (validHolePos.HasValue)
            {
                _enemySpawnHolePositions.Add(validHolePos.Value);
                PunchHoleWithThreshold(validHolePos.Value.x, validHolePos.Value.y, enemySpawnPunchHoleRadius);
            }
            else
            {
                break;
            }
        }
    }
    
    private Vector2Int? FindValidHolePosition(int centerX, int centerY, float minRadius, float maxRadius, float holeRadius)
    {
        int maxAttempts = 1000;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(minRadius, maxRadius);
            
            int holeCenterX = centerX + Mathf.RoundToInt(Mathf.Cos(angle) * distance);
            int holeCenterY = centerY + Mathf.RoundToInt(Mathf.Sin(angle) * distance);
            
            int holeRadiusInt = Mathf.CeilToInt(holeRadius);
            holeCenterX = Mathf.Clamp(holeCenterX, holeRadiusInt + 1, width - holeRadiusInt - 2);
            holeCenterY = Mathf.Clamp(holeCenterY, holeRadiusInt + 1, height - holeRadiusInt - 2);
            
            if (IsValidHolePosition(holeCenterX, holeCenterY, holeRadius))
            {
                return new Vector2Int(holeCenterX, holeCenterY);
            }
        }
        
        return null;
    }
    
    private bool IsValidHolePosition(int centerX, int centerY, float radius)
    {
        float innerRadius = radius * 0.7f;
        int groundCellCount = 0;
        int totalCellCount = 0;
        
        int radiusInt = Mathf.CeilToInt(radius);
        for (int x = centerX - radiusInt; x <= centerX + radiusInt; x++)
        {
            for (int y = centerY - radiusInt; y <= centerY + radiusInt; y++)
            {
                if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1)
                    continue;
                
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                
                if (distance <= innerRadius)
                {
                    totalCellCount++;
                    Vector3Int cellPos = new Vector3Int(x - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                    
                    if (!IsTerrainCell(cellPos)) 
                    {
                        groundCellCount++;
                    }
                }
            }
        }
        
        if (totalCellCount == 0) return false;
        
        float groundRatio = (float)groundCellCount / totalCellCount;
        return groundRatio >= 0.6f;
    }
    
    private void DrawCircleGizmo(Vector3 center, float radius)
    {
        int segments = 32;
        float angleStep = (Mathf.PI * 2f) / segments;
        
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
    
    private void ConnectWallsToBorders()
    {
        for (int y = 0; y < height; y++)
        {
            int innerX = 1;
            Vector3Int innerPos = new Vector3Int(innerX - _mapCenterXOffset, y - _mapCenterYOffset, 0);
            if (GetTileAtPosition(innerPos) == highWallTile)
            {
                Vector3Int borderPos = new Vector3Int(-_mapCenterXOffset, y - _mapCenterYOffset, 0);
                SetWallTile(borderPos, highWallTile);
            }
        }
        for (int y = 0; y < height; y++)
        {
            int innerX = width - 2;
            Vector3Int innerPos = new Vector3Int(innerX - _mapCenterXOffset, y - _mapCenterYOffset, 0);
            if (GetTileAtPosition(innerPos) == highWallTile)
            {
                Vector3Int borderPos = new Vector3Int((width - 1) - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                SetWallTile(borderPos, highWallTile);
            }
        }
        for (int x = 0; x < width; x++)
        {
            int innerY = 1;
            Vector3Int innerPos = new Vector3Int(x - _mapCenterXOffset, innerY - _mapCenterYOffset, 0);
            if (GetTileAtPosition(innerPos) == highWallTile)
            {
                Vector3Int borderPos = new Vector3Int(x - _mapCenterXOffset, -_mapCenterYOffset, 0);
                SetWallTile(borderPos, highWallTile);
            }
        }
        for (int x = 0; x < width; x++)
        {
            int innerY = height - 2;
            Vector3Int innerPos = new Vector3Int(x - _mapCenterXOffset, innerY - _mapCenterYOffset, 0);
            if (GetTileAtPosition(innerPos) == highWallTile)
            {
                Vector3Int borderPos = new Vector3Int(x - _mapCenterXOffset, (height - 1) - _mapCenterYOffset, 0);
                SetWallTile(borderPos, highWallTile);
            }
        }
    }

    private void PunchHoleWithThreshold(int centerX, int centerY, float radius)
    {
        int radiusInt = Mathf.CeilToInt(radius);
        for (int x = centerX - radiusInt; x <= centerX + radiusInt; x++)
        {
            for (int y = centerY - radiusInt; y <= centerY + radiusInt; y++)
            {
                if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1)
                    continue;
                
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                
                if (distance <= radius)
                {
                    Vector3Int cellPos = new Vector3Int(x - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                    SetGroundTile(cellPos, groundTile);
                }
            }
        }
        
        float edgeTolerance = 1.5f;
        float edgeMin = radius - edgeTolerance;
        float edgeMax = radius + edgeTolerance;
        
        for (int x = centerX - radiusInt - 1; x <= centerX + radiusInt + 1; x++)
        {
            for (int y = centerY - radiusInt - 1; y <= centerY + radiusInt + 1; y++)
            {
                if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1)
                    continue;
                
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                
                if (distance >= edgeMin && distance <= edgeMax)
                {
                    Vector3Int cellPos = new Vector3Int(x - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                    TileBase tile = GetTileAtPosition(cellPos);
                    
                    if (tile == highWallTile)
                    {
                        SetWallTile(cellPos, lowWallTile != null ? lowWallTile : groundTile);
                    }
                }
            }
        }
    }

    private void GenerateNoiseMap()
    {
        _noiseMap = new float[width, height];
        
        System.Random prng = new System.Random(seed);
        float offsetX = prng.Next(-100000, 100000) + noiseOffset.x;
        float offsetY = prng.Next(-100000, 100000) + noiseOffset.y;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float sampleX = (x / noiseScale) + offsetX;
                float sampleY = (y / noiseScale) + offsetY;

                float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
                
                _noiseMap[x, y] = perlinValue;
            }
        }
    }

    private void GenerateGradientMap()
    {
        _gradientMap = new float[width, height];

        if (gradientMode == GradientMode.Texture && gradientTexture != null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int texX = Mathf.RoundToInt(x * (float)gradientTexture.width / width);
                    int texY = Mathf.RoundToInt(y * (float)gradientTexture.height / height);
                    
                    texX = Mathf.Clamp(texX, 0, gradientTexture.width - 1);
                    texY = Mathf.Clamp(texY, 0, gradientTexture.height - 1);
                    
                    Color pixelColor = gradientTexture.GetPixel(texX, texY);
                    _gradientMap[x, y] = pixelColor.grayscale;
                }
            }
        }
        else if (gradientMode == GradientMode.Procedural)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float normalizedX = (float)x / width;
                    float normalizedY = (float)y / height;

                    float dx = normalizedX - gradientCenter.x;
                    float dy = normalizedY - gradientCenter.y;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float maxDistance = Mathf.Sqrt(0.5f * 0.5f + 0.5f * 0.5f);
                    float normalizedDistance = Mathf.Clamp01(distance / maxDistance);

                    float gradientValue = Mathf.Pow(normalizedDistance, falloffExponent);
                    
                    _gradientMap[x, y] = 1f - gradientValue;
                }
            }
        }
        else
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _gradientMap[x, y] = 0.5f;
                }
            }
        }
    }

    private void CalculateMapDimensions()
    {
        _mapCenterXOffset = width / 2;
        _mapCenterYOffset = height / 2;
    }
    
    private TileBase GetTileForPosition(int x, int y)
    {
        bool isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
        if (isBorder)
        {
            return wallTile;
        }

        if (useProceduralTerrain && _noiseMap != null)
        {
            float noiseValue = _noiseMap[x, y];
            
            if (useGradientMap && _gradientMap != null)
            {
                float gradientValue = _gradientMap[x, y];
                float combinedValue = noiseValue + (gradientValue - 0.5f) * gradientStrength;
                
                combinedValue = Mathf.Clamp01(combinedValue);
                noiseValue = combinedValue;
            }
            
            noiseValue = 1f - noiseValue;
            
            if (noiseValue >= highWallThreshold)
            {
                return highWallTile != null ? highWallTile : groundTile;
            }
            else if (noiseValue >= lowWallThreshold)
            {
                return lowWallTile != null ? lowWallTile : groundTile;
            }
            else
            {
                return groundTile;
            }
        }

        return groundTile;
    }

    public bool IsPositionInBounds(Vector3 worldPosition)
    {
        if (groundTilemap == null) return false;
        Vector3 localPos = groundTilemap.transform.InverseTransformPoint(worldPosition);
        int cellX = Mathf.FloorToInt(localPos.x + _mapCenterXOffset);
        int cellY = Mathf.FloorToInt(localPos.y + _mapCenterYOffset);
        return cellX >= 0 && cellX < width && cellY >= 0 && cellY < height;
    }
    
    public bool IsTerrainTile(TileBase tile)
    {
        if (tile == null) return false;
        return tile == wallTile || tile == lowWallTile || tile == highWallTile;
    }
    
    public bool IsTerrainCell(Vector3Int cellPosition)
    {
        if (highWallTilemap != null && highWallTilemap.HasTile(cellPosition)) return true;
        if (lowWallTilemap != null && lowWallTilemap.HasTile(cellPosition)) return true;
        return false;
    }
    
    private void CopyTilesToBuildingManagerGroundTilemap(BoundsInt mapBounds)
    {
        if (BuildingManager.Instance == null || BuildingManager.Instance.GroundTilemap == null || groundTilemap == null) return;
        
        Tilemap bmTilemap = BuildingManager.Instance.GroundTilemap;
        TileBase[] tiles = new TileBase[mapBounds.size.x * mapBounds.size.y];
        int index = 0;
        foreach (Vector3Int pos in mapBounds.allPositionsWithin)
        {
            tiles[index++] = groundTilemap.GetTile(pos);
        }
        
        bmTilemap.SetTilesBlock(mapBounds, tiles);
        bmTilemap.CompressBounds();
    }
    
    public void DrawEnemyTerritoryTiles()
    {
        if (!drawEnemyTerritoryTiles || enemyHomeTile == null || enemyTerritoryTile == null)
        {
            return;
        }
        
        if (groundTilemap == null || grid == null)
        {
            return;
        }
        
        if (_enemySpawnHolePositions == null || _enemySpawnHolePositions.Count == 0)
        {
            return;
        }
        
        HashSet<Vector3Int> homeTileCells = new HashSet<Vector3Int>();
        
        foreach (Vector2Int holePos in _enemySpawnHolePositions)
        {
            Vector3 worldPos = GetEnemySpawnHoleWorldPosition(holePos);
            Vector3Int centerCell = grid.WorldToCell(worldPos);
            
            int homeRadiusCells = Mathf.CeilToInt(enemyHomeRadius);
            for (int x = -homeRadiusCells; x <= homeRadiusCells; x++)
            {
                for (int y = -homeRadiusCells; y <= homeRadiusCells; y++)
                {
                    Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                    Vector3 cellWorldPos = grid.GetCellCenterWorld(cell);
                    float distance = Vector3.Distance(worldPos, cellWorldPos);
                    
                    if (distance <= enemyHomeRadius)
                    {
                        if (groundTilemap.HasTile(cell))
                        {
                            groundTilemap.SetTile(cell, enemyHomeTile);
                            homeTileCells.Add(cell);
                        }
                    }
                }
            }
            
            int territoryRadiusCells = Mathf.CeilToInt(enemyTerritoryRadius);
            for (int x = -territoryRadiusCells; x <= territoryRadiusCells; x++)
            {
                for (int y = -territoryRadiusCells; y <= territoryRadiusCells; y++)
                {
                    Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                    
                    if (homeTileCells.Contains(cell))
                    {
                        continue;
                    }
                    
                    Vector3 cellWorldPos = grid.GetCellCenterWorld(cell);
                    float distance = Vector3.Distance(worldPos, cellWorldPos);
                    
                    if (distance <= enemyTerritoryRadius && distance > enemyHomeRadius)
                    {
                        if (groundTilemap.HasTile(cell))
                        {
                            TileBase existingTile = groundTilemap.GetTile(cell);
                            if (existingTile != enemyHomeTile)
                            {
                                groundTilemap.SetTile(cell, enemyTerritoryTile);
                            }
                        }
                    }
                }
            }
        }
        
        if (BuildingManager.Instance != null && BuildingManager.Instance.GroundTilemap != null)
        {
            Tilemap buildingManagerGroundTilemap = BuildingManager.Instance.GroundTilemap;
            foreach (Vector2Int holePos in _enemySpawnHolePositions)
            {
                Vector3 worldPos = GetEnemySpawnHoleWorldPosition(holePos);
                Vector3Int centerCell = grid.WorldToCell(worldPos);
                
                int homeRadiusCells = Mathf.CeilToInt(enemyHomeRadius);
                for (int x = -homeRadiusCells; x <= homeRadiusCells; x++)
                {
                    for (int y = -homeRadiusCells; y <= homeRadiusCells; y++)
                    {
                        Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                        Vector3 cellWorldPos = grid.GetCellCenterWorld(cell);
                        float distance = Vector3.Distance(worldPos, cellWorldPos);
                        
                        if (distance <= enemyHomeRadius && buildingManagerGroundTilemap.HasTile(cell))
                        {
                            buildingManagerGroundTilemap.SetTile(cell, enemyHomeTile);
                        }
                    }
                }
                
                int territoryRadiusCells = Mathf.CeilToInt(enemyTerritoryRadius);
                for (int x = -territoryRadiusCells; x <= territoryRadiusCells; x++)
                {
                    for (int y = -territoryRadiusCells; y <= territoryRadiusCells; y++)
                    {
                        Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                        
                        if (homeTileCells.Contains(cell))
                        {
                            continue;
                        }
                        
                        TileBase existingTile = buildingManagerGroundTilemap.GetTile(cell);
                        if (existingTile == enemyHomeTile)
                        {
                            continue;
                        }
                        
                        Vector3 cellWorldPos = grid.GetCellCenterWorld(cell);
                        float distance = Vector3.Distance(worldPos, cellWorldPos);
                        
                        if (distance <= enemyTerritoryRadius && distance > enemyHomeRadius && buildingManagerGroundTilemap.HasTile(cell))
                        {
                            buildingManagerGroundTilemap.SetTile(cell, enemyTerritoryTile);
                        }
                    }
                }
            }
        }
        
        if (groundTilemap != null)
        {
            groundTilemap.RefreshAllTiles();
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showEnemySpawnHoleGizmos) return;
        
        if (grid == null)
        {
            grid = FindFirstObjectByType<Grid>();
        }
        
        if (grid == null) return;
        
        if (_mapCenterXOffset == 0 && _mapCenterYOffset == 0)
        {
            CalculateMapDimensions();
        }
        
        int centerX = width / 2;
        int centerY = height / 2;
        Vector3 centerWorld = grid.GetCellCenterWorld(new Vector3Int(centerX - _mapCenterXOffset, centerY - _mapCenterYOffset, 0));
        
        float cellSize = grid.cellSize.x;
        
        MapObjectSpawner mapObjectSpawner = FindFirstObjectByType<MapObjectSpawner>();
        if (mapObjectSpawner != null)
        {
            List<float> divisionRadii = mapObjectSpawner.GetDivisionRadii();
            if (divisionRadii != null && divisionRadii.Count > 0)
            {
                Gizmos.color = concentricCircleGizmoColor;
                foreach (float radius in divisionRadii)
                {
                    float radiusWorld = radius * cellSize;
                    DrawCircleGizmo(centerWorld, radiusWorld);
                }
            }
        }
        
        if (_enemySpawnHolePositions.Count > 0)
        {
            Gizmos.color = enemySpawnHoleGizmoColor;
            foreach (var holePos in _enemySpawnHolePositions)
            {
                Vector3 holeWorldPos = grid.GetCellCenterWorld(new Vector3Int(holePos.x - _mapCenterXOffset, holePos.y - _mapCenterYOffset, 0));
                float holeRadiusWorld = enemySpawnPunchHoleRadius * cellSize;
                DrawCircleGizmo(holeWorldPos, holeRadiusWorld);
            }
        }
    }
}