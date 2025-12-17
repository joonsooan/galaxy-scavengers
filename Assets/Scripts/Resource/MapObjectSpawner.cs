using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class ResourceSpawnSettings
{
    [Header("Resource Type")]
    public ResourceType resourceType;
    public TileBase resourceTile;
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

[System.Serializable]
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
    
    private readonly List<GameObject> _spawnedResources = new ();
    private Vector2Int _mapCenter;
    private int _mapWidth;
    private int _mapHeight;
    private float _maxMapRadius;
    private float _radiusStep;
    private readonly List<float> _divisionRadii = new ();
    private readonly List<float> _sectorPowerValues = new ();
    private readonly List<ResourceCircle> _resourceCircles = new ();
    
    private static bool isSpawningResources;
    public static bool IsSpawningResources => isSpawningResources;
    
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
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<MapGenerator>();
            if (mapGenerator == null)
            {
                Debug.LogError("[ProceduralResourceSpawner] MapGenerator not found!");
                return;
            }
        }
        
        if (grid == null)
        {
            grid = FindFirstObjectByType<Grid>();
            if (grid == null)
            {
                Debug.LogError("[ProceduralResourceSpawner] Grid not found!");
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
        
        _spawnedResources.Clear();
        _divisionRadii.Clear();
        _sectorPowerValues.Clear();
        _resourceCircles.Clear();
        
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
            
            Debug.Log($"[ProceduralResourceSpawner] Spawned {_spawnedResources.Count} resource nodes. Created {_resourceCircles.Count} resource circles.");
        }
        finally
        {
            isSpawningResources = false;
            
            if (FogOfWarManager.Instance != null)
            {
                FogOfWarManager.SetSuppressVisibilityEvents(false);
                
                StartCoroutine(DeferredFogRefresh());
            }
            
            UpdateAllResourceVisibility();
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
            float powerValue = 1f + normalizedDistance; // Starts at 1.0, goes to 2.0 at edge
            _sectorPowerValues.Add(powerValue);
        }
    }
    
    private void SpawnStartingAreaCircles()
    {
        if (startingAreaCircleCount <= 0) return;
        if (startingAreaRadius <= minSpawnRadius) return;
        
        int startingAreaCirclesSpawned = 0;
        int attempts = 0;
        int maxAttempts = startingAreaCircleCount * 50; // Increased attempts for better success rate
        
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
        
        // Cap the maximum number of circles to prevent excessive spawning
        circlesToSpawn = Mathf.Min(circlesToSpawn, maxCircles);
        circlesToSpawn = Mathf.Max(circlesToSpawn, 10); // Ensure minimum
        
        int attempts = 0;
        int maxAttempts = circlesToSpawn * 30; // Reasonable limit on attempts
        
        while (_resourceCircles.Count < circlesToSpawn && attempts < maxAttempts)
        {
            attempts++;
            
            // Random position within map bounds (with padding), but outside starting area
            int centerX = Random.Range(-(_mapWidth / 2) + borderPadding, (_mapWidth / 2) - borderPadding);
            int centerY = Random.Range(-(_mapHeight / 2) + borderPadding, (_mapHeight / 2) - borderPadding);
            Vector2Int center = new Vector2Int(centerX, centerY);
            
            // Ensure circle is outside the starting area (startingAreaRadius + maxCircleRadius to ensure full circle is outside)
            float distanceFromCenter = Vector2.Distance(center, _mapCenter);
            float minRequiredDistance = startingAreaRadius + maxCircleRadius;
            if (distanceFromCenter < minRequiredDistance)
            {
                // Adjust position to be outside starting area
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
                
                // Re-check bounds after adjustment
                if (center.x < -(_mapWidth / 2) + borderPadding || center.x >= (_mapWidth / 2) - borderPadding ||
                    center.y < -(_mapHeight / 2) + borderPadding || center.y >= (_mapHeight / 2) - borderPadding)
                {
                    continue; // Skip if out of bounds after adjustment
                }
            }
            
            // Random radius
            float radius = Random.Range(minCircleRadius, maxCircleRadius);
            
            // Check if circle overlaps with terrain
            if (CircleOverlapsTerrain(center, radius))
            {
                // Try to adjust: reduce radius or move center
                bool foundValid = false;
                
                // Try reducing radius first
                for (float testRadius = radius * 0.9f; testRadius >= minCircleRadius; testRadius -= 0.5f)
                {
                    if (!CircleOverlapsTerrain(center, testRadius))
                    {
                        radius = testRadius;
                        foundValid = true;
                        break;
                    }
                }
                
                // If reducing radius didn't work, try moving center in a wider area
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
                
                if (!foundValid) continue; // Skip this circle if we can't make it valid
            }
            
            // Check if circle is too close to existing circles (less strict)
            bool tooClose = false;
            foreach (var existingCircle in _resourceCircles)
            {
                float distance = Vector2.Distance(center, existingCircle.center);
                float minDistance = (radius + existingCircle.radius) * 0.5f; // Reduced from 0.8f to 0.5f
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
        
        // Optimize: Sample fewer points for larger circles, but ensure minimum coverage
        int samplePoints = Mathf.Min(Mathf.CeilToInt(radius * 2f), 16); // Cap at 16 samples max
        
        // Sample points around the circle perimeter
        for (int i = 0; i < samplePoints; i++)
        {
            float angle = (i / (float)samplePoints) * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(center.x + Mathf.Cos(angle) * radius);
            int y = Mathf.RoundToInt(center.y + Mathf.Sin(angle) * radius);
            
            Vector3Int cellPos = new Vector3Int(x, y, 0);
            
            // Check if this position is terrain
            if (BuildingManager.Instance.IsTerrainCell(cellPos))
            {
                return true;
            }
        }
        
        // Also check center and a few inner points
        Vector3Int centerCell = new Vector3Int(center.x, center.y, 0);
        if (BuildingManager.Instance.IsTerrainCell(centerCell))
        {
            return true;
        }
        
        // Check a few points at half radius
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
        
        if (sectorIndex >= _sectorPowerValues.Count) sectorIndex = _sectorPowerValues.Count - 1;
        
        List<ResourceSpawnSettings> validSettings = new List<ResourceSpawnSettings>();
        foreach (var settings in resourceSettings)
        {
            if (settings.resourcePrefab == null || settings.resourceTile == null) continue;
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
            if (settings == null || settings.resourcePrefab == null || settings.resourceTile == null) continue;
            
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
            
            int spawned = 0;
            int checkedPositions = 0;
            
            List<Vector3Int> validPositions = new List<Vector3Int>();
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    checkedPositions++;
                    
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
                spawned++;
            }
        }
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
            if (grid == null)
            {
                return;
            }
            
            Vector3 worldPos = grid.GetCellCenterWorld(cellPos);
            GameObject resourceNodeObj = Instantiate(settings.resourcePrefab, worldPos, Quaternion.identity, parentTransform);
            
            if (resourceNodeObj == null)
            {
                return;
            }
            
            _spawnedResources.Add(resourceNodeObj);
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
    
    private void UpdateAllResourceVisibility()
    {
        try
        {
            VisibilityController[] allControllers =
                FindObjectsByType<VisibilityController>(FindObjectsSortMode.None);
            foreach (var controller in allControllers)
            {
                if (controller != null && controller.gameObject != null)
                {
                    try
                    {
                        controller.SubscribeIfNeeded();
                        controller.ForceUpdateVisibility();
                    }
                    catch (System.Exception)
                    {
                        //
                    }
                }
            }
        }
        catch (System.Exception)
        {
            //
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        if (mapGenerator == null) return;
        
        Vector2Int mapSize = mapGenerator.MapSize;
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
            float radius = _divisionRadii[i] * cellSize; // Convert tiles to world units
            DrawCircleGizmo(centerWorld, radius);
        }
        
        Gizmos.color = resourceCircleColor;
        foreach (var circle in _resourceCircles)
        {
            Vector3 circleWorldPos = grid != null ? grid.GetCellCenterWorld(new Vector3Int(circle.center.x, circle.center.y, 0)) : new Vector3(circle.center.x, circle.center.y, 0);
            float circleRadiusWorld = circle.radius * cellSize; // Convert tiles to world units
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
        }
    }
}
