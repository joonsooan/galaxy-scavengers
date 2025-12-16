using UnityEngine;

public class VisibilityController : MonoBehaviour
{
    [Header("Visibility Settings")]
    [SerializeField] private VisibilityType visibilityType = VisibilityType.Enemy;
    
    public enum VisibilityType
    {
        Enemy,
        Resource,
        Building,
        Terrain   
    }
    
    private SpriteRenderer[] _spriteRenderers;
    private Canvas[] _canvases;
    private bool _wasVisible = true;
    private bool _isSubscribed = false;
    private Vector3Int _lastRegisteredCell = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
    
    private void Awake()
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        _canvases = GetComponentsInChildren<Canvas>();
    }
    
    private void OnEnable()
    {
        // For resources, defer event subscription during spawning to prevent GC allocations
        if (visibilityType == VisibilityType.Resource && MapObjectSpawner.IsSpawningResources)
        {
            // Don't subscribe yet - will be done in batch after spawning
            _isSubscribed = false;
            // Set initial visibility without checking fog (will be updated after spawning)
            SetVisible(true);
            return;
        }
        
        // For non-resources or when not spawning, subscribe immediately
        SubscribeToEvent();
        RegisterWithFogOfWar();
        UpdateVisibility();
    }
    
    private void OnDisable()
    {
        UnregisterFromFogOfWar();
        UnsubscribeFromEvent();
    }
    
    private void OnDestroy()
    {
        // Ensure we unsubscribe when object is destroyed
        UnregisterFromFogOfWar();
        UnsubscribeFromEvent();
    }
    
    private void SubscribeToEvent()
    {
        if (!_isSubscribed && FogOfWarManager.Instance != null)
        {
            _isSubscribed = true;
        }
    }
    
    private void UnsubscribeFromEvent()
    {
        if (_isSubscribed)
        {
            try
            {
            }
            catch (System.Exception)
            {
                // Ignore errors during unsubscribe (object might be destroyed)
            }
            finally
            {
                _isSubscribed = false;
            }
        }
    }
    
    public void SubscribeIfNeeded()
    {
        if (!_isSubscribed)
        {
            SubscribeToEvent();
            RegisterWithFogOfWar();
            UpdateVisibility();
        }
    }
    
    private void RegisterWithFogOfWar()
    {
        if (FogOfWarManager.Instance == null) return;
        
        Vector3Int currentCell = GetCurrentCell();
        if (currentCell != _lastRegisteredCell)
        {
            // Unregister from old position if we were registered
            if (_lastRegisteredCell.x != int.MaxValue)
            {
                FogOfWarManager.Instance.UnregisterVisibilityController(this, _lastRegisteredCell);
            }
            
            // Register at new position
            FogOfWarManager.Instance.RegisterVisibilityController(this, currentCell);
            _lastRegisteredCell = currentCell;
        }
    }
    
    private void UnregisterFromFogOfWar()
    {
        if (FogOfWarManager.Instance == null) return;
        
        if (_lastRegisteredCell.x != int.MaxValue)
        {
            FogOfWarManager.Instance.UnregisterVisibilityController(this, _lastRegisteredCell);
            _lastRegisteredCell = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        }
    }
    
    // Called directly by FogOfWarManager when this specific tile's visibility changes
    // This is much more efficient than the global event system
    public void OnVisibilityChangedDirect(Vector3Int cell, FogOfWarState state)
    {
        // Check if this object is still valid before processing
        if (this == null || gameObject == null)
        {
            return;
        }
        
        // Skip visibility updates during resource spawning to prevent performance issues
        if (visibilityType == VisibilityType.Resource && MapObjectSpawner.IsSpawningResources)
        {
            return;
        }
        
        // Since we're registered at this specific tile, we know this notification is for us
        UpdateVisibility();
    }
    
    // Legacy event handler for backward compatibility (should rarely be called now)
    private void OnVisibilityChanged(Vector3Int cell, FogOfWarState state)
    {
        // Check if this object is still valid before processing
        if (this == null || gameObject == null)
        {
            // Object was destroyed, unsubscribe immediately
            UnsubscribeFromEvent();
            return;
        }
        
        // Skip visibility updates during resource spawning to prevent performance issues
        if (visibilityType == VisibilityType.Resource && MapObjectSpawner.IsSpawningResources)
        {
            return;
        }
        
        if (FogOfWarManager.Instance != null)
        {
            Vector3Int ourCell = GetCurrentCell();
            if (ourCell == cell)
            {
                UpdateVisibility();
            }
        }
    }
    
    private Vector3Int GetCurrentCell()
    {
        // Check if object is destroyed using Unity's pattern
        if (this == null || gameObject == null)
        {
            return Vector3Int.zero;
        }
        
        // Check transform using Unity's destroyed object pattern
        if (transform == null)
        {
            return Vector3Int.zero;
        }
        
        // Check BuildingManager and grid
        if (BuildingManager.Instance == null)
        {
            return Vector3Int.zero;
        }
        
        // Check if BuildingManager.Instance is actually destroyed (Unity's pattern)
        try
        {
            // Access a property to check if object is destroyed
            if (BuildingManager.Instance.grid == null)
            {
                return Vector3Int.zero;
            }
            
            return BuildingManager.Instance.grid.WorldToCell(transform.position);
        }
        catch (System.Exception)
        {
            // Handle missing reference gracefully - object was destroyed
            return Vector3Int.zero;
        }
    }
    
    private void UpdateVisibility()
    {
        // Skip if object is being destroyed
        if (this == null || gameObject == null)
        {
            return;
        }
        
        // Update registration if position changed (for moving objects)
        Vector3Int currentCell = GetCurrentCell();
        if (currentCell != _lastRegisteredCell)
        {
            RegisterWithFogOfWar();
        }
        
        if (FogOfWarManager.Instance == null)
        {
            SetVisible(true);
            return;
        }
        
        try
        {
            Vector3Int cell = GetCurrentCell();
            bool shouldBeVisible = false;
            
            switch (visibilityType)
            {
                case VisibilityType.Enemy:
                    if (FogOfWarManager.Instance != null)
                    {
                        shouldBeVisible = FogOfWarManager.Instance.CanSeeEnemies(cell);
                    }
                    break;
                    
                case VisibilityType.Resource:
                    // Check if resources should always be visible (override fog)
                    if (MapObjectSpawner.ResourcesAlwaysVisible)
                    {
                        shouldBeVisible = true;
                    }
                    else if (FogOfWarManager.Instance != null)
                    {
                        shouldBeVisible = FogOfWarManager.Instance.CanSeeResources(cell);
                    }
                    break;
                    
                case VisibilityType.Building:
                case VisibilityType.Terrain:
                    shouldBeVisible = true; // Always visible
                    break;
            }
            
            if (_wasVisible != shouldBeVisible)
            {
                SetVisible(shouldBeVisible);
                _wasVisible = shouldBeVisible;
            }
        }
        catch (System.Exception)
        {
            if (this != null && gameObject != null)
            {
                SetVisible(true);
            }
        }
    }
    
    public void ForceUpdateVisibility()
    {
        UpdateVisibility();
    }
    
    private void SetVisible(bool visible)
    {
        foreach (var sr in _spriteRenderers)
        {
            if (sr != null)
            {
                sr.enabled = visible;
            }
        }
        
        foreach (var canvas in _canvases)
        {
            if (canvas != null)
            {
                canvas.enabled = visible;
            }
        }
    }
}

