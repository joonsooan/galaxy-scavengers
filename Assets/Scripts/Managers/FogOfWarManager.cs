using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum FogOfWarState
{
    Invisible,      // Default state - only terrain visible, no resources/enemies, can't place buildings
    PartlyVisible,  // Explored - terrain and resources visible, no enemies, can place buildings
    FullyVisible    // Currently visible - everything visible, can place buildings
}

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }
    
    [Header("References")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap fogTilemap;
    
    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.1f;
    [SerializeField] private bool enableDebugLogs = false;
    
    [Header("Visual Settings")]
    [Tooltip("The fog tile to use for fog overlay. IMPORTANT: The tile's sprite should be WHITE (RGB: 1,1,1) for proper color multiplication. The tile's color in the Tile asset should also be white. Opacity is controlled by the alpha channel in the fog colors below.")]
    [SerializeField] private TileBase fogTile;
    [Tooltip("Color applied to fog tile when fully visible. Alpha controls opacity (0 = transparent, 1 = opaque).")]
    [SerializeField] private Color fullyVisibleColor = new Color(1f, 1f, 1f, 0f);
    [Tooltip("Color applied to fog tile when partly visible (explored). Alpha controls opacity (0 = transparent, 1 = opaque).")]
    [SerializeField] private Color partlyVisibleColor = new Color(0.7f, 0.7f, 0.7f, 0.4f);
    [Tooltip("Color applied to fog tile when invisible (unexplored). Alpha controls opacity (0 = transparent, 1 = opaque).")]
    [SerializeField] private Color invisibleColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    
    // Visibility tracking
    private Dictionary<Vector3Int, FogOfWarState> _tileVisibility = new Dictionary<Vector3Int, FogOfWarState>();
    private HashSet<Vector3Int> _exploredTiles = new HashSet<Vector3Int>();
    
    // Vision providers (buildings and units that provide vision)
    private List<IVisionProvider> _visionProviders = new List<IVisionProvider>();
    
    // Optimization: Track which tiles are affected by vision providers
    private Dictionary<IVisionProvider, HashSet<Vector3Int>> _providerAffectedTiles = new Dictionary<IVisionProvider, HashSet<Vector3Int>>();
    
    // Optimization: Track which VisibilityControllers are at which tiles (for targeted notifications)
    private Dictionary<Vector3Int, List<VisibilityController>> _tileToControllers = new Dictionary<Vector3Int, List<VisibilityController>>();
    
    // Track all tiles that are currently visible (for efficient lookups)
    private HashSet<Vector3Int> _currentlyVisibleTiles = new HashSet<Vector3Int>();
    
    // Events
    public static event Action<Vector3Int, FogOfWarState> OnVisibilityChanged;
    public static event Action<Vector3Int> OnTileExplored;
    
    // Flag to suppress event invocations during resource spawning
    private static bool _suppressVisibilityEvents = false;
    public static bool SuppressVisibilityEvents => _suppressVisibilityEvents;
    
    public static void SetSuppressVisibilityEvents(bool suppress)
    {
        _suppressVisibilityEvents = suppress;
    }
    
    // Cached references to avoid repeated FindFirstObjectByType calls (major performance optimization)
    private MapGenerator _cachedMapGenerator;
    private Tilemap _cachedGroundTilemap;
    private Tilemap _cachedWallTilemap;
    
    // Flag to prevent fog visual updates during map generation/initialization
    private bool _isInitializing = false;
    private bool _fogInitialized = false;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        if (grid == null)
        {
            grid = FindFirstObjectByType<Grid>();
        }
        
        if (groundTilemap == null && BuildingManager.Instance != null)
        {
            groundTilemap = BuildingManager.Instance.GroundTilemap;
        }
        
        if (fogTilemap == null)
        {
            CreateFogTilemap();
        }
    }
    
    private void CreateFogTilemap()
    {
        if (grid == null)
        {
            Debug.LogWarning("[FogOfWarManager] Cannot create fog tilemap: Grid is null");
            return;
        }
        
        GameObject fogTilemapObj = GameObject.Find("Fog Tilemap");
        if (fogTilemapObj == null)
        {
            fogTilemapObj = new GameObject("Fog Tilemap");
            fogTilemapObj.transform.SetParent(grid.transform);
        }
        
        fogTilemap = fogTilemapObj.GetComponent<Tilemap>();
        if (fogTilemap == null)
        {
            fogTilemap = fogTilemapObj.AddComponent<Tilemap>();
            fogTilemapObj.AddComponent<TilemapRenderer>();
        }
        
        TilemapRenderer renderer = fogTilemap.GetComponent<TilemapRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 100;
            renderer.mode = TilemapRenderer.Mode.Individual;
        }
        
        fogTilemap.color = Color.white;
        fogTilemap.gameObject.SetActive(true);
    }
    
    private void Start()
    {
        if (fogTilemap == null)
        {
            CreateFogTilemap();
        }
        
        // Cache MapGenerator reference once
        _cachedMapGenerator = FindFirstObjectByType<MapGenerator>();
        
        // Delay fog initialization until map generation is complete
        // This prevents thousands of individual UpdateFogVisual calls during tile generation
        StartCoroutine(DelayedFogInitialization());
        
        RegisterAllExistingVisionProviders();
        InvokeRepeating(nameof(CleanupNullProviders), 5f, 5f);
    }
    
    private System.Collections.IEnumerator DelayedFogInitialization()
    {
        // Wait until map generation is likely complete
        // Wait for end of frame to ensure all Awake/Start methods have run
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        // Additional wait to ensure MapGenerator.GenerateMap() has completed
        // Check if map has been generated by verifying tilemap bounds
        while (_cachedMapGenerator != null && 
               ((_cachedMapGenerator.GroundTilemap != null && _cachedMapGenerator.GroundTilemap.cellBounds.size.x == 0) ||
                (_cachedMapGenerator.WallTilemap != null && _cachedMapGenerator.WallTilemap.cellBounds.size.x == 0)))
        {
            yield return null; // Wait until map has been generated
        }
        
        yield return new WaitForEndOfFrame();
        
        // Cache tilemap references after map generation
        if (_cachedMapGenerator != null)
        {
            _cachedGroundTilemap = _cachedMapGenerator.GroundTilemap;
            _cachedWallTilemap = _cachedMapGenerator.WallTilemap;
        }
        
        // Initialize fog (batch operation - covers all tiles at once)
        InitializeFogOfWar();
        _fogInitialized = true;
        
        // After all tiles are covered with fog, update visibility once for existing units/buildings
        UpdateVisibility();
    }
    
    private void RegisterAllExistingVisionProviders()
    {
        VisionProvider[] visionProviders = FindObjectsByType<VisionProvider>(FindObjectsSortMode.None);
        foreach (var provider in visionProviders)
        {
            if (provider != null)
            {
                RegisterVisionProvider(provider);
                provider.OnFogOfWarManagerReady();
            }
        }
        
        MonoBehaviour[] allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mb in allMonoBehaviours)
        {
            if (mb is IVisionProvider provider && !(mb is VisionProvider))
            {
                RegisterVisionProvider(provider);
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[FogOfWarManager] Registered {_visionProviders.Count} vision providers");
        }
    }
    
    // public void RefreshVisionProviders()
    // {
    //     RegisterAllExistingVisionProviders();
    // }
    //
    // private void ExploreStartingArea()
    // {
    //     MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
    //     if (mainStructure != null && grid != null)
    //     {
    //         Vector3Int mainCell = grid.WorldToCell(mainStructure.transform.position);
    //         for (int x = -2; x <= 2; x++)
    //         {
    //             for (int y = -2; y <= 2; y++)
    //             {
    //                 Vector3Int cell = mainCell + new Vector3Int(x, y, 0);
    //                 ExploreTile(cell);
    //             }
    //         }
    //     }
    // }
    
    private void InitializeFogOfWar()
    {
        _isInitializing = true;
        
        List<Tilemap> terrainTilemaps = new List<Tilemap>();
        
        if (groundTilemap == null && BuildingManager.Instance != null)
        {
            groundTilemap = BuildingManager.Instance.GroundTilemap;
        }
        
        if (groundTilemap != null)
        {
            terrainTilemaps.Add(groundTilemap);
        }
        
        // Use cached reference instead of FindFirstObjectByType
        MapGenerator mapGenerator = _cachedMapGenerator;
        if (mapGenerator != null)
        {
            // Add ground tilemap
            if (mapGenerator.GroundTilemap != null)
            {
                Tilemap mapGenGroundTilemap = mapGenerator.GroundTilemap;
                if (!terrainTilemaps.Contains(mapGenGroundTilemap))
                {
                    terrainTilemaps.Add(mapGenGroundTilemap);
                }
            }
            
            // Add wall tilemap (walls also need fog coverage)
            if (mapGenerator.WallTilemap != null)
            {
                Tilemap mapGenWallTilemap = mapGenerator.WallTilemap;
                if (!terrainTilemaps.Contains(mapGenWallTilemap))
                {
                    terrainTilemaps.Add(mapGenWallTilemap);
                }
            }
            
            // Backward compatibility: also check old Tilemap property
            if (mapGenerator.Tilemap != null)
            {
                Tilemap mapGenTilemap = mapGenerator.Tilemap;
                if (!terrainTilemaps.Contains(mapGenTilemap))
                {
                    terrainTilemaps.Add(mapGenTilemap);
                }
            }
        }
        
        if (terrainTilemaps.Count == 0)
        {
            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            foreach (var tilemap in tilemaps)
            {
                if (tilemap.name.ToLower().Contains("ground") || tilemap.name.ToLower().Contains("terrain"))
                {
                    if (!terrainTilemaps.Contains(tilemap))
                    {
                        terrainTilemaps.Add(tilemap);
                    }
                }
            }
        }
        
        if (terrainTilemaps.Count == 0)
        {
            Debug.LogWarning("[FogOfWarManager] No terrain tilemaps found. Fog of war may not work correctly.");
            _isInitializing = false;
            return;
        }
        
        if (fogTilemap == null)
        {
            Debug.LogWarning("[FogOfWarManager] Fog tilemap is null. Cannot initialize fog visuals.");
            _isInitializing = false;
            return;
        }
        
        if (fogTile == null)
        {
            Debug.LogWarning("[FogOfWarManager] Fog tile is not set. Cannot initialize fog visuals.");
            _isInitializing = false;
            return;
        }
        
        // Collect all tile positions from all terrain tilemaps
        HashSet<Vector3Int> allTilePositions = new HashSet<Vector3Int>();
        foreach (Tilemap terrainTilemap in terrainTilemaps)
        {
            BoundsInt bounds = terrainTilemap.cellBounds;
            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                if (terrainTilemap.HasTile(pos))
                {
                    allTilePositions.Add(pos);
                    _tileVisibility[pos] = FogOfWarState.Invisible;
                }
            }
        }
        
        // Batch initialize all fog tiles at once (much more efficient than individual calls)
        if (allTilePositions.Count > 0)
        {
            // Calculate bounds for batch operation
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            
            foreach (Vector3Int pos in allTilePositions)
            {
                minX = Mathf.Min(minX, pos.x);
                minY = Mathf.Min(minY, pos.y);
                maxX = Mathf.Max(maxX, pos.x);
                maxY = Mathf.Max(maxY, pos.y);
            }
            
            BoundsInt fogBounds = new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
            TileBase[] fogTiles = new TileBase[fogBounds.size.x * fogBounds.size.y];
            
            // Fill fog tiles array
            foreach (Vector3Int pos in fogBounds.allPositionsWithin)
            {
                int index = (pos.x - minX) + (pos.y - minY) * fogBounds.size.x;
                if (allTilePositions.Contains(pos))
                {
                    fogTiles[index] = fogTile;
                }
                else
                {
                    fogTiles[index] = null;
                }
            }
            
            // Batch set all fog tiles at once
            fogTilemap.SetTilesBlock(fogBounds, fogTiles);
            
            // Batch set colors for all tiles at once (Unity doesn't have SetColorsBlock, so we do it in a loop but it's still faster than individual UpdateFogVisual calls)
            foreach (Vector3Int pos in allTilePositions)
            {
                fogTilemap.SetColor(pos, invisibleColor);
            }
        }
        
        fogTilemap.RefreshAllTiles();
        fogTilemap.CompressBounds();
        
        _isInitializing = false;
    }
    
    public void RegisterVisionProvider(IVisionProvider provider)
    {
        if (provider != null && !_visionProviders.Contains(provider))
        {
            _visionProviders.Add(provider);
            
            // Initialize affected tiles for this provider
            if (!_providerAffectedTiles.ContainsKey(provider))
            {
                _providerAffectedTiles[provider] = new HashSet<Vector3Int>();
            }
            
            // For VisionProvider components, they will call OnVisionProviderTilesChanged in Start()
            // For other IVisionProvider implementations, do a full update
            if (!(provider is VisionProvider))
            {
                UpdateVisibility();
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[FogOfWarManager] Registered vision provider: {provider.GetType().Name} at {provider.GetPosition()}, range: {provider.GetVisionRange()}");
            }
        }
    }
    
    public void UnregisterVisionProvider(IVisionProvider provider)
    {
        if (provider != null && _visionProviders.Remove(provider))
        {
            // Get tiles that were affected by this provider
            HashSet<Vector3Int> affectedTiles = null;
            if (_providerAffectedTiles.TryGetValue(provider, out HashSet<Vector3Int> tiles))
            {
                affectedTiles = new HashSet<Vector3Int>(tiles);
            }
            
            _providerAffectedTiles.Remove(provider);
            
            // Update tiles that were visible because of this provider
            if (affectedTiles != null && affectedTiles.Count > 0)
            {
                UpdateSpecificTiles(affectedTiles);
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[FogOfWarManager] Unregistered vision provider: {provider.GetType().Name}");
            }
        }
    }
    
    public void OnVisionProviderChanged(IVisionProvider provider)
    {
        // Legacy method - kept for compatibility but should use OnVisionProviderTilesChanged instead
        if (provider != null && _visionProviders.Contains(provider))
        {
            // Force full update (less efficient, but works)
            UpdateVisibility();
        }
    }
    
    public void OnVisionProviderTilesChanged(IVisionProvider provider, HashSet<Vector3Int> enteredTiles, HashSet<Vector3Int> exitedTiles)
    {
        if (provider == null || !_visionProviders.Contains(provider)) return;
        
        // Early exit if no tiles changed
        if ((enteredTiles == null || enteredTiles.Count == 0) && (exitedTiles == null || exitedTiles.Count == 0))
        {
            return;
        }
        
        // Update the provider's affected tiles cache
        if (!_providerAffectedTiles.ContainsKey(provider))
        {
            _providerAffectedTiles[provider] = new HashSet<Vector3Int>();
        }
        
        // Add entered tiles
        if (enteredTiles != null && enteredTiles.Count > 0)
        {
            _providerAffectedTiles[provider].UnionWith(enteredTiles);
        }
        
        // Remove exited tiles
        if (exitedTiles != null && exitedTiles.Count > 0)
        {
            _providerAffectedTiles[provider].ExceptWith(exitedTiles);
        }
        
        // ONLY check the tiles that actually changed (entered or exited)
        // Don't check all previously affected tiles - that defeats the purpose of incremental updates!
        HashSet<Vector3Int> tilesToCheck = new HashSet<Vector3Int>();
        if (enteredTiles != null) tilesToCheck.UnionWith(enteredTiles);
        if (exitedTiles != null) tilesToCheck.UnionWith(exitedTiles);
        
        // Early exit if no tiles to check
        if (tilesToCheck.Count == 0) return;
        
        // Update only the changed tiles
        UpdateSpecificTiles(tilesToCheck);
    }
    
    private void UpdateSpecificTiles(HashSet<Vector3Int> tilesToCheck)
    {
        // Don't update during initialization - wait until fog is fully initialized
        if (!_fogInitialized || _isInitializing)
        {
            return;
        }
        
        if (grid == null || tilesToCheck == null || tilesToCheck.Count == 0) return;
        
        // Calculate new visibility states only for tiles that need checking
        Dictionary<Vector3Int, FogOfWarState> newVisibility = new Dictionary<Vector3Int, FogOfWarState>(tilesToCheck.Count);
        
        // Pre-check: Build a set of all visible tiles from all providers for faster lookup
        HashSet<Vector3Int> allVisibleTiles = new HashSet<Vector3Int>();
        foreach (var kvp in _providerAffectedTiles)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                allVisibleTiles.UnionWith(kvp.Value);
            }
        }
        
        foreach (var tile in tilesToCheck)
        {
            // Start with explored state if explored, otherwise invisible
            FogOfWarState state = _exploredTiles.Contains(tile) ? FogOfWarState.PartlyVisible : FogOfWarState.Invisible;
            
            // Fast check: Is this tile visible by any provider?
            if (allVisibleTiles.Contains(tile))
            {
                state = FogOfWarState.FullyVisible;
                
                // Mark as explored
                if (!_exploredTiles.Contains(tile))
                {
                    _exploredTiles.Add(tile);
                    OnTileExplored?.Invoke(tile);
                }
            }
            
            newVisibility[tile] = state;
        }
        
        // Update only changed tiles (avoid duplicate notifications)
        foreach (var tile in tilesToCheck)
        {
            FogOfWarState oldState = _tileVisibility.ContainsKey(tile) ? _tileVisibility[tile] : FogOfWarState.Invisible;
            FogOfWarState newState = newVisibility.ContainsKey(tile) ? newVisibility[tile] : FogOfWarState.Invisible;
            
            // Only update if state actually changed
            if (oldState != newState)
            {
                _tileVisibility[tile] = newState;
                UpdateFogVisual(tile, newState);
                
                // Update currently visible tiles tracking
                if (newState == FogOfWarState.FullyVisible)
                {
                    _currentlyVisibleTiles.Add(tile);
                }
                else
                {
                    _currentlyVisibleTiles.Remove(tile);
                }
                
                // Only invoke event if not suppressed (during resource spawning)
                // This will notify only the VisibilityControllers registered at this specific tile
                if (!_suppressVisibilityEvents)
                {
                    SafeInvokeVisibilityChanged(tile, newState);
                }
            }
        }
    }
    
    private void CleanupNullProviders()
    {
        int removed = _visionProviders.RemoveAll(p => p == null);
        
        var keysToRemove = new List<IVisionProvider>();
        foreach (var kvp in _providerAffectedTiles)
        {
            if (kvp.Key == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        foreach (var key in keysToRemove)
        {
            _providerAffectedTiles.Remove(key);
        }
        
        // Clean up null VisibilityControllers from tile-to-controllers mapping
        var tilesToClean = new List<Vector3Int>();
        foreach (var kvp in _tileToControllers)
        {
            kvp.Value.RemoveAll(c => c == null);
            if (kvp.Value.Count == 0)
            {
                tilesToClean.Add(kvp.Key);
            }
        }
        foreach (var tile in tilesToClean)
        {
            _tileToControllers.Remove(tile);
        }
        
        if (removed > 0 || keysToRemove.Count > 0)
        {
            UpdateVisibility();
        }
    }
    
    private void UpdateVisibility()
    {
        // Don't update visibility during initialization - wait until fog is fully initialized
        if (!_fogInitialized || _isInitializing)
        {
            return;
        }
        
        if (grid == null) return;
        
        // HashSet으로 중복 좌표 방지
        HashSet<Vector3Int> tilesToCheck = new HashSet<Vector3Int>();
        
        // 이미 탐험한 타일들을 검사 대상에 추가
        // FullyVisible 타일이 PartlyVisible로 변했을 수도 있어서
        foreach (var exploredTile in _exploredTiles)
        {
            tilesToCheck.Add(exploredTile);
        }
        
        // 모든 visionProvider를 순회하며 현재 보이는 타일을 계산
        Dictionary<IVisionProvider, HashSet<Vector3Int>> newAffectedTiles = new Dictionary<IVisionProvider, HashSet<Vector3Int>>();
        
        foreach (var provider in _visionProviders)
        {
            if (provider == null || !provider.CheckIsActive()) continue;
            
            try
            {
                Vector3 worldPos = provider.GetPosition();
                float visionRange = provider.GetVisionRange();
                
                if (visionRange <= 0) continue;
                
                Vector3Int centerCell = grid.WorldToCell(worldPos);
                // 성능 최적화: 원형 거리 계산 전, 사각형(Bounding Box) 범위로 먼저 루프를 제한
                int rangeInCells = Mathf.CeilToInt(visionRange);
                
                // 영향을 받는 타일 목록
                HashSet<Vector3Int> affectedTiles = new HashSet<Vector3Int>();
                
                // visionProvider 주변의 사각형 범위를 순회
                for (int x = -rangeInCells; x <= rangeInCells; x++)
                {
                    for (int y = -rangeInCells; y <= rangeInCells; y++)
                    {
                        Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                        Vector3 cellWorldPos = grid.GetCellCenterWorld(cell);
                        float distance = Vector3.Distance(worldPos, cellWorldPos);
                        
                        // 실제 거리(원형 시야) 체크
                        if (distance <= visionRange)
                        {
                            affectedTiles.Add(cell);
                            
                            // 현재 시야에 들어온 타일도 검사 대상에 포함
                            tilesToCheck.Add(cell);
                        }
                    }
                }
                
                // visionProvider의 시야에 들어온 타일 목록 저장
                newAffectedTiles[provider] = affectedTiles;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FogOfWarManager] Error processing vision provider {provider.GetType().Name}: {e.Message}");
            }
        }
        
        // 이전에 시야를 제공했던 타일들도 검사 대상에 추가
        foreach (var kvp in _providerAffectedTiles)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                foreach (var tile in kvp.Value)
                {
                    tilesToCheck.Add(tile);
                }
            }
        }
        
        // 캐시 업데이트: 현재 계산된 시야 정보를 다음 프레임 비교용으로 저장
        _providerAffectedTiles = newAffectedTiles;
        
        // 수집된 타일 목록에 속한 타일들의 최종 상태 판정
        Dictionary<Vector3Int, FogOfWarState> newVisibility = new Dictionary<Vector3Int, FogOfWarState>();
        
        foreach (var tile in tilesToCheck)
        {
            // 이미 탐험한 적이 있다 -> PartlyVisible (회색)
            // 탐험한 적이 없다 -> Invisible (검은색)
            FogOfWarState state = _exploredTiles.Contains(tile) ? FogOfWarState.PartlyVisible : FogOfWarState.Invisible;
            
            // 현재 타일을 어떤 visionProvider가 보고 있는지 확인
            foreach (var kvp in newAffectedTiles)
            {
                if (kvp.Value.Contains(tile))
                {
                    // 보고 있다면 FullyVisible
                    state = FogOfWarState.FullyVisible;
                    
                    // 이번에 처음 본 타일이라면 탐험 목록에 추가
                    if (!_exploredTiles.Contains(tile))
                    {
                        _exploredTiles.Add(tile);
                        // 탐험 이벤트 발생
                        OnTileExplored?.Invoke(tile);
                    }
                    // 하나라도 보고 있으면 더 검사할 필요 없음 (FullyVisible 확정)
                    break;
                }
            }
            
            newVisibility[tile] = state;
        }
        
        // 상태가 변한 타일만 실제 타일맵에 반영
        foreach (var tile in tilesToCheck)
        {
            FogOfWarState oldState = _tileVisibility.ContainsKey(tile) ? _tileVisibility[tile] : FogOfWarState.Invisible;
            FogOfWarState newState = newVisibility.ContainsKey(tile) ? newVisibility[tile] : FogOfWarState.Invisible;
            
            // 상태가 변경되었을 때만 로직 수행
            if (oldState != newState)
            {
                _tileVisibility[tile] = newState;
                UpdateFogVisual(tile, newState);
                
                // 이벤트 발생
                if (!_suppressVisibilityEvents)
                {
                    SafeInvokeVisibilityChanged(tile, newState);
                }
            }
        }
    }
    
    private void UpdateFogVisual(Vector3Int cell, FogOfWarState state)
    {
        // Skip updates during initialization - they're handled in batch
        if (_isInitializing || !_fogInitialized)
        {
            return;
        }
        
        if (fogTilemap == null) return;
        
        // Cache ground tilemap reference if not cached
        if (groundTilemap == null && BuildingManager.Instance != null)
        {
            groundTilemap = BuildingManager.Instance.GroundTilemap;
        }
        
        bool hasTile = false;
        Tilemap tilemapWithTile = null;
        
        // Check cached ground tilemap first
        if (groundTilemap != null && groundTilemap.HasTile(cell))
        {
            hasTile = true;
            tilemapWithTile = groundTilemap;
        }
        // Use cached MapGenerator references (avoid expensive FindFirstObjectByType call)
        else if (_cachedMapGenerator != null)
        {
            // Cache tilemap references on first use to avoid repeated property access
            if (_cachedWallTilemap == null)
            {
                _cachedWallTilemap = _cachedMapGenerator.WallTilemap;
            }
            if (_cachedGroundTilemap == null)
            {
                _cachedGroundTilemap = _cachedMapGenerator.GroundTilemap;
            }
            
            // Check wall tilemap first (walls should have fog coverage)
            if (_cachedWallTilemap != null && _cachedWallTilemap.HasTile(cell))
            {
                hasTile = true;
                tilemapWithTile = _cachedWallTilemap;
            }
            // Check ground tilemap
            else if (_cachedGroundTilemap != null && _cachedGroundTilemap.HasTile(cell))
            {
                hasTile = true;
                tilemapWithTile = _cachedGroundTilemap;
            }
        }
        
        if (!hasTile || tilemapWithTile == null) return;
        
        Color fogColor;
        bool shouldRemoveFog = false;
        
        switch (state)
        {
            case FogOfWarState.FullyVisible:
                fogColor = fullyVisibleColor;
                if (fogColor.a <= 0.01f)
                {
                    shouldRemoveFog = true;
                }
                break;
            case FogOfWarState.PartlyVisible:
                fogColor = partlyVisibleColor;
                break;
            case FogOfWarState.Invisible:
            default:
                fogColor = invisibleColor;
                break;
        }
        
        if (shouldRemoveFog)
        {
            if (fogTilemap.HasTile(cell))
            {
                fogTilemap.SetTile(cell, null);
            }
            return;
        }
        
        if (fogTile == null)
        {
            Debug.LogWarning("[FogOfWarManager] Fog tile is not set. Please assign a fog tile in the inspector.");
            return;
        }
        
        bool tileChanged = !fogTilemap.HasTile(cell) || fogTilemap.GetTile(cell) != fogTile;
        if (tileChanged)
        {
            fogTilemap.SetTile(cell, fogTile);
        }
        
        Color currentColor = fogTilemap.GetColor(cell);
        if (currentColor != fogColor || tileChanged)
        {
            fogTilemap.SetColor(cell, fogColor);
            fogTilemap.RefreshTile(cell);
        }
    }
    
    public FogOfWarState GetVisibilityState(Vector3Int cellPosition)
    {
        if (_tileVisibility.TryGetValue(cellPosition, out FogOfWarState state))
        {
            return state;
        }
        return FogOfWarState.Invisible;
    }
    
    public FogOfWarState GetVisibilityState(Vector3 worldPosition)
    {
        if (grid == null) return FogOfWarState.Invisible;
        Vector3Int cell = grid.WorldToCell(worldPosition);
        return GetVisibilityState(cell);
    }
    
    public bool IsFullyVisible(Vector3Int cellPosition)
    {
        return GetVisibilityState(cellPosition) == FogOfWarState.FullyVisible;
    }
    
    public bool IsPartlyVisible(Vector3Int cellPosition)
    {
        FogOfWarState state = GetVisibilityState(cellPosition);
        return state == FogOfWarState.PartlyVisible || state == FogOfWarState.FullyVisible;
    }
    
    public bool IsExplored(Vector3Int cellPosition)
    {
        return _exploredTiles.Contains(cellPosition);
    }
    
    public bool CanPlaceBuilding(Vector3Int cellPosition)
    {
        FogOfWarState state = GetVisibilityState(cellPosition);
        return state == FogOfWarState.FullyVisible || state == FogOfWarState.PartlyVisible;
    }
    
    public bool CanSeeEnemies(Vector3Int cellPosition)
    {
        return GetVisibilityState(cellPosition) == FogOfWarState.FullyVisible;
    }
    
    public bool CanSeeResources(Vector3Int cellPosition)
    {
        FogOfWarState state = GetVisibilityState(cellPosition);
        return state == FogOfWarState.FullyVisible || state == FogOfWarState.PartlyVisible;
    }
    
    public void RefreshFogOfWar()
    {
        // Only refresh if fog is already initialized
        // If not initialized yet, DelayedFogInitialization will handle it
        if (!_fogInitialized)
        {
            return;
        }
        
        if (_tileVisibility.Count == 0)
        {
            InitializeFogOfWar();
        }
        UpdateVisibility();
    }
    
    public void ExploreTile(Vector3Int cellPosition)
    {
        if (!_exploredTiles.Contains(cellPosition))
        {
            _exploredTiles.Add(cellPosition);
            OnTileExplored?.Invoke(cellPosition);
            
            if (!_tileVisibility.ContainsKey(cellPosition) || 
                _tileVisibility[cellPosition] == FogOfWarState.Invisible)
            {
                _tileVisibility[cellPosition] = FogOfWarState.PartlyVisible;
                UpdateFogVisual(cellPosition, FogOfWarState.PartlyVisible);
                
                if (!_suppressVisibilityEvents)
                {
                    SafeInvokeVisibilityChanged(cellPosition, FogOfWarState.PartlyVisible);
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    public void RegisterVisibilityController(VisibilityController controller, Vector3Int tilePosition)
    {
        if (controller == null) return;
        
        if (!_tileToControllers.ContainsKey(tilePosition))
        {
            _tileToControllers[tilePosition] = new List<VisibilityController>();
        }
        
        if (!_tileToControllers[tilePosition].Contains(controller))
        {
            _tileToControllers[tilePosition].Add(controller);
        }
    }
    
    public void UnregisterVisibilityController(VisibilityController controller, Vector3Int tilePosition)
    {
        if (controller == null || !_tileToControllers.ContainsKey(tilePosition)) return;
        
        _tileToControllers[tilePosition].Remove(controller);
        
        // Clean up empty lists
        if (_tileToControllers[tilePosition].Count == 0)
        {
            _tileToControllers.Remove(tilePosition);
        }
    }
    
    private void SafeInvokeVisibilityChanged(Vector3Int cell, FogOfWarState state)
    {
        // Only notify VisibilityControllers that are actually at this tile position
        if (_tileToControllers.TryGetValue(cell, out List<VisibilityController> controllers))
        {
            // Clean up null controllers while iterating
            for (int i = controllers.Count - 1; i >= 0; i--)
            {
                VisibilityController controller = controllers[i];
                if (controller == null)
                {
                    controllers.RemoveAt(i);
                    continue;
                }
                
                try
                {
                    // Directly call the method instead of using events for better performance
                    // This avoids the overhead of the global event system
                    controller.OnVisibilityChangedDirect(cell, state);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[FogOfWarManager] Error notifying VisibilityController: {e.Message}");
                    controllers.RemoveAt(i);
                }
            }
            
            // Clean up empty lists
            if (controllers.Count == 0)
            {
                _tileToControllers.Remove(cell);
            }
        }
    }
}