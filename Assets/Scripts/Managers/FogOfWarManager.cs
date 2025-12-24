using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
    private Tilemap _cachedGroundTilemap;
    private Tilemap _cachedWallTilemap;
    
    private bool _isInitializing;
    private bool _fogInitialized;
    private bool _respectFog = true;
    
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
        
        TilemapRenderer tr = fogTilemap.GetComponent<TilemapRenderer>();
        if (tr != null)
        {
            tr.sortingOrder = 100;
            tr.mode = TilemapRenderer.Mode.Individual;
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
        
        _cachedMapGenerator = FindFirstObjectByType<MapGenerator>();

        StartCoroutine(DelayedFogInitialization());
        RegisterAllExistingVisionProviders();
        InvokeRepeating(nameof(CleanupNullProviders), 5f, 5f);
    }
    
    private System.Collections.IEnumerator DelayedFogInitialization()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        while (_cachedMapGenerator != null && 
               ((_cachedMapGenerator.GroundTilemap != null && _cachedMapGenerator.GroundTilemap.cellBounds.size.x == 0) ||
                (_cachedMapGenerator.WallTilemap != null && _cachedMapGenerator.WallTilemap.cellBounds.size.x == 0)))
        {
            yield return null;
        }
        
        yield return new WaitForEndOfFrame();
        
        if (_cachedMapGenerator != null)
        {
            _cachedGroundTilemap = _cachedMapGenerator.GroundTilemap;
            _cachedWallTilemap = _cachedMapGenerator.WallTilemap;
        }
        
        InitializeFogOfWar();
        _fogInitialized = true;
        
        UpdateVisibility();
        UpdateAllVisibilityControllers();
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
        
        List<Tilemap> terrainTilemaps = new List<Tilemap>();
        
        if (groundTilemap == null && BuildingManager.Instance != null)
        {
            groundTilemap = BuildingManager.Instance.GroundTilemap;
        }
        
        if (groundTilemap != null)
        {
            terrainTilemaps.Add(groundTilemap);
        }
        
        MapGenerator mapGenerator = _cachedMapGenerator;
        if (mapGenerator != null)
        {
            if (mapGenerator.GroundTilemap != null)
            {
                Tilemap mapGenGroundTilemap = mapGenerator.GroundTilemap;
                if (!terrainTilemaps.Contains(mapGenGroundTilemap))
                {
                    terrainTilemaps.Add(mapGenGroundTilemap);
                }
            }
            
            if (mapGenerator.WallTilemap != null)
            {
                Tilemap mapGenWallTilemap = mapGenerator.WallTilemap;
                if (!terrainTilemaps.Contains(mapGenWallTilemap))
                {
                    terrainTilemaps.Add(mapGenWallTilemap);
                }
            }
            
            if (mapGenerator.WallTilemap != null)
            {
                Tilemap mapGenTilemap = mapGenerator.WallTilemap;
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
            _isInitializing = false;
            return;
        }
        
        if (fogTilemap == null)
        {
            _isInitializing = false;
            return;
        }
        
        if (fogTile == null)
        {
            _isInitializing = false;
            return;
        }
        
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
        
        if (allTilePositions.Count > 0)
        {
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
            
            fogTilemap.SetTilesBlock(fogBounds, fogTiles);
            
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
                UpdateFogVisual(tile, newState);
                
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
                if (!suppressVisibilityEvents)
                {
                    SafeInvokeVisibilityChanged(tile, newState);
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
        
        if (fogTilemap == null) return;
        
        if (groundTilemap == null && BuildingManager.Instance != null)
        {
            groundTilemap = BuildingManager.Instance.GroundTilemap;
        }
        
        bool hasTile = false;
        Tilemap tilemapWithTile = null;
        
        if (groundTilemap != null && groundTilemap.HasTile(cell))
        {
            hasTile = true;
            tilemapWithTile = groundTilemap;
        }
        else if (_cachedMapGenerator != null)
        {
            if (_cachedWallTilemap == null)
            {
                _cachedWallTilemap = _cachedMapGenerator.WallTilemap;
            }
            if (_cachedGroundTilemap == null)
            {
                _cachedGroundTilemap = _cachedMapGenerator.GroundTilemap;
            }
            
            if (_cachedWallTilemap != null && _cachedWallTilemap.HasTile(cell))
            {
                hasTile = true;
                tilemapWithTile = _cachedWallTilemap;
            }
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
        FogOfWarState state = GetVisibilityState(cellPosition);
        return state != FogOfWarState.Invisible;
    }
    
    public void ToggleFogVisibility()
    {
        _respectFog = !_respectFog;
        
        if (fogTilemap != null)
        {
            fogTilemap.gameObject.SetActive(_respectFog);
        }
        
        UpdateAllVisibilityControllers();
    }
    
    private void UpdateAllVisibilityControllers()
    {
        if (grid == null) return;
        
        VisibilityController[] controllers = FindObjectsByType<VisibilityController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var controller in controllers)
        {
            if (controller != null)
            {
                controller.ForceUpdateVisibility();
            }
        }
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
    }
    
    public void ExploreTile(Vector3Int cellPosition)
    {
        if (_exploredTiles.Add(cellPosition))
        {
            if (!_tileVisibility.ContainsKey(cellPosition) || 
                _tileVisibility[cellPosition] == FogOfWarState.Invisible)
            {
                _tileVisibility[cellPosition] = FogOfWarState.PartlyVisible;
                UpdateFogVisual(cellPosition, FogOfWarState.PartlyVisible);
                
                if (!suppressVisibilityEvents)
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