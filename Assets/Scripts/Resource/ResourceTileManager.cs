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
        
        if (_resourceTilemap.gameObject != null)
        {
            _resourceTilemap.gameObject.SetActive(true);
        }
        
        foreach (var kvp in _resourceTilePositions)
        {
            Vector3Int cellPos = kvp.Key;
            ResourceType resourceType = kvp.Value;
            
            ResourceSpawnSettings settings = _resourceSettings.Find(s => s.resourceType == resourceType);
            if (settings != null && settings.resourceRuleTile != null)
            {
                _resourceTilemap.SetTile(cellPos, settings.resourceRuleTile);
            }
        }
        
        RefreshRuleTiles(_resourceTilePositions.Keys);
        
        if (FogOfWarManager.Instance != null && FogOfWarManager.Instance.IsInitialized)
        {
            UpdateResourceTileVisibility();
        }
    }
    
    public void UpdateResourceTileVisibility()
    {
        if (_resourceTilemap == null) return;
        
        if (_resourceTilemap.gameObject != null && !_resourceTilemap.gameObject.activeSelf)
        {
            _resourceTilemap.gameObject.SetActive(true);
        }
        
        foreach (var kvp in _resourceTilePositions)
        {
            Vector3Int cellPos = kvp.Key;
            UpdateResourceTileVisibilityAtCell(cellPos);
        }
    }
    
    public void UpdateResourceTileVisibilityAtCell(Vector3Int cellPos)
    {
        if (_resourceTilemap == null || !_resourceTilemap.HasTile(cellPos)) return;
        
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
        
        foreach (Vector3Int pos in positions)
        {
            if (_resourceTilePositions.ContainsKey(pos))
            {
                _resourceTilemap.RefreshTile(pos);
                UpdateResourceTileVisibilityAtCell(pos);
            }
        }
    }
}

