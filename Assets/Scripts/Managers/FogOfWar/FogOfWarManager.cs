using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Systems.Jobs;

public enum FogOfWarState
{
    Invisible,      // 자원 및 적이 보이지 않음, 건물 설치 불가
    PartlyVisible,  // 자원 및 적이 보이지 않음, 건물 설치 가능
    FullyVisible    // 모든 오브젝트가 보임, 건물 설치 가능
}

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }
    
    [Header("References")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap fogTilemap;
    
    [Header("Visual Settings")]
    [SerializeField] private TileBase fogTile;
    [SerializeField] private Color fullyVisibleColor = new (1f, 1f, 1f, 0f);
    [SerializeField] private Color partlyVisibleColor = new (0.7f, 0.7f, 0.7f, 0.4f);
    [SerializeField] private Color invisibleColor = new (0.2f, 0.2f, 0.2f, 0.8f);

    private readonly Dictionary<Vector3Int, FogOfWarState> _tileVisibility = new();
    private readonly HashSet<Vector3Int> _exploredTiles = new ();
    
    private readonly List<IVisionProvider> _visionProviders = new ();
    private Dictionary<IVisionProvider, HashSet<Vector3Int>> _providerAffectedTiles = new ();
    private readonly Dictionary<Vector3Int, List<VisibilityController>> _tileToControllers = new ();
    private readonly HashSet<Vector3Int> _currentlyVisibleTiles = new();
    
    private static bool suppressVisibilityEvents;
    
    public bool IsInitialized => _fogInitialized;
    
    public static void SetSuppressVisibilityEvents(bool suppress)
    {
        suppressVisibilityEvents = suppress;
    }
    
    private MapGenerator _cachedMapGenerator;
    
    private bool _isInitializing;
    private bool _fogInitialized;
    private bool _respectFog = true;
    
    private FogOfWarInitializer _initializer;
    private FogOfWarVisibilityCalculator _visibilityCalculator;
    private FogOfWarVisualUpdater _visualUpdater;
    
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
        
        _initializer = new FogOfWarInitializer(this, grid, fogTilemap, fogTile, groundTilemap, null, invisibleColor);
        _visibilityCalculator = new FogOfWarVisibilityCalculator(grid, _providerAffectedTiles, _exploredTiles);
        _visualUpdater = new FogOfWarVisualUpdater(fogTilemap, fogTile, fullyVisibleColor, partlyVisibleColor, invisibleColor, groundTilemap, null);
    }
    
    private void CreateFogTilemap()
    {
        _initializer?.CreateFogTilemap();
        fogTilemap = _initializer?.GetFogTilemap();
    }
    
    private void Start()
    {
        if (fogTilemap == null)
        {
            CreateFogTilemap();
        }
        
        _cachedMapGenerator = FindFirstObjectByType<MapGenerator>();
        
        _initializer?.SetReferences(fogTilemap, groundTilemap, _cachedMapGenerator);
        _visualUpdater = new FogOfWarVisualUpdater(fogTilemap, fogTile, fullyVisibleColor, partlyVisibleColor, invisibleColor, groundTilemap, _cachedMapGenerator);

        StartCoroutine(DelayedFogInitialization(null));
        RegisterAllExistingVisionProviders();
        InvokeRepeating(nameof(CleanupNullProviders), 5f, 5f);
    }
    
    public void StartFogInitializationWithProgress(IInitializationProgress progress)
    {
        StartCoroutine(DelayedFogInitialization(progress));
    }
    
    private System.Collections.IEnumerator DelayedFogInitialization(IInitializationProgress progress = null)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        if (progress != null)
            progress.UpdateProgress(0.0f, "맵 생성 완료 대기 중...");
        
        while (_cachedMapGenerator != null && 
               ((_cachedMapGenerator.GroundTilemap != null && _cachedMapGenerator.GroundTilemap.cellBounds.size.x == 0) ||
                (_cachedMapGenerator.WallTilemap != null && _cachedMapGenerator.WallTilemap.cellBounds.size.x == 0)))
        {
            yield return null;
        }
        
        yield return new WaitForEndOfFrame();
        
        if (_initializer != null)
        {
            _initializer.SetCoroutineRunner(this);
            yield return StartCoroutine(_initializer.InitializeFogOfWarAsync(_tileVisibility, progress));
        }
        else
        {
            InitializeFogOfWar();
        }
        
        _fogInitialized = true;
        
        UpdateVisibility();
        UpdateAllVisibilityControllers();
        
        if (MapObjectSpawner.Instance != null)
        {
            MapObjectSpawner.Instance.UpdateResourceTileVisibility();
        }
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
    }
    
    private void InitializeFogOfWar()
    {
        _isInitializing = true;
        _initializer?.InitializeFogOfWar(_tileVisibility);
        _isInitializing = false;
    }
    
    public void RegisterVisionProvider(IVisionProvider provider)
    {
        if (provider != null && !_visionProviders.Contains(provider))
        {
            _visionProviders.Add(provider);
            
            if (!_providerAffectedTiles.ContainsKey(provider))
            {
                _providerAffectedTiles[provider] = new HashSet<Vector3Int>();
            }
            
            if (!(provider is VisionProvider))
            {
                UpdateVisibility();
            }
        }
    }
    
    public void UnregisterVisionProvider(IVisionProvider provider)
    {
        if (provider != null && _visionProviders.Remove(provider))
        {
            HashSet<Vector3Int> affectedTiles = null;
            if (_providerAffectedTiles.TryGetValue(provider, out HashSet<Vector3Int> tiles))
            {
                affectedTiles = new HashSet<Vector3Int>(tiles);
            }
            
            _providerAffectedTiles.Remove(provider);
            
            if (affectedTiles != null && affectedTiles.Count > 0)
            {
                UpdateSpecificTiles(affectedTiles);
            }
        }
    }
    
    public void OnVisionProviderTilesChanged(IVisionProvider provider, HashSet<Vector3Int> enteredTiles, HashSet<Vector3Int> exitedTiles)
    {
        if (provider == null || !_visionProviders.Contains(provider)) return;
        
        if ((enteredTiles == null || enteredTiles.Count == 0) && (exitedTiles == null || exitedTiles.Count == 0))
        {
            return;
        }
        
        if (!_providerAffectedTiles.ContainsKey(provider))
        {
            _providerAffectedTiles[provider] = new HashSet<Vector3Int>();
        }
        
        if (enteredTiles != null && enteredTiles.Count > 0)
        {
            _providerAffectedTiles[provider].UnionWith(enteredTiles);
        }
        
        if (exitedTiles != null && exitedTiles.Count > 0)
        {
            _providerAffectedTiles[provider].ExceptWith(exitedTiles);
        }
        
        HashSet<Vector3Int> tilesToCheck = new HashSet<Vector3Int>();
        if (enteredTiles != null) tilesToCheck.UnionWith(enteredTiles);
        if (exitedTiles != null) tilesToCheck.UnionWith(exitedTiles);
        
        if (tilesToCheck.Count == 0) return;
        
        UpdateSpecificTiles(tilesToCheck);
    }
    
    private void UpdateSpecificTiles(HashSet<Vector3Int> tilesToCheck)
    {
        if (!_fogInitialized || _isInitializing)
        {
            return;
        }
        
        if (grid == null || tilesToCheck == null || tilesToCheck.Count == 0) return;
        
        Dictionary<Vector3Int, FogOfWarState> newVisibility = new Dictionary<Vector3Int, FogOfWarState>(tilesToCheck.Count);
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
            FogOfWarState state = _exploredTiles.Contains(tile) ? FogOfWarState.PartlyVisible : FogOfWarState.Invisible;
            
            if (allVisibleTiles.Contains(tile))
            {
                state = FogOfWarState.FullyVisible;
                
                if (!_exploredTiles.Contains(tile))
                {
                    _exploredTiles.Add(tile);
                }
            }
            
            newVisibility[tile] = state;
        }
        
        foreach (var tile in tilesToCheck)
        {
            FogOfWarState oldState = _tileVisibility.TryGetValue(tile, out var value) ? value : FogOfWarState.Invisible;
            FogOfWarState newState = newVisibility.TryGetValue(tile, out var value1) ? value1 : FogOfWarState.Invisible;
            
            if (oldState != newState)
            {
                _tileVisibility[tile] = newState;
                _visualUpdater?.UpdateFogVisual(tile, newState);
                
                if (newState == FogOfWarState.FullyVisible)
                {
                    _currentlyVisibleTiles.Add(tile);
                }
                else
                {
                    _currentlyVisibleTiles.Remove(tile);
                }
                
                if (!suppressVisibilityEvents)
                {
                    SafeInvokeVisibilityChanged(tile, newState);
                    UpdateResourceTileVisibilityAtTile(tile);
                }
            }
        }
    }
    
    private void UpdateResourceTileVisibilityAtTile(Vector3Int tile)
    {
        if (MapObjectSpawner.Instance != null)
        {
            MapObjectSpawner.Instance.UpdateResourceTileVisibilityAtCell(tile);
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
        if (!_fogInitialized || _isInitializing)
        {
            return;
        }
        
        if (grid == null) return;
        
        HashSet<Vector3Int> tilesToCheck = new HashSet<Vector3Int>();
        
        foreach (var exploredTile in _exploredTiles)
        {
            tilesToCheck.Add(exploredTile);
        }
        
        Dictionary<IVisionProvider, HashSet<Vector3Int>> newAffectedTiles = _visibilityCalculator.CalculateVisionProviderTiles(_visionProviders);
        
        foreach (var kvp in newAffectedTiles)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                foreach (var tile in kvp.Value)
                {
                    tilesToCheck.Add(tile);
                }
            }
        }
        
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
        
        _providerAffectedTiles = newAffectedTiles;
        
        Dictionary<Vector3Int, FogOfWarState> newVisibility = _visibilityCalculator.CalculateVisibilityStates(tilesToCheck, newAffectedTiles);
        
        // 상태가 변한 타일만 실제 타일맵에 반영
        foreach (var tile in tilesToCheck)
        {
            FogOfWarState oldState = _tileVisibility.ContainsKey(tile) ? _tileVisibility[tile] : FogOfWarState.Invisible;
            FogOfWarState newState = newVisibility.ContainsKey(tile) ? newVisibility[tile] : FogOfWarState.Invisible;
            
            // 상태가 변경되었을 때만 로직 수행
            if (oldState != newState)
            {
                _tileVisibility[tile] = newState;
                _visualUpdater?.UpdateFogVisual(tile, newState);
                
                // 이벤트 발생
                if (!suppressVisibilityEvents)
                {
                    SafeInvokeVisibilityChanged(tile, newState);
                    UpdateResourceTileVisibilityAtTile(tile);
                }
            }
        }
    }
    
    private void UpdateFogVisual(Vector3Int cell, FogOfWarState state)
    {
        if (_isInitializing || !_fogInitialized)
        {
            return;
        }
        
        _visualUpdater?.UpdateFogVisual(cell, state);
    }

    private FogOfWarState GetVisibilityState(Vector3Int cellPosition)
    {
        if (_tileVisibility.TryGetValue(cellPosition, out FogOfWarState state))
        {
            return state;
        }
        return FogOfWarState.Invisible;
    }
    
    public bool CanPlaceBuilding(Vector3Int cellPosition)
    {
        FogOfWarState state = GetVisibilityState(cellPosition);
        return state != FogOfWarState.Invisible;
    }
    
    public bool CanSeeEnemies(Vector3Int cellPosition)
    {
        if (!_respectFog)
        {
            return true;
        }
        return GetVisibilityState(cellPosition) == FogOfWarState.FullyVisible;
    }
    
    public bool CanSeeResources(Vector3Int cellPosition)
    {
        if (!_respectFog)
        {
            return true;
        }
        
        if (_tileVisibility.TryGetValue(cellPosition, out FogOfWarState state))
        {
            return state != FogOfWarState.Invisible;
        }
        
        if (_exploredTiles.Contains(cellPosition))
        {
            return true;
        }
        
        return false;
    }
    
    public void ToggleFogVisibility()
    {
        _respectFog = !_respectFog;
        
        if (fogTilemap != null)
        {
            fogTilemap.gameObject.SetActive(_respectFog);
        }
        
        UpdateAllVisibilityControllers();
        
        if (MapObjectSpawner.Instance != null)
        {
            MapObjectSpawner.Instance.UpdateResourceTileVisibility();
        }
    }
    
    private void UpdateAllVisibilityControllers()
    {
        _visualUpdater?.UpdateAllVisibilityControllers();
    }
    
    public void RefreshFogOfWar()
    {
        if (!_fogInitialized)
        {
            return;
        }
        
        if (_tileVisibility.Count == 0)
        {
            InitializeFogOfWar();
        }
        UpdateVisibility();
        
        if (MapObjectSpawner.Instance != null)
        {
            MapObjectSpawner.Instance.UpdateResourceTileVisibility();
        }
    }
    
    public void ExploreTile(Vector3Int cellPosition)
    {
        if (_exploredTiles.Add(cellPosition))
        {
            if (!_tileVisibility.ContainsKey(cellPosition) || 
                _tileVisibility[cellPosition] == FogOfWarState.Invisible)
            {
                _tileVisibility[cellPosition] = FogOfWarState.PartlyVisible;
                _visualUpdater?.UpdateFogVisual(cellPosition, FogOfWarState.PartlyVisible);
                
                if (!suppressVisibilityEvents)
                {
                    SafeInvokeVisibilityChanged(cellPosition, FogOfWarState.PartlyVisible);
                    UpdateResourceTileVisibilityAtTile(cellPosition);
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
        
        if (_tileToControllers[tilePosition].Count == 0)
        {
            _tileToControllers.Remove(tilePosition);
        }
    }
    
    private void SafeInvokeVisibilityChanged(Vector3Int cell, FogOfWarState state)
    {
        if (_tileToControllers.TryGetValue(cell, out List<VisibilityController> controllers))
        {
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
                    controller.OnVisibilityChangedDirect(cell, state);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[FogOfWarManager] Error notifying VisibilityController: {e.Message}");
                    controllers.RemoveAt(i);
                }
            }
            
            if (controllers.Count == 0)
            {
                _tileToControllers.Remove(cell);
            }
        }
    }
}