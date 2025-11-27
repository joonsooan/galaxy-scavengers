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
            renderer.sortingOrder = 100; // High sorting order to render on top
            // Ensure the renderer supports transparency for fog overlay
            renderer.mode = TilemapRenderer.Mode.Individual;
        }
        
        // Set tilemap color to white so individual cell colors work correctly
        fogTilemap.color = Color.white;
        fogTilemap.gameObject.SetActive(true);
    }
    
    private void Start()
    {
        if (fogTilemap == null)
        {
            CreateFogTilemap();
        }
        
        InitializeFogOfWar();
        // ExploreStartingArea();
        RegisterAllExistingVisionProviders();
        InvokeRepeating(nameof(UpdateVisibility), updateInterval, updateInterval);
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
        List<Tilemap> terrainTilemaps = new List<Tilemap>();
        
        if (groundTilemap == null && BuildingManager.Instance != null)
        {
            groundTilemap = BuildingManager.Instance.GroundTilemap;
        }
        
        if (groundTilemap != null)
        {
            terrainTilemaps.Add(groundTilemap);
        }
        
        MapGenerator mapGenerator = FindFirstObjectByType<MapGenerator>();
        if (mapGenerator != null && mapGenerator.Tilemap != null)
        {
            Tilemap mapGenTilemap = mapGenerator.Tilemap;
            if (!terrainTilemaps.Contains(mapGenTilemap))
            {
                terrainTilemaps.Add(mapGenTilemap);
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
            return;
        }
        
        if (fogTilemap == null)
        {
            Debug.LogWarning("[FogOfWarManager] Fog tilemap is null. Cannot initialize fog visuals.");
            return;
        }
        
        // Initialize all tiles in all terrain tilemaps as invisible
        foreach (Tilemap terrainTilemap in terrainTilemaps)
        {
            BoundsInt bounds = terrainTilemap.cellBounds;
            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                if (terrainTilemap.HasTile(pos))
                {
                    _tileVisibility[pos] = FogOfWarState.Invisible;
                    // Initialize fog overlay
                    UpdateFogVisual(pos, FogOfWarState.Invisible);
                }
            }
        }
        
        // Refresh the fog tilemap after initialization
        fogTilemap.RefreshAllTiles();
        fogTilemap.CompressBounds();
    }
    
    public void RegisterVisionProvider(IVisionProvider provider)
    {
        if (provider != null && !_visionProviders.Contains(provider))
        {
            _visionProviders.Add(provider);
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
            // Remove from affected tiles tracking
            _providerAffectedTiles.Remove(provider);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[FogOfWarManager] Unregistered vision provider: {provider.GetType().Name}");
            }
        }
    }
    
    private void UpdateVisibility()
    {
        if (grid == null) return;
        
        // Clean up null providers first
        _visionProviders.RemoveAll(p => p == null);
        
        // Clean up null providers from affected tiles dictionary
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
        
        // Collect all tiles that need to be checked (only tiles affected by vision providers)
        HashSet<Vector3Int> tilesToCheck = new HashSet<Vector3Int>();
        
        // Add all explored tiles (they should be checked for state changes)
        foreach (var exploredTile in _exploredTiles)
        {
            tilesToCheck.Add(exploredTile);
        }
        
        // Track new affected tiles for each provider
        Dictionary<IVisionProvider, HashSet<Vector3Int>> newAffectedTiles = new Dictionary<IVisionProvider, HashSet<Vector3Int>>();
        
        // Apply vision from all providers and collect affected tiles
        foreach (var provider in _visionProviders)
        {
            if (provider == null || !provider.CheckIsActive()) continue;
            
            try
            {
                Vector3 worldPos = provider.GetPosition();
                float visionRange = provider.GetVisionRange();
                
                if (visionRange <= 0) continue;
                
                Vector3Int centerCell = grid.WorldToCell(worldPos);
                int rangeInCells = Mathf.CeilToInt(visionRange);
                
                HashSet<Vector3Int> affectedTiles = new HashSet<Vector3Int>();
                
                // Check all cells within vision range
                for (int x = -rangeInCells; x <= rangeInCells; x++)
                {
                    for (int y = -rangeInCells; y <= rangeInCells; y++)
                    {
                        Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                        Vector3 cellWorldPos = grid.GetCellCenterWorld(cell);
                        float distance = Vector3.Distance(worldPos, cellWorldPos);
                        
                        if (distance <= visionRange)
                        {
                            affectedTiles.Add(cell);
                            tilesToCheck.Add(cell);
                        }
                    }
                }
                
                newAffectedTiles[provider] = affectedTiles;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FogOfWarManager] Error processing vision provider {provider.GetType().Name}: {e.Message}");
            }
        }
        
        // Check tiles that were previously affected but might not be anymore
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
        
        // Update affected tiles tracking
        _providerAffectedTiles = newAffectedTiles;
        
        // Calculate new visibility states only for tiles that need checking
        Dictionary<Vector3Int, FogOfWarState> newVisibility = new Dictionary<Vector3Int, FogOfWarState>();
        
        foreach (var tile in tilesToCheck)
        {
            // Start with explored state if explored
            FogOfWarState state = _exploredTiles.Contains(tile) ? FogOfWarState.PartlyVisible : FogOfWarState.Invisible;
            
            // Check if any provider makes it fully visible
            foreach (var kvp in newAffectedTiles)
            {
                if (kvp.Value.Contains(tile))
                {
                    state = FogOfWarState.FullyVisible;
                    
                    // Mark as explored
                    if (!_exploredTiles.Contains(tile))
                    {
                        _exploredTiles.Add(tile);
                        OnTileExplored?.Invoke(tile);
                    }
                    break;
                }
            }
            
            newVisibility[tile] = state;
        }
        
        // Update only changed tiles
        foreach (var tile in tilesToCheck)
        {
            FogOfWarState oldState = _tileVisibility.ContainsKey(tile) ? _tileVisibility[tile] : FogOfWarState.Invisible;
            FogOfWarState newState = newVisibility.ContainsKey(tile) ? newVisibility[tile] : FogOfWarState.Invisible;
            
            if (oldState != newState)
            {
                _tileVisibility[tile] = newState;
                UpdateFogVisual(tile, newState);
                
                // Only invoke event if not suppressed (during resource spawning)
                if (!_suppressVisibilityEvents)
                {
                    OnVisibilityChanged?.Invoke(tile, newState);
                }
            }
        }
    }
    
    private void UpdateFogVisual(Vector3Int cell, FogOfWarState state)
    {
        if (fogTilemap == null) return;
        
        // Ensure groundTilemap reference is up to date
        if (groundTilemap == null && BuildingManager.Instance != null)
        {
            groundTilemap = BuildingManager.Instance.GroundTilemap;
        }
        
        // Always use groundTilemap as the source for tiles (it contains all terrain after copying)
        // This ensures fog works correctly for both ground and terrain tiles
        bool hasTile = false;
        Tilemap tilemapWithTile = null;
        
        if (groundTilemap != null && groundTilemap.HasTile(cell))
        {
            hasTile = true;
            tilemapWithTile = groundTilemap;
        }
        // Fallback to MapGenerator's tilemap only if groundTilemap is not available
        else
        {
            MapGenerator mapGenerator = FindFirstObjectByType<MapGenerator>();
            if (mapGenerator != null && mapGenerator.Tilemap != null)
            {
                if (mapGenerator.Tilemap.HasTile(cell))
                {
                    hasTile = true;
                    tilemapWithTile = mapGenerator.Tilemap;
                }
            }
        }
        
        // Only show fog on tiles that exist
        if (!hasTile || tilemapWithTile == null) return;
        
        Color fogColor;
        bool shouldRemoveFog = false;
        
        switch (state)
        {
            case FogOfWarState.FullyVisible:
                fogColor = fullyVisibleColor;
                // For fully visible, remove the fog tile to show normal brightness
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
        
        // Remove fog tile if fully visible
        if (shouldRemoveFog)
        {
            if (fogTilemap.HasTile(cell))
            {
                fogTilemap.SetTile(cell, null);
            }
            return;
        }
        
        // Always use the separate fog tile (never fall back to terrain tile)
        if (fogTile == null)
        {
            Debug.LogWarning("[FogOfWarManager] Fog tile is not set. Please assign a fog tile in the inspector.");
            return;
        }
        
        // Set or update the fog tile
        bool tileChanged = !fogTilemap.HasTile(cell) || fogTilemap.GetTile(cell) != fogTile;
        if (tileChanged)
        {
            fogTilemap.SetTile(cell, fogTile);
        }
        
        // Apply opacity/color to the fog tile to cover the terrain below
        // Unity multiplies the tile's color with the color we set here
        // Therefore, the fog tile should be WHITE (1,1,1) so the color multiplication works correctly
        // The alpha channel in fogColor controls the opacity of the fog overlay
        // Always set the color to ensure it's applied (even if tile already exists)
        // This ensures the color is updated when visibility state changes
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
        // Only initialize if not already initialized (to avoid expensive re-initialization)
        if (_tileVisibility.Count == 0)
        {
            InitializeFogOfWar();
        }
        
        // Force an immediate visibility update to light up tiles based on current buildings/units
        // This is more efficient than re-initializing everything
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
                
                // Only invoke event if not suppressed (during resource spawning)
                if (!_suppressVisibilityEvents)
                {
                    OnVisibilityChanged?.Invoke(cellPosition, FogOfWarState.PartlyVisible);
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
}

