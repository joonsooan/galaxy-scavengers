using System;
using System.Collections;
using System.Collections.Generic;
using Systems.Jobs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public enum FogOfWarState
{
    Invisible, // 자원 및 적이 보이지 않음, 건물 설치 불가
    PartlyVisible, // 자원 및 적이 보이지 않음, 건물 설치 가능
    FullyVisible // 모든 오브젝트가 보임, 건물 설치 가능
}

public class FogOfWarManager : MonoBehaviour
{
    private static bool suppressVisibilityEvents;

    [Header("References")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap fogTilemap;

    [Header("Visual Settings")]
    [SerializeField] private TileBase fogTile;
    [SerializeField] private Color fullyVisibleColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color partlyVisibleColor = new Color(0.7f, 0.7f, 0.7f, 0.4f);
    [SerializeField] private Color invisibleColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    private readonly HashSet<Vector3Int> _exploredTiles = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, List<VisibilityController>> _tileToControllers = new Dictionary<Vector3Int, List<VisibilityController>>();

    private readonly Dictionary<Vector3Int, FogOfWarState> _tileVisibility = new Dictionary<Vector3Int, FogOfWarState>();

    private readonly List<IVisionProvider> _visionProviders = new List<IVisionProvider>();

    private MapGenerator _cachedMapGenerator;
    private HashSet<Vector3Int> _currentlyVisibleTiles = new HashSet<Vector3Int>();

    private FogOfWarInitializer _initializer;

    private bool _isInitializing;
    private bool _isVisibilityUpdateRunning;
    private readonly Dictionary<IVisionProvider, HashSet<Vector3Int>> _providerAffectedTiles = new Dictionary<IVisionProvider, HashSet<Vector3Int>>();
    private bool _respectFog = true;
    private FogOfWarVisualUpdater _visualUpdater;
    public static FogOfWarManager Instance { get; private set; }

    public bool IsInitialized { get; private set; }

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
        _visualUpdater = new FogOfWarVisualUpdater(fogTilemap, fogTile, fullyVisibleColor, partlyVisibleColor, invisibleColor, groundTilemap, null);
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

        string currentSceneName = SceneManager.GetActiveScene().name;
        if (currentSceneName != "GameScene" && currentSceneName != "TutorialScene")
        {
            StartCoroutine(DelayedFogInitialization());
        }

        RegisterAllExistingVisionProviders();
        InvokeRepeating(nameof(CleanupNullProviders), 5f, 5f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void SetSuppressVisibilityEvents(bool suppress)
    {
        suppressVisibilityEvents = suppress;
    }

    private void CreateFogTilemap()
    {
        _initializer?.CreateFogTilemap();
        fogTilemap = _initializer?.GetFogTilemap();
    }

    public void StartFogInitializationWithProgress(IInitializationProgress progress)
    {
        // Debug.Log("[FogOfWar] StartFogInitializationWithProgress");
        StartCoroutine(DelayedFogInitialization(progress));
    }

    private IEnumerator DelayedFogInitialization(IInitializationProgress progress = null)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        while (_cachedMapGenerator != null &&
               (_cachedMapGenerator.GroundTilemap != null && _cachedMapGenerator.GroundTilemap.cellBounds.size.x == 0 ||
                   _cachedMapGenerator.WallTilemap != null && _cachedMapGenerator.WallTilemap.cellBounds.size.x == 0))
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();

        if (_initializer != null)
        {
            _initializer.SetCoroutineRunner(this);
            yield return StartCoroutine(_initializer.InitializeFogOfWarAsync(_tileVisibility, progress));
        }

        _isInitializing = false;
        IsInitialized = true;

        // Debug.Log("[FogOfWar] DelayedFogInitialization complete, calling ForceRebuildProviderTiles and UpdateVisibilityCoroutine");

        ForceRebuildProviderTiles();
        StartCoroutine(UpdateVisibilityCoroutine());
        UpdateAllVisibilityControllers();

        if (MapObjectSpawner.Instance != null)
        {
            MapObjectSpawner.Instance.UpdateResourceTileVisibility();
        }

        RefreshAllStrategicOverlayTileVisibility();
    }

    private void ForceRebuildProviderTiles()
    {
        _providerAffectedTiles.Clear();
        foreach (IVisionProvider provider in _visionProviders)
        {
            if (provider != null && provider.CheckIsActive())
            {
                if (provider is VisionProvider vp)
                {
                    vp.ForceUpdateAffectedTiles();
                }
            }
        }
    }

    private void RegisterAllExistingVisionProviders()
    {
        VisionProvider[] visionProviders = FindObjectsByType<VisionProvider>(FindObjectsSortMode.None);
        foreach (VisionProvider provider in visionProviders)
        {
            if (provider != null)
            {
                RegisterVisionProvider(provider);
                provider.OnFogOfWarManagerReady();
            }
        }

        MonoBehaviour[] allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (MonoBehaviour mb in allMonoBehaviours)
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
                StartCoroutine(UpdateVisibilityCoroutine());
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
        if (!IsInitialized || _isInitializing)
        {
            return;
        }

        if (grid == null || tilesToCheck == null || tilesToCheck.Count == 0) return;

        Dictionary<Vector3Int, FogOfWarState> newVisibility = new Dictionary<Vector3Int, FogOfWarState>(tilesToCheck.Count);

        foreach (Vector3Int tile in tilesToCheck)
        {
            FogOfWarState state = _exploredTiles.Contains(tile) ? FogOfWarState.PartlyVisible : FogOfWarState.Invisible;

            bool isTileVisible = false;
            foreach (KeyValuePair<IVisionProvider, HashSet<Vector3Int>> kvp in _providerAffectedTiles)
            {
                if (kvp.Key != null && kvp.Value != null && kvp.Value.Contains(tile))
                {
                    isTileVisible = true;
                    break;
                }
            }

            if (isTileVisible)
            {
                state = FogOfWarState.FullyVisible;

                if (!_exploredTiles.Contains(tile))
                {
                    _exploredTiles.Add(tile);
                }
            }

            newVisibility[tile] = state;
        }

        foreach (Vector3Int tile in tilesToCheck)
        {
            FogOfWarState oldState = _tileVisibility.TryGetValue(tile, out FogOfWarState value) ? value : FogOfWarState.Invisible;
            FogOfWarState newState = newVisibility.TryGetValue(tile, out FogOfWarState value1) ? value1 : FogOfWarState.Invisible;

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

        UpdateStrategicOverlayTileVisibilityAtCell(tile);
    }

    private void CleanupNullProviders()
    {
        int removed = _visionProviders.RemoveAll(p => p == null);

        List<IVisionProvider> keysToRemove = new List<IVisionProvider>();
        foreach (KeyValuePair<IVisionProvider, HashSet<Vector3Int>> kvp in _providerAffectedTiles)
        {
            if (kvp.Key == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        foreach (IVisionProvider key in keysToRemove)
        {
            _providerAffectedTiles.Remove(key);
        }

        List<Vector3Int> tilesToClean = new List<Vector3Int>();
        foreach (KeyValuePair<Vector3Int, List<VisibilityController>> kvp in _tileToControllers)
        {
            kvp.Value.RemoveAll(c => c == null);
            if (kvp.Value.Count == 0)
            {
                tilesToClean.Add(kvp.Key);
            }
        }
        foreach (Vector3Int tile in tilesToClean)
        {
            _tileToControllers.Remove(tile);
        }

        if (removed > 0 || keysToRemove.Count > 0)
        {
            StartCoroutine(UpdateVisibilityCoroutine());
        }
    }

    private IEnumerator UpdateVisibilityCoroutine()
    {
        if (!IsInitialized || _isInitializing)
        {
            // Debug.Log("[FogOfWar] UpdateVisibilityCoroutine skipped (not initialized or initializing)");
            yield break;
        }

        if (grid == null) yield break;

        // Debug.Log("[FogOfWar] UpdateVisibilityCoroutine started");

        if (_isVisibilityUpdateRunning)
        {
            // Debug.Log("[FogOfWar] UpdateVisibilityCoroutine request ignored; already running");
            yield break;
        }

        _isVisibilityUpdateRunning = true;

        HashSet<Vector3Int> tilesToCheck = new HashSet<Vector3Int>();

        foreach (Vector3Int exploredTile in _exploredTiles)
        {
            tilesToCheck.Add(exploredTile);
        }

        foreach (KeyValuePair<IVisionProvider, HashSet<Vector3Int>> kvp in _providerAffectedTiles)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                foreach (Vector3Int tile in kvp.Value)
                {
                    tilesToCheck.Add(tile);
                }
            }
        }

        foreach (Vector3Int visibleTile in _currentlyVisibleTiles)
        {
            tilesToCheck.Add(visibleTile);
            if (!_exploredTiles.Contains(visibleTile))
            {
                _exploredTiles.Add(visibleTile);
            }
        }

        Dictionary<Vector3Int, FogOfWarState> newVisibility = new Dictionary<Vector3Int, FogOfWarState>();

        foreach (Vector3Int tile in tilesToCheck)
        {
            FogOfWarState state = _exploredTiles.Contains(tile) ? FogOfWarState.PartlyVisible : FogOfWarState.Invisible;

            bool isTileVisible = false;
            foreach (KeyValuePair<IVisionProvider, HashSet<Vector3Int>> kvp in _providerAffectedTiles)
            {
                if (kvp.Key != null && kvp.Value != null && kvp.Value.Contains(tile))
                {
                    isTileVisible = true;
                    break;
                }
            }

            if (isTileVisible)
            {
                state = FogOfWarState.FullyVisible;

                if (!_exploredTiles.Contains(tile))
                {
                    _exploredTiles.Add(tile);
                }
            }

            newVisibility[tile] = state;
        }

        HashSet<Vector3Int> newCurrentlyVisibleTiles = new HashSet<Vector3Int>();

        foreach (Vector3Int tile in tilesToCheck)
        {
            FogOfWarState oldState = _tileVisibility.ContainsKey(tile) ? _tileVisibility[tile] : FogOfWarState.Invisible;
            FogOfWarState newState = newVisibility.ContainsKey(tile) ? newVisibility[tile] : FogOfWarState.Invisible;

            if (newState == FogOfWarState.FullyVisible)
            {
                newCurrentlyVisibleTiles.Add(tile);
            }

            if (oldState != newState)
            {
                _tileVisibility[tile] = newState;
                _visualUpdater?.UpdateFogVisual(tile, newState);

                if (!suppressVisibilityEvents)
                {
                    SafeInvokeVisibilityChanged(tile, newState);
                    UpdateResourceTileVisibilityAtTile(tile);
                }
            }
        }

        _currentlyVisibleTiles = newCurrentlyVisibleTiles;

        _isVisibilityUpdateRunning = false;

        // Debug.Log("[FogOfWar] UpdateVisibilityCoroutine finished");
    }


    private void UpdateFogVisual(Vector3Int cell, FogOfWarState state)
    {
        if (_isInitializing || !IsInitialized)
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

    public bool CanSeePersistentOverlay(Vector3Int cellPosition)
    {
        return CanSeeResources(cellPosition);
    }

    public void RefreshAllStrategicOverlayTileVisibility()
    {
        if (!IsInitialized)
        {
            return;
        }

        if (_cachedMapGenerator == null)
        {
            _cachedMapGenerator = FindFirstObjectByType<MapGenerator>();
        }

        if (_cachedMapGenerator == null)
        {
            return;
        }

        RefreshStrategicOverlayForTilemap(_cachedMapGenerator.EnemyHomeTilemap);
        RefreshStrategicOverlayForTilemap(_cachedMapGenerator.EnemyTerritoryTilemap);

        foreach (Vector3Int cell in _cachedMapGenerator.AncientRuinsCells)
        {
            UpdateStrategicOverlayTileVisibilityAtCell(cell);
        }
    }

    private void RefreshStrategicOverlayForTilemap(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return;
        }

        foreach (Vector3Int pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(pos))
            {
                UpdateStrategicOverlayTileVisibilityAtCell(pos);
            }
        }
    }

    private void UpdateStrategicOverlayTileVisibilityAtCell(Vector3Int tile)
    {
        if (!IsInitialized)
        {
            return;
        }

        if (_cachedMapGenerator == null)
        {
            _cachedMapGenerator = FindFirstObjectByType<MapGenerator>();
        }

        MapGenerator mapGenerator = _cachedMapGenerator;
        if (mapGenerator == null)
        {
            return;
        }

        bool overlayVisible = CanSeePersistentOverlay(tile);
        Color overlayColor = overlayVisible ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0f);

        Tilemap enemyHome = mapGenerator.EnemyHomeTilemap;
        if (enemyHome != null && enemyHome.HasTile(tile))
        {
            enemyHome.SetColor(tile, overlayColor);
            enemyHome.RefreshTile(tile);
        }

        Tilemap enemyTerritory = mapGenerator.EnemyTerritoryTilemap;
        if (enemyTerritory != null && enemyTerritory.HasTile(tile))
        {
            enemyTerritory.SetColor(tile, overlayColor);
            enemyTerritory.RefreshTile(tile);
        }

        Tilemap decoration = mapGenerator.DecorationTilemap;
        if (decoration != null && mapGenerator.IsAncientRuinsCell(tile) && decoration.HasTile(tile))
        {
            Color ruinsColor = new Color(1f, 1f, 1f, 1f);
            decoration.SetColor(tile, ruinsColor);
            decoration.RefreshTile(tile);
        }
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

        RefreshAllStrategicOverlayTileVisibility();
    }

    private void UpdateAllVisibilityControllers()
    {
        _visualUpdater?.UpdateAllVisibilityControllers();
    }

    public void RefreshFogOfWar()
    {
        if (!IsInitialized)
        {
            // Debug.Log("[FogOfWar] RefreshFogOfWar called but not initialized, calling InitializeFogOfWar");
            return;
        }

        if (_tileVisibility.Count == 0)
        {
            // Debug.Log("[FogOfWar] RefreshFogOfWar found empty visibility, calling InitializeFogOfWar");
            InitializeFogOfWar();
        }

        // Debug.Log("[FogOfWar] RefreshFogOfWar starting provider updates and visibility coroutine");

        foreach (IVisionProvider provider in _visionProviders)
        {
            if (provider == null) continue;

            if (provider is VisionProvider visionProvider)
            {
                visionProvider.ForceUpdateAffectedTiles();
            }
        }

        StartCoroutine(UpdateVisibilityCoroutine());

        if (MapObjectSpawner.Instance != null)
        {
            MapObjectSpawner.Instance.UpdateResourceTileVisibility();
        }

        RefreshAllStrategicOverlayTileVisibility();
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
                catch (Exception)
                {
                    // Debug.LogWarning($"[FogOfWarManager] Error notifying VisibilityController: {e.Message}");
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
