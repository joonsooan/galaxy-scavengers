using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;
using Systems.Jobs;

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
    
    private readonly List<ResourceNode> _pendingResourceNodes = new ();
    private readonly Dictionary<Vector3Int, ResourceType> _resourceTilePositions = new ();
    private static MapObjectSpawner _instance;
    
    private ResourceCircleGenerator _circleGenerator;
    private ResourceTileManager _tileManager;
    private List<ResourceCircle> _cachedResourceCircles = new ();

    public static event Action OnAllObjectsSpawned;
    private static bool isSpawningResources;
    public static bool IsSpawningResources => isSpawningResources;
    
    public Tilemap ResourceTilemap => resourceTilemap;
    
    public List<float> GetDivisionRadii()
    {
        if (_circleGenerator == null)
        {
            InitializeCircleGenerator();
        }
        return _circleGenerator?.GetDivisionRadii() ?? new List<float>();
    }
    
    private void InitializeCircleGenerator()
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
        _circleGenerator = new ResourceCircleGenerator(
            numberOfCircles, borderPadding, minCircleRadius, maxCircleRadius,
            circleSpawnPercentage, maxCircles, minSpawnRadius, startingAreaRadius,
            startingAreaCircleCount, startingAreaMinDistance, resourceSettings);
        _circleGenerator.Initialize(mapSize.x, mapSize.y, Vector2Int.zero);
    }
    
    public IEnumerator SpawnResourcesAsync(IInitializationProgress progress = null)
    {
        Vector2Int mapSize = mapGenerator.MapSize;
        
        if (progress != null)
        {
            progress.UpdateProgress(0.0f, "희귀 광물 반응 탐지 중...");
            yield return new WaitForSeconds(0.5f);
        }
        
        if (_circleGenerator == null)
        {
            InitializeCircleGenerator();
        }
        else
        {
            _circleGenerator.Initialize(mapSize.x, mapSize.y, Vector2Int.zero);
        }
        
        _tileManager = new ResourceTileManager(resourceTilemap, _resourceTilePositions, resourceSettings);
        
        _pendingResourceNodes.Clear();
        
        isSpawningResources = true;
        
        if (FogOfWarManager.Instance != null)
        {
            FogOfWarManager.SetSuppressVisibilityEvents(true);
        }
        
        try
        {
            List<ResourceCircle> resourceCircles = _circleGenerator.GenerateResourceCircles();
            List<ResourceCircle> startingAreaCircles = _circleGenerator.GenerateStartingAreaCircles();
            
            resourceCircles.AddRange(startingAreaCircles);
            
            _cachedResourceCircles = new List<ResourceCircle>(resourceCircles);
            
            yield return StartCoroutine(SpawnResourcesInCirclesAsync(resourceCircles, progress));
            
            RegisterBufferedResources();
            
            _tileManager.InitializeRuleTiles();
            
            OnAllObjectsSpawned.Invoke();
        }
        finally
        {
            isSpawningResources = false;
            
            if (FogOfWarManager.Instance != null)
            {
                FogOfWarManager.SetSuppressVisibilityEvents(false);
            }
        }
    }
    
    public void SpawnResources()
    {
        StartCoroutine(SpawnResourcesSyncInternal());
    }
    
    private IEnumerator SpawnResourcesSyncInternal()
    {
        yield return StartCoroutine(SpawnResourcesAsync(null));
    }
    
    private IEnumerator SpawnResourcesInCirclesAsync(List<ResourceCircle> resourceCircles, IInitializationProgress progress = null)
    {
        List<float> divisionRadii = _circleGenerator.GetDivisionRadii();
        List<float> sectorPowerValues = _circleGenerator.GetSectorPowerValues();
        Vector2Int mapCenter = Vector2Int.zero;
        
        int totalCircles = resourceCircles.Count;
        int processedCircles = 0;
        const int circlesPerFrame = 5; // Process 5 circles per frame to avoid blocking
        
        foreach (var circle in resourceCircles)
        {
            ResourceSpawnSettings settings = resourceSettings.Find(s => s.resourceType == circle.resourceType);
            if (settings == null || settings.resourcePrefab == null || settings.resourceRuleTile == null)
            {
                processedCircles++;
                continue;
            }
            
            float distanceFromCenter = Vector2.Distance(circle.center, mapCenter);
            int sectorIndex = 0;
            for (int i = 0; i < divisionRadii.Count - 1; i++)
            {
                if (distanceFromCenter >= divisionRadii[i] && distanceFromCenter < divisionRadii[i + 1])
                {
                    sectorIndex = i;
                    break;
                }
            }
            
            if (sectorIndex >= sectorPowerValues.Count) sectorIndex = sectorPowerValues.Count - 1;
            float powerValue = sectorPowerValues[sectorIndex];
            
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
            
            processedCircles++;
            
            // Yield every few circles to avoid blocking
            if (processedCircles % circlesPerFrame == 0)
            {
                yield return null;
            }
        }
    }
    
    private void SpawnResourcesInCircles(List<ResourceCircle> resourceCircles)
    {
        List<float> divisionRadii = _circleGenerator.GetDivisionRadii();
        List<float> sectorPowerValues = _circleGenerator.GetSectorPowerValues();
        Vector2Int mapCenter = Vector2Int.zero;
        
        foreach (var circle in resourceCircles)
        {
            ResourceSpawnSettings settings = resourceSettings.Find(s => s.resourceType == circle.resourceType);
            if (settings == null || settings.resourcePrefab == null || settings.resourceRuleTile == null) continue;
            
            float distanceFromCenter = Vector2.Distance(circle.center, mapCenter);
            int sectorIndex = 0;
            for (int i = 0; i < divisionRadii.Count - 1; i++)
            {
                if (distanceFromCenter >= divisionRadii[i] && distanceFromCenter < divisionRadii[i + 1])
                {
                    sectorIndex = i;
                    break;
                }
            }
            
            if (sectorIndex >= sectorPowerValues.Count) sectorIndex = sectorPowerValues.Count - 1;
            float powerValue = sectorPowerValues[sectorIndex];
            
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
        _tileManager?.UpdateResourceTileVisibility();
    }
    
    public void UpdateResourceTileVisibilityAtCell(Vector3Int cellPos)
    {
        _tileManager?.UpdateResourceTileVisibilityAtCell(cellPos);
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
        List<float> divisionRadii = GetDivisionRadii();
        for (int i = 0; i < divisionRadii.Count; i++)
        {
            float radius = divisionRadii[i] * cellSize;
            DrawCircleGizmo(centerWorld, radius);
        }
        
        Gizmos.color = resourceCircleColor;
        if (_cachedResourceCircles != null && _cachedResourceCircles.Count > 0)
        {
            foreach (var circle in _cachedResourceCircles)
            {
                Vector3 circleWorldPos = grid != null ? grid.GetCellCenterWorld(new Vector3Int(circle.center.x, circle.center.y, 0)) : new Vector3(circle.center.x, circle.center.y, 0);
                float circleRadiusWorld = circle.radius * cellSize;
                DrawCircleGizmo(circleWorldPos, circleRadiusWorld);
            }
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
    
    public void UpdateNearbyRuleTiles(Vector3Int minedPosition)
    {
        _tileManager?.UpdateNearbyRuleTiles(minedPosition);
    }
}
