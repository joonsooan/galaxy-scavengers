using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

[Serializable]
public class ResourceSpawnSettings
{
    [Header("Resource Type")]
    public ResourceType resourceType;
    public RuleTile resourceRuleTile;
    public GameObject resourcePrefab;
    
    [Header("Spawn Frequency")]
    [Tooltip("Percentage chance (0-100) for this resource type to spawn in a spawnable circle.")]
    [Range(0f, 100f)]
    public float spawnFrequencyPercent = 50f;
    
    [Header("Richness Settings")]
    [Tooltip("Base richness (amount per tile) at map center.")]
    [Range(0, 1000)]
    public int baseRichness = 100;
}

[Serializable]
public class ResourceCircle
{
    public Vector2Int center;
    public float radius;
    public ResourceType resourceType;
}

public class MapObjectSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Grid grid;
    [SerializeField] private Transform parentTransform;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private Tilemap resourceTilemap;
    
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    public static MapObjectSpawner Instance => _instance;
    
    [Header("Resource Settings")]
    [SerializeField] private List<ResourceSpawnSettings> resourceSettings = new ();
    
    [Header("Concentric Circle Division")]
    [Range(0, 20)]
    [SerializeField] private int numberOfCircles = 10;
    
    [Range(0, 50)]
    [SerializeField] private int borderPadding = 10;
    
    [Header("Resource Circle Spawning")]
    [Range(0, 20)]
    [SerializeField] private float minCircleRadius = 5f;
    
    [Range(0, 50)]
    [SerializeField] private float maxCircleRadius = 15f;
    
    [Range(0f, 100f)]
    [SerializeField] private float circleSpawnPercentage = 30f;
    
    [Range(10, 200)]
    [SerializeField] private int maxCircles = 50;
    
    [Range(10, 1000)]
    [SerializeField] private int maxResourcesPerCircle = 200;
    
    [Header("Starting Area")]
    [Range(0, 50)]
    [SerializeField] private int minSpawnRadius = 5;
    
    [Range(0, 50)]
    [SerializeField] private int startingAreaRadius = 15;
    
    [Range(0, 20)]
    [SerializeField] private int startingAreaCircleCount = 3;
    
    [Range(0f, 1f)]
    [SerializeField] private float startingAreaMinDistance = 0.3f;
    
    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color divisionCircleColor = new(1f, 1f, 0f, 0.3f);
    [SerializeField] private Color resourceCircleColor = new(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color startingAreaMinRadiusColor = new(1f, 0f, 0f, 0.5f);
    [SerializeField] private Color startingAreaMaxRadiusColor = new(0f, 0f, 1f, 0.5f);
    
    private Vector2Int _mapCenter;
    private int _mapWidth;
    private int _mapHeight;
    private float _maxMapRadius;
    private float _radiusStep;
    private readonly List<float> _divisionRadii = new ();
    private readonly List<float> _sectorPowerValues = new ();
    private readonly List<ResourceCircle> _resourceCircles = new ();
    private readonly List<ResourceNode> _pendingResourceNodes = new ();
    private readonly Dictionary<Vector3Int, ResourceType> _resourceTilePositions = new ();
    private static MapObjectSpawner _instance;

    public static event Action OnAllObjectsSpawned;
    private static bool isSpawningResources;
    public static bool IsSpawningResources => isSpawningResources;
    
    public Tilemap ResourceTilemap => resourceTilemap;
    
    public List<float> GetDivisionRadii()
    {
        if (_divisionRadii == null || _divisionRadii.Count == 0)
        {
            InitializeConcentricCircles();
        }
        return _divisionRadii;
    }
    
    private void InitializeConcentricCircles()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<MapGenerator>();
            if (mapGenerator == null)
            {
                Debug.LogWarning("[MapObjectSpawner] MapGenerator not found. Cannot initialize concentric circles.");
                return;
            }
        }
        
        Vector2Int mapSize = mapGenerator.MapSize;
        _mapWidth = mapSize.x;
        _mapHeight = mapSize.y;
        _mapCenter = Vector2Int.zero; // Map center is at (0, 0)
        
        float halfWidth = (_mapWidth / 2f) - borderPadding;
        float halfHeight = (_mapHeight / 2f) - borderPadding;
        _maxMapRadius = Mathf.Min(halfWidth, halfHeight);
        
        DivideMapIntoConcentricCircles();
    }
    
    public void SpawnResources()
    {
        Vector2Int mapSize = mapGenerator.MapSize;
        _mapWidth = mapSize.x;
        _mapHeight = mapSize.y;
        _mapCenter = Vector2Int.zero; // Map center is at (0, 0)
        
        float halfWidth = (_mapWidth / 2f) - borderPadding;
        float halfHeight = (_mapHeight / 2f) - borderPadding;
        _maxMapRadius = Mathf.Min(halfWidth, halfHeight);
        
        _divisionRadii.Clear();
        _sectorPowerValues.Clear();
        _resourceCircles.Clear();
        _pendingResourceNodes.Clear();
        
        isSpawningResources = true;
        
        if (FogOfWarManager.Instance != null)
        {
            FogOfWarManager.SetSuppressVisibilityEvents(true);
        }
        
        try
        {
            DivideMapIntoConcentricCircles();
            SpawnResourceCircles();
            SpawnStartingAreaCircles();
            SpawnResourcesInCircles();
            RegisterBufferedResources();
            
            // Initialize rule tiles after all resources are placed
            InitializeResourceRuleTiles();
            
            OnAllObjectsSpawned.Invoke();
            
            Debug.Log($"Spawned {_pendingResourceNodes.Count} resource nodes. Created {_resourceCircles.Count} resource circles.");
        }
        finally
        {
            isSpawningResources = false;
            
            if (FogOfWarManager.Instance != null)
            {
                FogOfWarManager.SetSuppressVisibilityEvents(false);
                
                StartCoroutine(DeferredFogRefresh());
            }
        }
    }
    
    private void DivideMapIntoConcentricCircles()
    {
        _divisionRadii.Clear();
        _sectorPowerValues.Clear();
        
        _radiusStep = _maxMapRadius / numberOfCircles;
        
        for (int i = 0; i <= numberOfCircles; i++)
        {
            float radius = i * _radiusStep;
            _divisionRadii.Add(radius);
            
            float normalizedDistance = radius / _maxMapRadius;
            float powerValue = 1f + normalizedDistance;
            _sectorPowerValues.Add(powerValue);
        }
    }
    
    private void SpawnStartingAreaCircles()
    {
        if (startingAreaCircleCount <= 0) return;
        if (startingAreaRadius <= minSpawnRadius) return;
        
        int startingAreaCirclesSpawned = 0;
        int attempts = 0;
        int maxAttempts = startingAreaCircleCount * 50;
        
        while (startingAreaCirclesSpawned < startingAreaCircleCount && attempts < maxAttempts)
        {
            attempts++;
            
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float minDist = minSpawnRadius;
            float maxDist = startingAreaRadius;
            float distance = Random.Range(minDist, maxDist);
            
            int centerX = Mathf.RoundToInt(Mathf.Cos(angle) * distance);
            int centerY = Mathf.RoundToInt(Mathf.Sin(angle) * distance);
            Vector2Int center = new Vector2Int(centerX, centerY);
            
            float distanceFromCenter = Vector2.Distance(center, _mapCenter);
            if (distanceFromCenter < minSpawnRadius)
            {
                if (distanceFromCenter > 0)
                {
                    float scale = minSpawnRadius / distanceFromCenter;
                    center = new Vector2Int(
                        Mathf.RoundToInt(center.x * scale),
                        Mathf.RoundToInt(center.y * scale)
                    );
                }
                else
                {
                    center = new Vector2Int(minSpawnRadius, 0);
                }
            }
            
            float maxAllowedRadius = startingAreaRadius - Vector2.Distance(center, _mapCenter);
            if (maxAllowedRadius < minCircleRadius) continue;
            
            float radius = Random.Range(minCircleRadius, Mathf.Min(maxCircleRadius, maxAllowedRadius));
            
            if (CircleOverlapsTerrain(center, radius))
            {
                bool foundValid = false;
                for (float testRadius = radius * 0.9f; testRadius >= minCircleRadius; testRadius -= 0.5f)
                {
                    if (!CircleOverlapsTerrain(center, testRadius))
                    {
                        radius = testRadius;
                        foundValid = true;
                        break;
                    }
                }
                
                if (!foundValid) continue;
            }
            
            bool tooClose = false;
            foreach (var existingCircle in _resourceCircles)
            {
                float circleDistance = Vector2.Distance(center, existingCircle.center);
                float minDistance = (radius + existingCircle.radius) * startingAreaMinDistance;
                if (circleDistance < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }
            
            if (tooClose) continue;
            
            ResourceType? selectedType = SelectResourceTypeForCircle(center);
            if (!selectedType.HasValue) continue;
            
            ResourceCircle circle = new ResourceCircle
            {
                center = center,
                radius = radius,
                resourceType = selectedType.Value
            };
            
            _resourceCircles.Add(circle);
            startingAreaCirclesSpawned++;
        }
    }
    
    private void SpawnResourceCircles()
    {
        _resourceCircles.Clear();
        
        float mapArea = (_mapWidth - borderPadding * 2) * (_mapHeight - borderPadding * 2);
        float avgCircleArea = Mathf.PI * ((minCircleRadius + maxCircleRadius) / 2f) * ((minCircleRadius + maxCircleRadius) / 2f);
        int maxPossibleCircles = Mathf.RoundToInt(mapArea / avgCircleArea);
        int circlesToSpawn = Mathf.RoundToInt(maxPossibleCircles * (circleSpawnPercentage / 100f));
        
        circlesToSpawn = Mathf.Min(circlesToSpawn, maxCircles);
        circlesToSpawn = Mathf.Max(circlesToSpawn, 10);
        
        int attempts = 0;
        int maxAttempts = circlesToSpawn * 30;
        
        while (_resourceCircles.Count < circlesToSpawn && attempts < maxAttempts)
        {
            attempts++;
            
            int centerX = Random.Range(-(_mapWidth / 2) + borderPadding, (_mapWidth / 2) - borderPadding);
            int centerY = Random.Range(-(_mapHeight / 2) + borderPadding, (_mapHeight / 2) - borderPadding);
            Vector2Int center = new Vector2Int(centerX, centerY);
            
            float distanceFromCenter = Vector2.Distance(center, _mapCenter);
            float minRequiredDistance = startingAreaRadius + maxCircleRadius;
            if (distanceFromCenter < minRequiredDistance)
            {
                if (distanceFromCenter > 0)
                {
                    float scale = minRequiredDistance / distanceFromCenter;
                    center = new Vector2Int(
                        Mathf.RoundToInt(center.x * scale),
                        Mathf.RoundToInt(center.y * scale)
                    );
                }
                else
                {
                    center = new Vector2Int(Mathf.CeilToInt(minRequiredDistance), 0);
                }
                
                if (center.x < -(_mapWidth / 2) + borderPadding || center.x >= (_mapWidth / 2) - borderPadding ||
                    center.y < -(_mapHeight / 2) + borderPadding || center.y >= (_mapHeight / 2) - borderPadding)
                {
                    continue;
                }
            }
            
            float radius = Random.Range(minCircleRadius, maxCircleRadius);
            
            if (CircleOverlapsTerrain(center, radius))
            {
                bool foundValid = false;
                
                for (float testRadius = radius * 0.9f; testRadius >= minCircleRadius; testRadius -= 0.5f)
                {
                    if (!CircleOverlapsTerrain(center, testRadius))
                    {
                        radius = testRadius;
                        foundValid = true;
                        break;
                    }
                }
                
                if (!foundValid)
                {
                    for (int offsetX = -5; offsetX <= 5 && !foundValid; offsetX++)
                    {
                        for (int offsetY = -5; offsetY <= 5; offsetY++)
                        {
                            Vector2Int testCenter = center + new Vector2Int(offsetX, offsetY);
                            if (!CircleOverlapsTerrain(testCenter, radius))
                            {
                                center = testCenter;
                                foundValid = true;
                                break;
                            }
                        }
                    }
                }
                
                if (!foundValid) continue;
            }
            
            bool tooClose = false;
            foreach (var existingCircle in _resourceCircles)
            {
                float distance = Vector2.Distance(center, existingCircle.center);
                float minDistance = (radius + existingCircle.radius) * 0.5f;
                if (distance < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }
            
            if (tooClose) continue;
            
            ResourceType? selectedType = SelectResourceTypeForCircle(center);
            if (!selectedType.HasValue) continue;
            
            ResourceCircle circle = new ResourceCircle
            {
                center = center,
                radius = radius,
                resourceType = selectedType.Value
            };
            
            _resourceCircles.Add(circle);
        }
    }
    
    private bool CircleOverlapsTerrain(Vector2Int center, float radius)
    {
        if (BuildingManager.Instance == null) return false;
        
        int samplePoints = Mathf.Min(Mathf.CeilToInt(radius * 2f), 16);
        
        for (int i = 0; i < samplePoints; i++)
        {
            float angle = (i / (float)samplePoints) * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(center.x + Mathf.Cos(angle) * radius);
            int y = Mathf.RoundToInt(center.y + Mathf.Sin(angle) * radius);
            
            Vector3Int cellPos = new Vector3Int(x, y, 0);
            
            if (BuildingManager.Instance.IsTerrainCell(cellPos))
            {
                return true;
            }
        }
        
        Vector3Int centerCell = new Vector3Int(center.x, center.y, 0);
        if (BuildingManager.Instance.IsTerrainCell(centerCell))
        {
            return true;
        }
        
        for (int i = 0; i < 4; i++)
        {
            float angle = (i / 4f) * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(center.x + Mathf.Cos(angle) * radius * 0.5f);
            int y = Mathf.RoundToInt(center.y + Mathf.Sin(angle) * radius * 0.5f);
            Vector3Int cellPos = new Vector3Int(x, y, 0);
            if (BuildingManager.Instance.IsTerrainCell(cellPos))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private ResourceType? SelectResourceTypeForCircle(Vector2Int center)
    {
        float distanceFromCenter = Vector2.Distance(center, _mapCenter);
        
        int sectorIndex = 0;
        for (int i = 0; i < _divisionRadii.Count - 1; i++)
        {
            if (distanceFromCenter >= _divisionRadii[i] && distanceFromCenter < _divisionRadii[i + 1])
            {
                sectorIndex = i;
                break;
            }
        }
        
        List<ResourceSpawnSettings> validSettings = new List<ResourceSpawnSettings>();
        foreach (var settings in resourceSettings)
        {
            if (settings.resourcePrefab == null || settings.resourceRuleTile == null) continue;
            if (Random.Range(0f, 100f) <= settings.spawnFrequencyPercent)
            {
                validSettings.Add(settings);
            }
        }
        
        if (validSettings.Count == 0) return null;
        
        ResourceSpawnSettings selected = validSettings[Random.Range(0, validSettings.Count)];
        return selected.resourceType;
    }
    
    private void SpawnResourcesInCircles()
    {
        foreach (var circle in _resourceCircles)
        {
            ResourceSpawnSettings settings = resourceSettings.Find(s => s.resourceType == circle.resourceType);
            if (settings == null || settings.resourcePrefab == null || settings.resourceRuleTile == null) continue;
            
            float distanceFromCenter = Vector2.Distance(circle.center, _mapCenter);
            int sectorIndex = 0;
            for (int i = 0; i < _divisionRadii.Count - 1; i++)
            {
                if (distanceFromCenter >= _divisionRadii[i] && distanceFromCenter < _divisionRadii[i + 1])
                {
                    sectorIndex = i;
                    break;
                }
            }
            
            if (sectorIndex >= _sectorPowerValues.Count) sectorIndex = _sectorPowerValues.Count - 1;
            float powerValue = _sectorPowerValues[sectorIndex];
            
            int richness = Mathf.RoundToInt(settings.baseRichness * powerValue);
            
            int radiusInt = Mathf.CeilToInt(circle.radius);
            int minX = circle.center.x - radiusInt;
            int maxX = circle.center.x + radiusInt;
            int minY = circle.center.y - radiusInt;
            int maxY = circle.center.y + radiusInt;
            
            List<Vector3Int> validPositions = new List<Vector3Int>();
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    float distanceFromCircleCenter = Vector2.Distance(new Vector2(x, y), circle.center);
                    if (distanceFromCircleCenter > circle.radius) continue;
                    
                    Vector3Int cellPos = new Vector3Int(x, y, 0);
                    
                    if (!IsValidSpawnPosition(cellPos)) continue;
                    
                    validPositions.Add(cellPos);
                }
            }
            
            if (validPositions.Count > maxResourcesPerCircle)
            {
                ShuffleList(validPositions);
                validPositions = validPositions.GetRange(0, maxResourcesPerCircle);
            }
            
            foreach (var cellPos in validPositions)
            {
                SpawnResourceAtPosition(cellPos, settings, richness);
            }
        }
    }
    
    private void RegisterBufferedResources()
    {
        if (ResourceManager.Instance == null) return;

        foreach (var node in _pendingResourceNodes)
        {
            if (node != null)
            {
                ResourceManager.Instance.AddResourceNode(node);
            }
        }
        
        _pendingResourceNodes.Clear(); 
    }
    
    private bool IsValidSpawnPosition(Vector3Int cellPos)
    {
        Vector2Int mapSize = mapGenerator.MapSize;
        int minX = -mapSize.x / 2;
        int maxX = mapSize.x / 2 - 1;
        int minY = -mapSize.y / 2;
        int maxY = mapSize.y / 2 - 1;
        
        if (cellPos.x < minX || cellPos.x > maxX || cellPos.y < minY || cellPos.y > maxY)
            return false;
        
        if (BuildingManager.Instance != null && BuildingManager.Instance.IsTerrainCell(cellPos))
            return false;
        
        if (BuildingManager.Instance != null && BuildingManager.Instance.IsBuildingTile(cellPos))
            return false;
        
        if (BuildingManager.Instance != null && BuildingManager.Instance.GroundTilemap != null)
        {
            if (!BuildingManager.Instance.GroundTilemap.HasTile(cellPos))
                return false;
        }
        
        return true;
    }
    
    private void SpawnResourceAtPosition(Vector3Int cellPos, ResourceSpawnSettings settings, int richness)
    {
        if (settings == null || settings.resourcePrefab == null)
        {
            return;
        }
        try
        {
            if (grid == null) return;
            
            Vector3 worldPos = grid.GetCellCenterWorld(cellPos);
            GameObject resourceNodeObj = Instantiate(settings.resourcePrefab, worldPos, Quaternion.identity, parentTransform);
            
            if (resourceNodeObj == null) return;
            
            ResourceNode resourceNode = resourceNodeObj.GetComponent<ResourceNode>();
            resourceNode.amountToMine = richness;
            resourceNode.cellPosition = cellPos;
            _pendingResourceNodes.Add(resourceNode);
            
            // Track resource position for rule tile placement
            _resourceTilePositions[cellPos] = settings.resourceType;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ProceduralResourceSpawner] Error spawning resource at {cellPos}: {e.Message}");
        }
    }
    
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    
    public void UpdateResourceTileVisibility()
    {
        if (resourceTilemap == null) return;
        
        // Ensure tilemap is active and visible
        if (resourceTilemap.gameObject != null && !resourceTilemap.gameObject.activeSelf)
        {
            resourceTilemap.gameObject.SetActive(true);
        }
        
        // Update visibility for all resource tiles based on fog of war
        foreach (var kvp in _resourceTilePositions)
        {
            Vector3Int cellPos = kvp.Key;
            UpdateResourceTileVisibilityAtCell(cellPos);
        }
    }
    
    public void UpdateResourceTileVisibilityAtCell(Vector3Int cellPos)
    {
        if (resourceTilemap == null || !resourceTilemap.HasTile(cellPos)) return;
        
        // Check visibility if fog is initialized, otherwise default to visible
        bool isVisible = true;
        if (FogOfWarManager.Instance != null && FogOfWarManager.Instance.IsInitialized)
        {
            isVisible = FogOfWarManager.Instance.CanSeeResources(cellPos);
        }
        
        // Set tile color with proper RGB and alpha based on visibility
        // Visible = white with alpha 1.0, Invisible = white with alpha 0.0
        Color tileColor = isVisible ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0f);
        resourceTilemap.SetColor(cellPos, tileColor);
        resourceTilemap.RefreshTile(cellPos);
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        if (mapGenerator == null) return;

        Vector3 centerWorld = grid != null ? grid.GetCellCenterWorld(Vector3Int.zero) : Vector3.zero;
        
        float cellSize = grid != null ? grid.cellSize.x : 1f;
        
        Gizmos.color = startingAreaMinRadiusColor;
        float minRadiusWorld = minSpawnRadius * cellSize;
        DrawCircleGizmo(centerWorld, minRadiusWorld);
        
        Gizmos.color = startingAreaMaxRadiusColor;
        float maxRadiusWorld = startingAreaRadius * cellSize;
        DrawCircleGizmo(centerWorld, maxRadiusWorld);
        
        Gizmos.color = divisionCircleColor;
        for (int i = 0; i < _divisionRadii.Count; i++)
        {
            float radius = _divisionRadii[i] * cellSize;
            DrawCircleGizmo(centerWorld, radius);
        }
        
        Gizmos.color = resourceCircleColor;
        foreach (var circle in _resourceCircles)
        {
            Vector3 circleWorldPos = grid != null ? grid.GetCellCenterWorld(new Vector3Int(circle.center.x, circle.center.y, 0)) : new Vector3(circle.center.x, circle.center.y, 0);
            float circleRadiusWorld = circle.radius * cellSize;
            DrawCircleGizmo(circleWorldPos, circleRadiusWorld);
        }
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
    
    private IEnumerator DeferredFogRefresh()
    {
        yield return null;
        yield return null;
        
        if (FogOfWarManager.Instance != null)
        {
            FogOfWarManager.Instance.RefreshFogOfWar();
            // RefreshFogOfWar now automatically calls UpdateResourceTileVisibility
        }
    }
    
    private void InitializeResourceRuleTiles()
    {
        if (resourceTilemap == null)
        {
            Debug.LogWarning("[MapObjectSpawner] Resource tilemap not assigned. Cannot initialize rule tiles.");
            return;
        }
        
        // Ensure tilemap is visible
        if (resourceTilemap.gameObject != null)
        {
            resourceTilemap.gameObject.SetActive(true);
        }
        
        // Place rule tiles at all resource positions
        foreach (var kvp in _resourceTilePositions)
        {
            Vector3Int cellPos = kvp.Key;
            ResourceType resourceType = kvp.Value;
            
            ResourceSpawnSettings settings = resourceSettings.Find(s => s.resourceType == resourceType);
            if (settings != null && settings.resourceRuleTile != null)
            {
                resourceTilemap.SetTile(cellPos, settings.resourceRuleTile);
                // Set initial color to white with full alpha (visible)
                resourceTilemap.SetColor(cellPos, Color.white);
            }
        }
        
        // Refresh all rule tiles to apply proper tiling rules
        RefreshRuleTiles(_resourceTilePositions.Keys);
        
        // Update visibility for all resource tiles based on fog of war
        UpdateResourceTileVisibility();
        
        Debug.Log($"[MapObjectSpawner] Initialized {_resourceTilePositions.Count} resource rule tiles.");
    }
    
    public void UpdateNearbyRuleTiles(Vector3Int minedPosition)
    {
        if (resourceTilemap == null) return;
        
        // Remove the mined tile
        _resourceTilePositions.Remove(minedPosition);
        resourceTilemap.SetTile(minedPosition, null);
        
        // Update nearby tiles (check all 8 neighbors)
        HashSet<Vector3Int> tilesToRefresh = new HashSet<Vector3Int>();
        
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue; // Skip the mined tile itself
                
                Vector3Int neighborPos = minedPosition + new Vector3Int(x, y, 0);
                
                // If this neighbor has a resource tile, refresh it
                if (_resourceTilePositions.ContainsKey(neighborPos))
                {
                    tilesToRefresh.Add(neighborPos);
                }
            }
        }
        
        RefreshRuleTiles(tilesToRefresh);
    }
    
    private void RefreshRuleTiles(IEnumerable<Vector3Int> positions)
    {
        if (resourceTilemap == null) return;
        
        // Refresh each tile position to update RuleTile sprites based on neighbors
        foreach (Vector3Int pos in positions)
        {
            if (_resourceTilePositions.ContainsKey(pos))
            {
                // Unity's RuleTile system automatically updates based on neighbors
                // We just need to trigger a refresh
                resourceTilemap.RefreshTile(pos);
                
                // Update visibility after refreshing tile
                UpdateResourceTileVisibilityAtCell(pos);
            }
        }
    }
}
