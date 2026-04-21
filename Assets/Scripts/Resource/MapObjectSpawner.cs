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

    public Transform ParentTransform => parentTransform;
    
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
    [SerializeField] public List<ResourceSpawnSettings> resourceSettings = new ();
    
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

    [Header("Edge Irregularity")]
    [Range(0f, 1f)]
    [SerializeField] private float edgeBandRatio = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float edgeRemovalChance = 0.35f;
    
    [Header("Ring richness (per tile by map-center distance)")]
    [Tooltip("Per-block amount in the innermost concentric sector (between first two division radii).")]
    [SerializeField] private int ringRichnessInnermost = 50;
    [Tooltip("Added per outer sector: ring 0 = innermost, ring 1 = innermost + step (e.g. 50, 100, 150…).")]
    [SerializeField] private int ringRichnessStep = 50;

    [Header("Starter ring (one cluster per listed type)")]
    [Range(0, 50)]
    [SerializeField] private int starterRingInnerRadius = 5;
    [Tooltip("Global procedural circles stay outside this radius from map center (plus max circle radius).")]
    [Range(0, 50)]
    [SerializeField] private int starterRingOuterRadius = 15;
    [Tooltip("Order matches Starter Cluster Radius By Type Index. Duplicate types spawn multiple clusters.")]
    [SerializeField] private ResourceType[] starterResourceTypes =
    {
        ResourceType.Ferrite,
        ResourceType.Aether,
        ResourceType.Biomass,
        ResourceType.CryoCrystal
    };
    [Tooltip("Cluster radius per starterResourceTypes index. Use 0 to use Starter Cluster Default Radius.")]
    [SerializeField] private float[] starterClusterRadiusByTypeIndex = new float[4];
    [SerializeField] private float starterClusterDefaultRadius = 1f;
    [SerializeField] private int starterClusterPlacementMinDistance = 3;
    [SerializeField] private int starterClusterPlacementMaxDistance = 8;
    [SerializeField] private float starterClusterMinGap = 2.5f;
    [SerializeField] private int starterClusterPlacementAttempts = 50;
    
    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color divisionCircleColor = new(1f, 1f, 0f, 0.3f);
    [SerializeField] private Color resourceCircleColor = new(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color starterRingInnerGizmoColor = new(1f, 0f, 0f, 0.5f);
    [SerializeField] private Color starterRingOuterGizmoColor = new(0f, 0f, 1f, 0.5f);
    
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
            circleSpawnPercentage, maxCircles, starterRingOuterRadius, resourceSettings);
        _circleGenerator.Initialize(mapSize.x, mapSize.y, Vector2Int.zero);
    }
    
    public IEnumerator SpawnResourcesAsync(IInitializationProgress progress = null)
    {
        Vector2Int mapSize = mapGenerator.MapSize;
        
        if (progress != null)
        {
            progress.UpdateProgress(0.0f, "희귀 광물 반응 탐지 중...");
            yield return CoroutineCache.GetWaitForSeconds(0.5f);
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
            AppendStarterRingResourceCircles(resourceCircles);
            
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
                    if (ShouldSkipEdgeCell(cellPos, circle, distanceFromCircleCenter)) continue;
                    
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
                float cellDistFromMapCenter = Vector2.Distance(new Vector2(cellPos.x, cellPos.y), mapCenter);
                int sectorIndex = GetSectorIndexFromMapCenterDistance(cellDistFromMapCenter, divisionRadii);
                int richness = GetRichnessForSectorIndex(sectorIndex);
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
    
    public void SpawnResourcesInCircles(List<ResourceCircle> resourceCircles)
    {
        if (_tileManager == null)
        {
            _tileManager = new ResourceTileManager(resourceTilemap, _resourceTilePositions, resourceSettings);
        }
        
        List<float> divisionRadii = _circleGenerator != null ? _circleGenerator.GetDivisionRadii() : new List<float>();
        Vector2Int mapCenter = Vector2Int.zero;
        
        foreach (var circle in resourceCircles)
        {
            ResourceSpawnSettings settings = resourceSettings.Find(s => s.resourceType == circle.resourceType);
            if (settings == null || settings.resourcePrefab == null || settings.resourceRuleTile == null) continue;
            
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
                    if (ShouldSkipEdgeCell(cellPos, circle, distanceFromCircleCenter)) continue;
                    
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
                float cellDistFromMapCenter = Vector2.Distance(new Vector2(cellPos.x, cellPos.y), mapCenter);
                int sectorIndex = GetSectorIndexFromMapCenterDistance(cellDistFromMapCenter, divisionRadii);
                int richness = GetRichnessForSectorIndex(sectorIndex);
                SpawnResourceAtPosition(cellPos, settings, richness);
            }
        }
        
        RegisterBufferedResources();
        if (_tileManager != null)
        {
            _tileManager.InitializeRuleTiles();
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
    
    private static int GetSectorIndexFromMapCenterDistance(float distanceFromMapCenter, List<float> divisionRadii)
    {
        if (divisionRadii == null || divisionRadii.Count < 2)
        {
            return 0;
        }

        for (int i = 0; i < divisionRadii.Count - 1; i++)
        {
            if (distanceFromMapCenter >= divisionRadii[i] && distanceFromMapCenter < divisionRadii[i + 1])
            {
                return i;
            }
        }

        if (distanceFromMapCenter >= divisionRadii[divisionRadii.Count - 1])
        {
            return divisionRadii.Count - 2;
        }

        return 0;
    }

    private int GetRichnessForSectorIndex(int sectorIndex)
    {
        int step = Mathf.Max(0, ringRichnessStep);
        return Mathf.Max(1, ringRichnessInnermost + sectorIndex * step);
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
        if (_resourceTilePositions.ContainsKey(cellPos))
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
            resourceNode.ApplyProceduralRichness(richness);
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

    private bool ShouldSkipEdgeCell(Vector3Int cellPos, ResourceCircle circle, float distanceFromCircleCenter)
    {
        if (edgeRemovalChance <= 0f || edgeBandRatio <= 0f || circle.radius <= 0f)
        {
            return false;
        }

        float normalizedDistance = distanceFromCircleCenter / circle.radius;
        float edgeStart = Mathf.Clamp01(1f - edgeBandRatio);
        if (normalizedDistance < edgeStart)
        {
            return false;
        }

        float edgeProgress = Mathf.InverseLerp(edgeStart, 1f, normalizedDistance);
        float localChance = edgeRemovalChance * edgeProgress;
        float noise = GetDeterministic01(cellPos.x, cellPos.y, circle.center.x, circle.center.y, (int)circle.resourceType);
        return noise < localChance;
    }

    private static float GetDeterministic01(int x, int y, int cx, int cy, int resourceType)
    {
        uint hash = 2166136261u;
        hash = (hash ^ (uint)x) * 16777619u;
        hash = (hash ^ (uint)y) * 16777619u;
        hash = (hash ^ (uint)cx) * 16777619u;
        hash = (hash ^ (uint)cy) * 16777619u;
        hash = (hash ^ (uint)resourceType) * 16777619u;
        return (hash & 0x00FFFFFF) / 16777215f;
    }
    
    private static readonly ResourceType[] DefaultStarterResourceTypes =
    {
        ResourceType.Ferrite,
        ResourceType.Aether,
        ResourceType.Biomass,
        ResourceType.CryoCrystal
    };

    private float GetStarterClusterRadiusForTypeIndex(int typeIndex)
    {
        if (starterClusterRadiusByTypeIndex != null &&
            typeIndex >= 0 &&
            typeIndex < starterClusterRadiusByTypeIndex.Length)
        {
            float r = starterClusterRadiusByTypeIndex[typeIndex];
            if (r > 0f)
            {
                return r;
            }
        }

        return Mathf.Max(0.01f, starterClusterDefaultRadius);
    }

    private bool StarterResourceDiskHasInvalidCell(Vector2Int center, float radius)
    {
        if (mapGenerator == null || BuildingManager.Instance == null)
        {
            return false;
        }

        float rSq = radius * radius;
        int ri = Mathf.CeilToInt(radius);
        for (int dx = -ri; dx <= ri; dx++)
        {
            for (int dy = -ri; dy <= ri; dy++)
            {
                if (dx * dx + dy * dy > rSq + 0.01f)
                {
                    continue;
                }

                Vector3Int c = new Vector3Int(center.x + dx, center.y + dy, 0);
                if (!IsValidSpawnPosition(c))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void AppendStarterRingResourceCircles(List<ResourceCircle> resourceCircles)
    {
        if (resourceCircles == null || resourceSettings == null || resourceSettings.Count == 0)
        {
            return;
        }

        ResourceType[] types = starterResourceTypes;
        if (types == null || types.Length == 0)
        {
            types = DefaultStarterResourceTypes;
        }

        Vector2Int mapCenter = Vector2Int.zero;
        int minDist = starterClusterPlacementMinDistance;
        int maxDist = Mathf.Max(minDist, starterClusterPlacementMaxDistance);
        float minGapBetweenEdges = starterClusterMinGap;
        int maxAttemptsPerType = Mathf.Max(starterClusterPlacementAttempts, 80);

        for (int ti = 0; ti < types.Length; ti++)
        {
            ResourceType type = types[ti];
            ResourceSpawnSettings settings = resourceSettings.Find(s => s != null && s.resourceType == type);
            if (settings == null || settings.resourcePrefab == null || settings.resourceRuleTile == null)
            {
                continue;
            }

            float clusterRadius = GetStarterClusterRadiusForTypeIndex(ti);

            Vector2Int center = Vector2Int.zero;
            int attempts = 0;
            bool placed = false;
            do
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(minDist, maxDist + 1f);
                int x = Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int y = Mathf.RoundToInt(Mathf.Sin(angle) * radius);
                center = new Vector2Int(x, y);

                float distFromMapCenter = Vector2.Distance(center, mapCenter);
                if (distFromMapCenter > 0.01f && distFromMapCenter < minDist)
                {
                    center = new Vector2Int(
                        Mathf.RoundToInt(center.x * minDist / distFromMapCenter),
                        Mathf.RoundToInt(center.y * minDist / distFromMapCenter));
                }

                bool tooClose = false;
                foreach (ResourceCircle existing in resourceCircles)
                {
                    float separationNeeded = clusterRadius + existing.radius + minGapBetweenEdges;
                    if (Vector2.Distance(center, existing.center) < separationNeeded)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose && !StarterResourceDiskHasInvalidCell(center, clusterRadius))
                {
                    placed = true;
                    break;
                }

                attempts++;
            }
            while (attempts < maxAttemptsPerType);

            if (!placed)
            {
                continue;
            }

            resourceCircles.Add(new ResourceCircle
            {
                center = center,
                radius = clusterRadius,
                resourceType = type
            });
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
        
        Gizmos.color = starterRingInnerGizmoColor;
        float minRadiusWorld = starterRingInnerRadius * cellSize;
        DrawCircleGizmo(centerWorld, minRadiusWorld);
        
        Gizmos.color = starterRingOuterGizmoColor;
        float maxRadiusWorld = starterRingOuterRadius * cellSize;
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
