using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FogOfWarVisualUpdater
{
    private readonly Tilemap _fogTilemap;
    private readonly TileBase _fogTile;
    private readonly Color _fullyVisibleColor;
    private readonly Color _partlyVisibleColor;
    private readonly Color _invisibleColor;
    private readonly Tilemap _groundTilemap;
    private readonly MapGenerator _cachedMapGenerator;
    private Tilemap _cachedGroundTilemap;
    private Tilemap _cachedWallTilemap;
    
    public FogOfWarVisualUpdater(Tilemap fogTilemap, TileBase fogTile, Color fullyVisibleColor, 
        Color partlyVisibleColor, Color invisibleColor, Tilemap groundTilemap, MapGenerator mapGenerator)
    {
        _fogTilemap = fogTilemap;
        _fogTile = fogTile;
        _fullyVisibleColor = fullyVisibleColor;
        _partlyVisibleColor = partlyVisibleColor;
        _invisibleColor = invisibleColor;
        _groundTilemap = groundTilemap;
        _cachedMapGenerator = mapGenerator;
        
        if (_cachedMapGenerator != null)
        {
            _cachedGroundTilemap = _cachedMapGenerator.GroundTilemap;
            _cachedWallTilemap = _cachedMapGenerator.WallTilemap;
        }
    }
    
    public void UpdateFogVisual(Vector3Int cell, FogOfWarState state)
    {
        if (_fogTilemap == null) return;
        
        Tilemap groundTilemap = GetGroundTilemap();
        if (!HasTile(groundTilemap, cell)) return;
        
        Color fogColor = GetFogColorForState(state);
        bool shouldRemoveFog = ShouldRemoveFog(state, fogColor);
        
        if (shouldRemoveFog)
        {
            if (_fogTilemap.HasTile(cell))
            {
                _fogTilemap.SetTile(cell, null);
            }
            return;
        }
        
        if (_fogTile == null)
        {
            Debug.LogWarning("[FogOfWarManager] Fog tile is not set. Please assign a fog tile in the inspector.");
            return;
        }
        
        bool tileChanged = !_fogTilemap.HasTile(cell) || _fogTilemap.GetTile(cell) != _fogTile;
        if (tileChanged)
        {
            _fogTilemap.SetTile(cell, _fogTile);
        }
        
        Color currentColor = _fogTilemap.GetColor(cell);
        if (currentColor != fogColor || tileChanged)
        {
            _fogTilemap.SetColor(cell, fogColor);
            _fogTilemap.RefreshTile(cell);
        }
    }
    
    public void UpdateAllVisibilityControllers()
    {
        VisibilityController[] controllers = Object.FindObjectsByType<VisibilityController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var controller in controllers)
        {
            if (controller != null)
            {
                controller.ForceUpdateVisibility();
            }
        }
    }
    
    private Tilemap GetGroundTilemap()
    {
        if (_groundTilemap != null) return _groundTilemap;
        if (BuildingManager.Instance != null) return BuildingManager.Instance.GroundTilemap;
        
        if (_cachedMapGenerator != null)
        {
            if (_cachedWallTilemap == null)
            {
                _cachedWallTilemap = _cachedMapGenerator.WallTilemap;
            }
            if (_cachedGroundTilemap == null)
            {
                _cachedGroundTilemap = _cachedMapGenerator.GroundTilemap;
            }
        }
        
        return null;
    }
    
    private bool HasTile(Tilemap groundTilemap, Vector3Int cell)
    {
        if (groundTilemap != null && groundTilemap.HasTile(cell))
        {
            return true;
        }
        
        if (_cachedWallTilemap != null && _cachedWallTilemap.HasTile(cell))
        {
            return true;
        }
        
        if (_cachedGroundTilemap != null && _cachedGroundTilemap.HasTile(cell))
        {
            return true;
        }
        
        return false;
    }
    
    private Color GetFogColorForState(FogOfWarState state)
    {
        switch (state)
        {
            case FogOfWarState.FullyVisible:
                return _fullyVisibleColor;
            case FogOfWarState.PartlyVisible:
                return _partlyVisibleColor;
            case FogOfWarState.Invisible:
            default:
                return _invisibleColor;
        }
    }
    
    private bool ShouldRemoveFog(FogOfWarState state, Color fogColor)
    {
        return state == FogOfWarState.FullyVisible && fogColor.a <= 0.01f;
    }
}

