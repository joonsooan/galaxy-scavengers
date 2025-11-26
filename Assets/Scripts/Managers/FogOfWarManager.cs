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
    [SerializeField] private Tilemap fogTilemap; // Overlay tilemap for fog visualization
    
    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.1f; // How often to update visibility
    [SerializeField] private bool enableDebugLogs = false; // Enable debug logging
    
    [Header("Visual Settings")]
    [SerializeField] private TileBase fogTile; // Tile to use for fog overlay (use a white tile for best results)
    [SerializeField] private Color fullyVisibleColor = new Color(1f, 1f, 1f, 0f); // Transparent (no fog)
    [SerializeField] private Color partlyVisibleColor = new Color(0.7f, 0.7f, 0.7f, 0.4f); // Slightly dark (30% dark overlay)
    [SerializeField] private Color invisibleColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Very dark (80% dark overlay)
    
    // Visibility tracking
    private Dictionary<Vector3Int, FogOfWarState> _tileVisibility = new Dictionary<Vector3Int, FogOfWarState>();
    private HashSet<Vector3Int> _exploredTiles = new HashSet<Vector3Int>();
    
    // Vision providers (buildings and units that provide vision)
    private List<IVisionProvider> _visionProviders = new List<IVisionProvider>();
    
    // Events
    public static event Action<Vector3Int, FogOfWarState> OnVisibilityChanged;
    public static event Action<Vector3Int> OnTileExplored;
    
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
            // Try to get ground tilemap from BuildingManager
            groundTilemap = BuildingManager.Instance.GroundTilemap;
        }
        
        // Create fog tilemap if it doesn't exist
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
        
        // Find or create fog tilemap GameObject
        GameObject fogTilemapObj = GameObject.Find("FogTilemap");
        if (fogTilemapObj == null)
        {
            fogTilemapObj = new GameObject("FogTilemap");
            fogTilemapObj.transform.SetParent(grid.transform);
        }
        
        fogTilemap = fogTilemapObj.GetComponent<Tilemap>();
        if (fogTilemap == null)
        {
            fogTilemap = fogTilemapObj.AddComponent<Tilemap>();
            fogTilemapObj.AddComponent<TilemapRenderer>();
        }
        
        // Set sorting order to be above other tilemaps
        TilemapRenderer renderer = fogTilemap.GetComponent<TilemapRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 100; // High sorting order to render on top
        }
        
        // Set the fog tilemap's base color to white for proper tinting
        fogTilemap.color = Color.white;
        
        // Ensure the fog tilemap is enabled and visible
        fogTilemap.gameObject.SetActive(true);
    }
    
    private void Start()
    {
        // Ensure fog tilemap is created before initialization
        if (fogTilemap == null)
        {
            CreateFogTilemap();
        }
        
        // Initialize all tiles as invisible
        InitializeFogOfWar();
        
        // Explore starting area (where main structure is)
        ExploreStartingArea();
        
        // Register all existing vision providers (in case they were created before FogOfWarManager)
        RegisterAllExistingVisionProviders();
        
        // Start updating visibility
        InvokeRepeating(nameof(UpdateVisibility), updateInterval, updateInterval);
    }
    
    private void RegisterAllExistingVisionProviders()
    {
        // Find all VisionProvider components in the scene
        VisionProvider[] visionProviders = FindObjectsByType<VisionProvider>(FindObjectsSortMode.None);
        foreach (var provider in visionProviders)
        {
            if (provider != null)
            {
                RegisterVisionProvider(provider);
                // Notify the provider that FogOfWarManager is ready
                provider.OnFogOfWarManagerReady();
            }
        }
        
        // Also find any components that implement IVisionProvider directly
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
    
    // Public method to manually trigger registration of all vision providers (useful for debugging)
    public void RefreshVisionProviders()
    {
        RegisterAllExistingVisionProviders();
    }
    
    private void ExploreStartingArea()
    {
        // Find MainStructure and explore area around it
        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        if (mainStructure != null && grid != null)
        {
            Vector3Int mainCell = grid.WorldToCell(mainStructure.transform.position);
            // Explore a 5x5 area around the main structure
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    Vector3Int cell = mainCell + new Vector3Int(x, y, 0);
                    ExploreTile(cell);
                }
            }
        }
    }
    
    private void InitializeFogOfWar()
    {
        if (groundTilemap == null)
        {
            // Try to get ground tilemap from BuildingManager
            if (BuildingManager.Instance != null)
            {
                groundTilemap = BuildingManager.Instance.GroundTilemap;
            }
            
            // Fallback: Try to find ground tilemap by name
            if (groundTilemap == null)
            {
                Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
                foreach (var tilemap in tilemaps)
                {
                    if (tilemap.name.ToLower().Contains("ground"))
                    {
                        groundTilemap = tilemap;
                        break;
                    }
                }
            }
        }
        
        if (groundTilemap == null)
        {
            Debug.LogWarning("[FogOfWarManager] Ground tilemap not found. Fog of war may not work correctly.");
            return;
        }
        
        if (fogTilemap == null)
        {
            Debug.LogWarning("[FogOfWarManager] Fog tilemap is null. Cannot initialize fog visuals.");
            return;
        }
        
        // Initialize all tiles in the tilemap as invisible
        BoundsInt bounds = groundTilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (groundTilemap.HasTile(pos))
            {
                _tileVisibility[pos] = FogOfWarState.Invisible;
                // Initialize fog overlay
                UpdateFogVisual(pos, FogOfWarState.Invisible);
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
            if (enableDebugLogs)
            {
                Debug.Log($"[FogOfWarManager] Unregistered vision provider: {provider.GetType().Name}");
            }
        }
    }
    
    private void UpdateVisibility()
    {
        if (grid == null) return;
        
        // Reset all tiles to explored state (if explored) or invisible
        Dictionary<Vector3Int, FogOfWarState> newVisibility = new Dictionary<Vector3Int, FogOfWarState>();
        
        // Copy explored state
        foreach (var exploredTile in _exploredTiles)
        {
            newVisibility[exploredTile] = FogOfWarState.PartlyVisible;
        }
        
        // Apply vision from all providers
        // Clean up null providers first
        _visionProviders.RemoveAll(p => p == null);
        
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
                            // Mark as fully visible
                            newVisibility[cell] = FogOfWarState.FullyVisible;
                            
                            // Mark as explored
                            if (!_exploredTiles.Contains(cell))
                            {
                                _exploredTiles.Add(cell);
                                OnTileExplored?.Invoke(cell);
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FogOfWarManager] Error processing vision provider {provider.GetType().Name}: {e.Message}");
            }
        }
        
        // Update visibility states and notify changes
        HashSet<Vector3Int> allTiles = new HashSet<Vector3Int>(_tileVisibility.Keys);
        allTiles.UnionWith(newVisibility.Keys);
        
        foreach (var tile in allTiles)
        {
            FogOfWarState oldState = _tileVisibility.ContainsKey(tile) ? _tileVisibility[tile] : FogOfWarState.Invisible;
            FogOfWarState newState = newVisibility.ContainsKey(tile) ? newVisibility[tile] : FogOfWarState.Invisible;
            
            if (oldState != newState)
            {
                _tileVisibility[tile] = newState;
                UpdateFogVisual(tile, newState);
                OnVisibilityChanged?.Invoke(tile, newState);
            }
        }
    }
    
    private void UpdateFogVisual(Vector3Int cell, FogOfWarState state)
    {
        if (fogTilemap == null || groundTilemap == null) return;
        
        // Only show fog on tiles that exist in the ground tilemap
        if (!groundTilemap.HasTile(cell)) return;
        
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
        
        // Ensure we have a tile in the fog tilemap
        // Use a white tile for the fog overlay - darker colors will darken the view
        if (!fogTilemap.HasTile(cell))
        {
            // Use fog tile if available, otherwise use ground tile as base
            TileBase tileToUse = fogTile != null ? fogTile : groundTilemap.GetTile(cell);
            if (tileToUse != null)
            {
                fogTilemap.SetTile(cell, tileToUse);
                // Reset color to white first to ensure proper tinting
                fogTilemap.SetColor(cell, Color.white);
            }
            else
            {
                return; // Can't create fog visual without a tile
            }
        }
        
        // Apply color tint to create darkness effect
        // Unity multiplies tile color * set color, so:
        // White tile (1,1,1) * dark color (0.2,0.2,0.2) = dark result (0.2,0.2,0.2)
        // IMPORTANT: The fog tile should be white for this to work correctly
        fogTilemap.SetColor(cell, fogColor);
        
        // Force refresh to ensure the color is applied
        fogTilemap.RefreshTile(cell);
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
        // Can place buildings on fully visible or partly visible tiles
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
    
    // Manually explore a tile (for testing or special cases)
    public void ExploreTile(Vector3Int cellPosition)
    {
        if (!_exploredTiles.Contains(cellPosition))
        {
            _exploredTiles.Add(cellPosition);
            OnTileExplored?.Invoke(cellPosition);
            
            // If not fully visible, set to partly visible
            if (!_tileVisibility.ContainsKey(cellPosition) || 
                _tileVisibility[cellPosition] == FogOfWarState.Invisible)
            {
                _tileVisibility[cellPosition] = FogOfWarState.PartlyVisible;
                UpdateFogVisual(cellPosition, FogOfWarState.PartlyVisible);
                OnVisibilityChanged?.Invoke(cellPosition, FogOfWarState.PartlyVisible);
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

