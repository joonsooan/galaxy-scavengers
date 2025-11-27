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

public class ProceduralResourceSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap resourceTilemap;
    [SerializeField] private Grid grid;
    [SerializeField] private Transform parentTransform;
    [SerializeField] private MapGenerator mapGenerator;
    
    [Header("Resource Settings")]
    [SerializeField] private List<ResourceSpawnSettings> resourceSettings = new List<ResourceSpawnSettings>();
    
    [Header("Concentric Circle Division")]
    [Tooltip("Number of concentric circles to divide the map into.")]
    [Range(0, 20)]
    [SerializeField] private int numberOfCircles = 10;
    
    [Tooltip("Padding from map borders (in tiles) to exclude from resource spawning.")]
    [Range(0, 50)]
    [SerializeField] private int borderPadding = 10;
    
    [Header("Resource Circle Spawning")]
    [Tooltip("Minimum radius for resource spawn circles (in tiles).")]
    [Range(0, 20)]
    [SerializeField] private float minCircleRadius = 5f;
    
    [Tooltip("Maximum radius for resource spawn circles (in tiles).")]
    [Range(0, 50)]
    [SerializeField] private float maxCircleRadius = 15f;
    
    [Tooltip("Percentage of potential spawn circles that will actually spawn (0-100).")]
    [Range(0f, 100f)]
    [SerializeField] private float circleSpawnPercentage = 30f;
    
    [Tooltip("Maximum number of circles that can spawn (prevents excessive spawning).")]
    [Range(10, 200)]
    [SerializeField] private int maxCircles = 50;
    
    [Tooltip("Maximum number of resources per circle (prevents memory issues with large circles).")]
    [Range(10, 1000)]
    [SerializeField] private int maxResourcesPerCircle = 200;
    
    [Header("Starting Area")]
    [Tooltip("Minimum radius from center where NO resources spawn (safe zone).")]
    [Range(0, 50)]
    [SerializeField] private int minSpawnRadius = 5;
    
    [Tooltip("Maximum radius of starting area where starting area circles can spawn.")]
    [Range(0, 50)]
    [SerializeField] private int startingAreaRadius = 15;
    
    [Tooltip("Number of circles to spawn in the starting area (between minSpawnRadius and startingAreaRadius).")]
    [Range(0, 20)]
    [SerializeField] private int startingAreaCircleCount = 3;
    
    [Tooltip("Minimum distance between starting area resource circles (as multiplier of combined radii, 0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float startingAreaMinDistance = 0.3f;
    
    [Header("Debug")]
    [Tooltip("Show gizmos in scene view to visualize circles.")]
    [SerializeField] private bool showGizmos = true;
    
    [Tooltip("Color for division circles (concentric).")]
    [SerializeField] private Color divisionCircleColor = new Color(1f, 1f, 0f, 0.3f);
    
    [Tooltip("Color for resource spawn circles.")]
    [SerializeField] private Color resourceCircleColor = new Color(0f, 1f, 0f, 0.5f);
    
    [Tooltip("Color for starting area min spawn radius (safe zone).")]
    [SerializeField] private Color startingAreaMinRadiusColor = new Color(1f, 0f, 0f, 0.5f);
    
    [Tooltip("Color for starting area max radius.")]
    [SerializeField] private Color startingAreaMaxRadiusColor = new Color(0f, 0f, 1f, 0.5f);
    
    private readonly List<GameObject> _spawnedResources = new List<GameObject>();
    private Vector2Int _mapCenter;
    private int _mapWidth;
    private int _mapHeight;
    private float _maxMapRadius;
    private float _radiusStep;
    private List<float> _divisionRadii = new List<float>();
    private List<float> _sectorPowerValues = new List<float>();
    private List<ResourceCircle> _resourceCircles = new List<ResourceCircle>();
    
    // Static flag to override resource visibility (regardless of fog)
    private static bool _resourcesAlwaysVisible = false;
    public static bool ResourcesAlwaysVisible => _resourcesAlwaysVisible;
    
    // Static flag to disable visibility updates during resource spawning
    private static bool _isSpawningResources = false;
    public static bool IsSpawningResources => _isSpawningResources;
    
    public Tilemap ResourceTilemap => resourceTilemap;
    
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
        
        if (resourceTilemap == null)
        {
            Debug.LogError("[ProceduralResourceSpawner] ResourceTilemap not assigned!");
            return;
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
        
        if (parentTransform == null)
        {
            parentTransform = transform;
        }
        
        // Get map dimensions
        Vector2Int mapSize = mapGenerator.MapSize;
        _mapWidth = mapSize.x;
        _mapHeight = mapSize.y;
        _mapCenter = Vector2Int.zero; // Map center is at (0, 0)
        
        // Calculate max radius (with padding)
        float halfWidth = (_mapWidth / 2f) - borderPadding;
        float halfHeight = (_mapHeight / 2f) - borderPadding;
        _maxMapRadius = Mathf.Min(halfWidth, halfHeight);
        
        // Clear existing resources
        _spawnedResources.Clear();
        _divisionRadii.Clear();
        _sectorPowerValues.Clear();
        _resourceCircles.Clear();
        
        // Disable visibility updates during spawning to prevent performance issues
        _isSpawningResources = true;
        
        // Suppress FogOfWarManager event invocations during spawning
        if (FogOfWarManager.Instance != null)
        {
            FogOfWarManager.SetSuppressVisibilityEvents(true);
        }
        
        try
        {
            // Step 1: Divide map into concentric circles
            DivideMapIntoConcentricCircles();
            
            // Step 2: Spawn resource circles (outside starting area)
            SpawnResourceCircles();
            
            // Step 3: Spawn starting area circles (between minSpawnRadius and startingAreaRadius)
            SpawnStartingAreaCircles();
            
            // Step 4: Spawn resources in circles
            SpawnResourcesInCircles();
            
            Debug.Log($"[ProceduralResourceSpawner] Spawned {_spawnedResources.Count} resource nodes. Created {_resourceCircles.Count} resource circles.");
        }
        finally
        {
            // Re-enable visibility updates after spawning is complete
            _isSpawningResources = false;
            
            // Re-enable FogOfWarManager event invocations
            if (FogOfWarManager.Instance != null)
            {
                FogOfWarManager.SetSuppressVisibilityEvents(false);
                
                // Defer fog refresh to next frame to avoid blocking
                StartCoroutine(DeferredFogRefresh());
            }
            
            // Batch update all visibility controllers after spawning
            UpdateAllResourceVisibility();
        }
    }
    
    private void DivideMapIntoConcentricCircles()
    {
        _divisionRadii.Clear();
        _sectorPowerValues.Clear();
        
        // Calculate radius step (equal difference between circles)
        _radiusStep = _maxMapRadius / numberOfCircles;
        
        // Create division radii and calculate power values
        for (int i = 0; i <= numberOfCircles; i++)
        {
            float radius = i * _radiusStep;
            _divisionRadii.Add(radius);
            
            // Power value increases with distance from center
            // Power = 1.0 at center, increases linearly to maxPower at edge
            float normalizedDistance = radius / _maxMapRadius;
            float powerValue = 1f + normalizedDistance; // Starts at 1.0, goes to 2.0 at edge
            _sectorPowerValues.Add(powerValue);
        }
        
        Debug.Log($"[ProceduralResourceSpawner] Divided map into {numberOfCircles} concentric circles. Max radius: {_maxMapRadius}");
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
            
            // Random angle and distance within starting area ring (between minSpawnRadius and startingAreaRadius)
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float minDist = minSpawnRadius;
            float maxDist = startingAreaRadius;
            float distance = Random.Range(minDist, maxDist);
            
            int centerX = Mathf.RoundToInt(Mathf.Cos(angle) * distance);
            int centerY = Mathf.RoundToInt(Mathf.Sin(angle) * distance);
            Vector2Int center = new Vector2Int(centerX, centerY);
            
            // Ensure circle center is at least minSpawnRadius from center
            float distanceFromCenter = Vector2.Distance(center, _mapCenter);
            if (distanceFromCenter < minSpawnRadius)
            {
                // Adjust to be at least minSpawnRadius away
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
            
            // Ensure circle doesn't extend beyond startingAreaRadius
            float maxAllowedRadius = startingAreaRadius - Vector2.Distance(center, _mapCenter);
            if (maxAllowedRadius < minCircleRadius) continue;
            
            // Random radius, but ensure it stays within starting area
            float radius = Random.Range(minCircleRadius, Mathf.Min(maxCircleRadius, maxAllowedRadius));
            
            // Check if circle overlaps with terrain
            if (CircleOverlapsTerrain(center, radius))
            {
                // Try to adjust: reduce radius
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
            
            // Check if circle is too close to existing circles
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
            
            // Determine which resource type for this circle (based on frequency)
            ResourceType? selectedType = SelectResourceTypeForCircle(center);
            if (!selectedType.HasValue) continue;
            
            // Add circle
            ResourceCircle circle = new ResourceCircle
            {
                center = center,
                radius = radius,
                resourceType = selectedType.Value
            };
            
            _resourceCircles.Add(circle);
            startingAreaCirclesSpawned++;
        }
        
        Debug.Log($"[ProceduralResourceSpawner] Spawned {startingAreaCirclesSpawned} starting area circles (target: {startingAreaCircleCount}).");
    }
    
    private void SpawnResourceCircles()
    {
        _resourceCircles.Clear();
        
        // Calculate how many circles to spawn based on percentage
        // Use map area to calculate potential circles more accurately
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
                        for (int offsetY = -5; offsetY <= 5 && !foundValid; offsetY++)
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
            
            // Determine which resource type for this circle (based on frequency)
            ResourceType? selectedType = SelectResourceTypeForCircle(center);
            if (!selectedType.HasValue) continue;
            
            // Add circle
            ResourceCircle circle = new ResourceCircle
            {
                center = center,
                radius = radius,
                resourceType = selectedType.Value
            };
            
            _resourceCircles.Add(circle);
        }
        
        Debug.Log($"[ProceduralResourceSpawner] Spawned {_resourceCircles.Count} resource circles out of {circlesToSpawn} attempted (max possible: {maxPossibleCircles}).");
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
        // Calculate distance from center to determine which sector this circle is in
        float distanceFromCenter = Vector2.Distance(center, _mapCenter);
        
        // Find which sector this circle belongs to
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
        
        // Select resource type based on spawn frequency
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
        
        // Randomly select from valid settings
        ResourceSpawnSettings selected = validSettings[Random.Range(0, validSettings.Count)];
        return selected.resourceType;
    }
    
    private void SpawnResourcesInCircles()
    {
        foreach (var circle in _resourceCircles)
        {
            ResourceSpawnSettings settings = resourceSettings.Find(s => s.resourceType == circle.resourceType);
            if (settings == null || settings.resourcePrefab == null || settings.resourceTile == null) continue;
            
            // Calculate distance from center to get power value
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
            
            // Calculate richness with power multiplier
            int richness = Mathf.RoundToInt(settings.baseRichness * powerValue);
            
            // Fill the entire circle with resources (with limit to prevent memory issues)
            int radiusInt = Mathf.CeilToInt(circle.radius);
            int minX = circle.center.x - radiusInt;
            int maxX = circle.center.x + radiusInt;
            int minY = circle.center.y - radiusInt;
            int maxY = circle.center.y + radiusInt;
            
            int spawned = 0;
            int checkedPositions = 0;
            
            // Collect valid positions first to avoid excessive checks
            List<Vector3Int> validPositions = new List<Vector3Int>();
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    checkedPositions++;
                    
                    // Check if position is within circle
                    float distanceFromCircleCenter = Vector2.Distance(new Vector2(x, y), circle.center);
                    if (distanceFromCircleCenter > circle.radius) continue;
                    
                    Vector3Int cellPos = new Vector3Int(x, y, 0);
                    
                    // Check if valid position (not terrain, not already occupied, etc.)
                    if (!IsValidSpawnPosition(cellPos)) continue;
                    
                    validPositions.Add(cellPos);
                }
            }
            
            // Limit the number of resources spawned per circle
            if (validPositions.Count > maxResourcesPerCircle)
            {
                // Shuffle and take only the first maxResourcesPerCircle
                ShuffleList(validPositions);
                validPositions = validPositions.GetRange(0, maxResourcesPerCircle);
            }
            
            // Spawn resources at valid positions
            foreach (var cellPos in validPositions)
            {
                SpawnResourceAtPosition(cellPos, settings, richness);
                spawned++;
            }
            
            // Debug.Log($"[ProceduralResourceSpawner] Filled circle at ({circle.center.x}, {circle.center.y}) with radius {circle.radius} - spawned {spawned}/{validPositions.Count} {circle.resourceType} resources (checked {checkedPositions} positions).");
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
        
        if (resourceTilemap.HasTile(cellPos))
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
        if (resourceTilemap == null || settings == null || settings.resourceTile == null || settings.resourcePrefab == null)
        {
            return;
        }
        
        try
        {
            resourceTilemap.SetTile(cellPos, settings.resourceTile);
            
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
            
            ResourceNode nodeComponent = resourceNodeObj.GetComponent<ResourceNode>();
            
            if (nodeComponent != null)
            {
                nodeComponent.cellPosition = cellPos;
                nodeComponent.amountToMine = richness;
                
                ResourceSpawner oldSpawner = GetComponent<ResourceSpawner>();
                if (oldSpawner != null)
                {
                    nodeComponent.spawner = oldSpawner;
                }
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
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
    
    public void NotifyResourceDestroyed(ResourceNode node)
    {
        if (node != null && node.gameObject != null)
        {
            _spawnedResources.Remove(node.gameObject);
        }
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ToggleResourceVisibility();
        }
    }
    
    public static void ToggleResourceVisibility()
    {
        _resourcesAlwaysVisible = !_resourcesAlwaysVisible;
        UpdateAllResourceVisibilityStatic();
        // Debug.Log($"[ProceduralResourceSpawner] Resources always visible: {_resourcesAlwaysVisible}");
    }
    
    private static void UpdateAllResourceVisibilityStatic()
    {
        VisibilityController[] allControllers = Object.FindObjectsByType<VisibilityController>(FindObjectsSortMode.None);
        foreach (var controller in allControllers)
        {
            if (controller != null)
            {
                // Subscribe to events if needed (for resources that were spawned)
                controller.SubscribeIfNeeded();
                controller.ForceUpdateVisibility();
            }
        }
    }
    
    private void UpdateAllResourceVisibility()
    {
        try
        {
            VisibilityController[] allControllers = FindObjectsByType<VisibilityController>(FindObjectsSortMode.None);
            foreach (var controller in allControllers)
            {
                if (controller != null && controller.gameObject != null)
                {
                    try
                    {
                        // Subscribe to events if needed (for resources that were spawned)
                        controller.SubscribeIfNeeded();
                        controller.ForceUpdateVisibility();
                    }
                    catch (System.Exception)
                    {
                        // Skip invalid controllers
                        continue;
                    }
                }
            }
        }
        catch (System.Exception)
        {
            // Handle gracefully if FindObjectsByType fails
            // Silently continue - visibility will update naturally as resources are discovered
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        if (mapGenerator == null) return;
        
        Vector2Int mapSize = mapGenerator.MapSize;
        Vector3 centerWorld = grid != null ? grid.GetCellCenterWorld(Vector3Int.zero) : Vector3.zero;
        
        // Get cell size for converting tile units to world units
        float cellSize = grid != null ? grid.cellSize.x : 1f;
        
        // Draw starting area radius circles
        // Draw min spawn radius (safe zone - no resources)
        Gizmos.color = startingAreaMinRadiusColor;
        float minRadiusWorld = minSpawnRadius * cellSize;
        DrawCircleGizmo(centerWorld, minRadiusWorld);
        
        // Draw starting area max radius
        Gizmos.color = startingAreaMaxRadiusColor;
        float maxRadiusWorld = startingAreaRadius * cellSize;
        DrawCircleGizmo(centerWorld, maxRadiusWorld);
        
        // Draw division circles (concentric) - convert from tile units to world units
        Gizmos.color = divisionCircleColor;
        for (int i = 0; i < _divisionRadii.Count; i++)
        {
            float radius = _divisionRadii[i] * cellSize; // Convert tiles to world units
            DrawCircleGizmo(centerWorld, radius);
        }
        
        // Draw resource spawn circles - convert from tile units to world units
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
        // Wait a frame to let all resources finish initializing
        yield return null;
        
        // Wait another frame to ensure all Awake/OnEnable calls are complete
        yield return null;
        
        // Now refresh fog of war
        if (FogOfWarManager.Instance != null)
        {
            FogOfWarManager.Instance.RefreshFogOfWar();
        }
    }
}
