using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Systems.Jobs;

public class FogOfWarInitializer
{
    private readonly FogOfWarManager _manager;
    private readonly Grid _grid;
    private Tilemap _fogTilemap;
    private TileBase _fogTile;
    private Tilemap _groundTilemap;
    private MapGenerator _cachedMapGenerator;
    private Tilemap _cachedGroundTilemap;
    private Tilemap _cachedWallTilemap;
    private Color _invisibleColor;
    private static readonly WaitForSeconds _wait05 = CoroutineCache.GetWaitForSeconds(0.5f);
    
    public FogOfWarInitializer(FogOfWarManager manager, Grid grid, Tilemap fogTilemap, TileBase fogTile, 
        Tilemap groundTilemap, MapGenerator mapGenerator, Color invisibleColor)
    {
        _manager = manager;
        _grid = grid;
        _fogTilemap = fogTilemap;
        _fogTile = fogTile;
        _groundTilemap = groundTilemap;
        _cachedMapGenerator = mapGenerator;
        _invisibleColor = invisibleColor;
    }
    
    public void SetReferences(Tilemap fogTilemap, Tilemap groundTilemap, MapGenerator mapGenerator)
    {
        _fogTilemap = fogTilemap;
        _groundTilemap = groundTilemap;
        _cachedMapGenerator = mapGenerator;
    }
    
    public IEnumerator InitializeFogOfWarAsync(Dictionary<Vector3Int, FogOfWarState> tileVisibility, IInitializationProgress progress = null)
    {
        if (progress != null)
        {
            progress.UpdateProgress(0.0f, "대기 농도 및 가시거리 분석 중...");
            yield return _wait05;
        }
        
        List<Tilemap> terrainTilemaps = CollectTerrainTilemaps();
        
        if (terrainTilemaps.Count == 0 || _fogTilemap == null || _fogTile == null)
        {
            yield break;
        }
        
        HashSet<Vector3Int> allTilePositions = CollectAllTilePositions(terrainTilemaps, tileVisibility);
        
        // Yield to avoid blocking
        yield return null;
        
        if (allTilePositions.Count > 0)
        {
            TilemapRenderer renderer = _fogTilemap.GetComponent<TilemapRenderer>();
            bool wasEnabled = renderer != null && renderer.enabled;
            
            if (renderer != null)
            {
                renderer.enabled = false;
            }
            
            // Break up tile placement into chunks to avoid blocking
            yield return PlaceFogTilesAsync(allTilePositions, progress);
            
            if (renderer != null)
            {
                renderer.enabled = wasEnabled;
            }
        }
        
        _fogTilemap.CompressBounds();
    }
    
    private MonoBehaviour _coroutineRunner;
    
    public void SetCoroutineRunner(MonoBehaviour runner)
    {
        _coroutineRunner = runner;
    }
    
    private Coroutine StartCoroutineOnRunner(IEnumerator coroutine)
    {
        if (_coroutineRunner == null && _manager != null)
        {
            _coroutineRunner = _manager;
        }
        
        if (_coroutineRunner != null)
        {
            return _coroutineRunner.StartCoroutine(coroutine);
        }
        
        return null;
    }
    
    private IEnumerator PlaceFogTilesAsync(HashSet<Vector3Int> allTilePositions, IInitializationProgress progress = null)
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
        
        int totalPositions = fogBounds.size.x * fogBounds.size.y;
        int processedPositions = 0;
        const int positionsPerFrame = 1000; // Process 1000 positions per frame
        
        foreach (Vector3Int pos in fogBounds.allPositionsWithin)
        {
            int index = (pos.x - minX) + (pos.y - minY) * fogBounds.size.x;
            if (allTilePositions.Contains(pos))
            {
                fogTiles[index] = _fogTile;
            }
            else
            {
                fogTiles[index] = null;
            }
            
            processedPositions++;
            
            if (processedPositions % positionsPerFrame == 0)
            {
                yield return null;
            }
        }
        
        _fogTilemap.SetTilesBlock(fogBounds, fogTiles);
        
        // Batch set colors in chunks
        List<Vector3Int> positionsList = new List<Vector3Int>(allTilePositions);
        const int colorsPerFrame = 500;
        
        for (int i = 0; i < positionsList.Count; i += colorsPerFrame)
        {
            int endIndex = Mathf.Min(i + colorsPerFrame, positionsList.Count);
            
            for (int j = i; j < endIndex; j++)
            {
                _fogTilemap.SetColor(positionsList[j], _invisibleColor);
            }
            
            if (i % (colorsPerFrame * 5) == 0)
            {
                yield return null;
            }
        }
        
        // Refresh tiles in chunks
        for (int i = 0; i < positionsList.Count; i += colorsPerFrame)
        {
            int endIndex = Mathf.Min(i + colorsPerFrame, positionsList.Count);
            
            for (int j = i; j < endIndex; j++)
            {
                _fogTilemap.RefreshTile(positionsList[j]);
            }
            
            if (i % (colorsPerFrame * 5) == 0)
            {
                yield return null;
            }
        }
    }
    
    public void InitializeFogOfWar(Dictionary<Vector3Int, FogOfWarState> tileVisibility)
    {
        List<Tilemap> terrainTilemaps = CollectTerrainTilemaps();
        
        if (terrainTilemaps.Count == 0 || _fogTilemap == null || _fogTile == null)
        {
            return;
        }
        
        HashSet<Vector3Int> allTilePositions = CollectAllTilePositions(terrainTilemaps, tileVisibility);
        
        if (allTilePositions.Count > 0)
        {
            TilemapRenderer renderer = _fogTilemap.GetComponent<TilemapRenderer>();
            bool wasEnabled = renderer != null && renderer.enabled;
            
            if (renderer != null)
            {
                renderer.enabled = false;
            }
            
            PlaceFogTiles(allTilePositions);
            
            if (renderer != null)
            {
                renderer.enabled = wasEnabled;
            }
        }
        
        _fogTilemap.CompressBounds();
    }
    
    public void CreateFogTilemap()
    {
        if (_grid == null)
        {
            Debug.LogWarning("[FogOfWarManager] Cannot create fog tilemap: Grid is null");
            return;
        }
        
        GameObject fogTilemapObj = GameObject.Find("Fog Tilemap");
        if (fogTilemapObj == null)
        {
            fogTilemapObj = new GameObject("Fog Tilemap");
            fogTilemapObj.transform.SetParent(_grid.transform);
        }
        
        _fogTilemap = fogTilemapObj.GetComponent<Tilemap>();
        if (_fogTilemap == null)
        {
            _fogTilemap = fogTilemapObj.AddComponent<Tilemap>();
            fogTilemapObj.AddComponent<TilemapRenderer>();
        }
        
        TilemapRenderer tr = _fogTilemap.GetComponent<TilemapRenderer>();
        if (tr != null)
        {
            tr.sortingOrder = 100;
            tr.mode = TilemapRenderer.Mode.Individual;
        }
        
        _fogTilemap.color = Color.white;
        _fogTilemap.gameObject.SetActive(true);
    }
    
    public Tilemap GetFogTilemap()
    {
        return _fogTilemap;
    }
    
    private List<Tilemap> CollectTerrainTilemaps()
    {
        List<Tilemap> terrainTilemaps = new List<Tilemap>();
        
        if (_groundTilemap == null && BuildingManager.Instance != null)
        {
            _groundTilemap = BuildingManager.Instance.GroundTilemap;
        }
        
        if (_groundTilemap != null)
        {
            terrainTilemaps.Add(_groundTilemap);
        }
        
        if (_cachedMapGenerator != null)
        {
            if (_cachedMapGenerator.GroundTilemap != null)
            {
                Tilemap mapGenGroundTilemap = _cachedMapGenerator.GroundTilemap;
                if (!terrainTilemaps.Contains(mapGenGroundTilemap))
                {
                    terrainTilemaps.Add(mapGenGroundTilemap);
                }
                _cachedGroundTilemap = mapGenGroundTilemap;
            }
            
            if (_cachedMapGenerator.WallTilemap != null)
            {
                Tilemap mapGenWallTilemap = _cachedMapGenerator.WallTilemap;
                if (!terrainTilemaps.Contains(mapGenWallTilemap))
                {
                    terrainTilemaps.Add(mapGenWallTilemap);
                }
                _cachedWallTilemap = mapGenWallTilemap;
            }
        }
        
        if (terrainTilemaps.Count == 0)
        {
            Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
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
        
        return terrainTilemaps;
    }
    
    private HashSet<Vector3Int> CollectAllTilePositions(List<Tilemap> terrainTilemaps, Dictionary<Vector3Int, FogOfWarState> tileVisibility)
    {
        HashSet<Vector3Int> allTilePositions = new HashSet<Vector3Int>();
        
        foreach (Tilemap terrainTilemap in terrainTilemaps)
        {
            BoundsInt bounds = terrainTilemap.cellBounds;
            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                if (!terrainTilemap.HasTile(pos))
                {
                    continue;
                }

                allTilePositions.Add(pos);
                tileVisibility[pos] = FogOfWarState.Invisible;
            }
        }
        
        return allTilePositions;
    }
    
    private void PlaceFogTiles(HashSet<Vector3Int> allTilePositions)
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
                fogTiles[index] = _fogTile;
            }
            else
            {
                fogTiles[index] = null;
            }
        }
        
        _fogTilemap.SetTilesBlock(fogBounds, fogTiles);
        
        BatchSetFogColors(allTilePositions);
    }
    
    private void BatchSetFogColors(HashSet<Vector3Int> allTilePositions)
    {
        foreach (Vector3Int pos in allTilePositions)
        {
            _fogTilemap.SetColor(pos, _invisibleColor);
        }
        
        List<Vector3Int> positionsToRefresh = new List<Vector3Int>(allTilePositions);
        
        foreach (Vector3Int pos in positionsToRefresh)
        {
            _fogTilemap.RefreshTile(pos);
        }
    }
}

