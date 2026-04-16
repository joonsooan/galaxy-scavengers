using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ResourceTileManager
{
    private readonly Tilemap _resourceTilemap;
    private readonly Dictionary<Vector3Int, ResourceType> _resourceTilePositions;
    private readonly List<ResourceSpawnSettings> _resourceSettings;
    
    public ResourceTileManager(Tilemap resourceTilemap, Dictionary<Vector3Int, ResourceType> resourceTilePositions, 
        List<ResourceSpawnSettings> resourceSettings)
    {
        _resourceTilemap = resourceTilemap;
        _resourceTilePositions = resourceTilePositions;
        _resourceSettings = resourceSettings;
    }
    
    public void InitializeRuleTiles()
    {
        if (_resourceTilemap == null)
        {
            Debug.LogWarning("[MapObjectSpawner] Resource tilemap not assigned. Cannot initialize rule tiles.");
            return;
        }
        
        if (_resourceTilePositions.Count == 0) return;
        
        TilemapRenderer renderer = _resourceTilemap.GetComponent<TilemapRenderer>();
        bool wasEnabled = renderer != null && renderer.enabled;
        
        if (renderer != null)
        {
            renderer.enabled = false;
        }
        
        if (_resourceTilemap.gameObject != null)
        {
            _resourceTilemap.gameObject.SetActive(true);
        }
        
        BatchSetRuleTiles();
        
        if (renderer != null)
        {
            renderer.enabled = wasEnabled;
        }
        
        if (FogOfWarManager.Instance != null && FogOfWarManager.Instance.IsInitialized)
        {
            BatchUpdateResourceTileVisibility();
        }
    }
    
    private void BatchSetRuleTiles()
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        
        foreach (var pos in _resourceTilePositions.Keys)
        {
            minX = Mathf.Min(minX, pos.x);
            minY = Mathf.Min(minY, pos.y);
            maxX = Mathf.Max(maxX, pos.x);
            maxY = Mathf.Max(maxY, pos.y);
        }
        
        BoundsInt bounds = new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        TileBase[] tiles = new TileBase[bounds.size.x * bounds.size.y];
        
        foreach (var kvp in _resourceTilePositions)
        {
            Vector3Int cellPos = kvp.Key;
            ResourceType resourceType = kvp.Value;
            
            ResourceSpawnSettings settings = _resourceSettings.Find(s => s.resourceType == resourceType);
            if (settings != null && settings.resourceRuleTile != null)
            {
                int index = (cellPos.x - minX) + (cellPos.y - minY) * bounds.size.x;
                tiles[index] = settings.resourceRuleTile;
            }
        }
        
        _resourceTilemap.SetTilesBlock(bounds, tiles);
        
        List<Vector3Int> positionsToRefresh = new List<Vector3Int>(_resourceTilePositions.Keys);
        
        foreach (Vector3Int pos in positionsToRefresh)
        {
            _resourceTilemap.RefreshTile(pos);
        }
    }
    
    public void UpdateResourceTileVisibility()
    {
        BatchUpdateResourceTileVisibility();
    }
    
    private void BatchUpdateResourceTileVisibility()
    {
        if (_resourceTilemap == null) return;
        
        if (_resourceTilePositions.Count == 0) return;
        
        bool fogInitialized = FogOfWarManager.Instance != null && FogOfWarManager.Instance.IsInitialized;
        Color visibleColor = new Color(1f, 1f, 1f, 1f);
        Color hiddenColor = new Color(1f, 1f, 1f, 0f);
        
        List<Vector3Int> positionsToRefresh = new List<Vector3Int>(_resourceTilePositions.Count);
        
        foreach (var kvp in _resourceTilePositions)
        {
            Vector3Int cellPos = kvp.Key;
            if (!_resourceTilemap.HasTile(cellPos)) continue;
            
            bool isVisible = true;
            if (fogInitialized)
            {
                isVisible = FogOfWarManager.Instance.CanSeeResources(cellPos);
            }
            
            Color tileColor = isVisible ? visibleColor : hiddenColor;
            _resourceTilemap.SetColor(cellPos, tileColor);
            positionsToRefresh.Add(cellPos);
        }
        
        foreach (Vector3Int pos in positionsToRefresh)
        {
            _resourceTilemap.RefreshTile(pos);
        }
    }
    
    public void UpdateResourceTileVisibilityAtCell(Vector3Int cellPos)
    {
        if (_resourceTilemap == null || !_resourceTilemap.HasTile(cellPos)) return;
        
        bool wasVisible = _resourceTilemap.GetColor(cellPos).a > 0.5f;
        bool isVisible = true;
        if (FogOfWarManager.Instance != null && FogOfWarManager.Instance.IsInitialized)
        {
            isVisible = FogOfWarManager.Instance.CanSeeResources(cellPos);
        }
        
        Color tileColor = isVisible ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0f);
        _resourceTilemap.SetColor(cellPos, tileColor);
        _resourceTilemap.RefreshTile(cellPos);
    }
    
    public void UpdateNearbyRuleTiles(Vector3Int minedPosition)
    {
        if (_resourceTilemap == null) return;
        
        _resourceTilePositions.Remove(minedPosition);
        _resourceTilemap.SetTile(minedPosition, null);
        
        HashSet<Vector3Int> tilesToRefresh = new HashSet<Vector3Int>();
        
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                
                Vector3Int neighborPos = minedPosition + new Vector3Int(x, y, 0);
                
                if (_resourceTilePositions.ContainsKey(neighborPos))
                {
                    tilesToRefresh.Add(neighborPos);
                }
            }
        }
        
        RefreshRuleTiles(tilesToRefresh);
    }
    
    private void RefreshRuleTiles(IEnumerable<Vector3Int> positions)
    {
        if (_resourceTilemap == null) return;
        
        List<Vector3Int> positionsToRefresh = new List<Vector3Int>();
        bool fogInitialized = FogOfWarManager.Instance != null && FogOfWarManager.Instance.IsInitialized;
        Color visibleColor = new Color(1f, 1f, 1f, 1f);
        Color hiddenColor = new Color(1f, 1f, 1f, 0f);
        
        foreach (Vector3Int pos in positions)
        {
            if (_resourceTilePositions.ContainsKey(pos) && _resourceTilemap.HasTile(pos))
            {
                bool isVisible = true;
                if (fogInitialized)
                {
                    isVisible = FogOfWarManager.Instance.CanSeeResources(pos);
                }
                
                Color tileColor = isVisible ? visibleColor : hiddenColor;
                _resourceTilemap.SetColor(pos, tileColor);
                positionsToRefresh.Add(pos);
            }
        }
        
        foreach (Vector3Int pos in positionsToRefresh)
        {
            _resourceTilemap.RefreshTile(pos);
        }
    }
}

