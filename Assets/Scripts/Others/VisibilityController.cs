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
    
    private void Awake()
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        _canvases = GetComponentsInChildren<Canvas>();
    }
    
    private void OnEnable()
    {
        // For resources, defer event subscription during spawning to prevent GC allocations
        if (visibilityType == VisibilityType.Resource && ProceduralResourceSpawner.IsSpawningResources)
        {
            // Don't subscribe yet - will be done in batch after spawning
            _isSubscribed = false;
            // Set initial visibility without checking fog (will be updated after spawning)
            SetVisible(true);
            return;
        }
        
        // For non-resources or when not spawning, subscribe immediately
        SubscribeToEvent();
        UpdateVisibility();
    }
    
    private void OnDisable()
    {
        UnsubscribeFromEvent();
    }
    
    private void SubscribeToEvent()
    {
        if (!_isSubscribed && FogOfWarManager.Instance != null)
        {
            FogOfWarManager.OnVisibilityChanged += OnVisibilityChanged;
            _isSubscribed = true;
        }
    }
    
    private void UnsubscribeFromEvent()
    {
        if (_isSubscribed && FogOfWarManager.Instance != null)
        {
            FogOfWarManager.OnVisibilityChanged -= OnVisibilityChanged;
            _isSubscribed = false;
        }
    }
    
    public void SubscribeIfNeeded()
    {
        if (!_isSubscribed)
        {
            SubscribeToEvent();
            UpdateVisibility();
        }
    }
    
    private void Update()
    {
        // Skip visibility updates during resource spawning to prevent performance issues
        if (visibilityType == VisibilityType.Resource && ProceduralResourceSpawner.IsSpawningResources)
        {
            return;
        }
        
        UpdateVisibility();
    }
    
    private void OnVisibilityChanged(Vector3Int cell, FogOfWarState state)
    {
        // Skip visibility updates during resource spawning to prevent performance issues
        if (visibilityType == VisibilityType.Resource && ProceduralResourceSpawner.IsSpawningResources)
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
        if (transform == null)
        {
            return Vector3Int.zero;
        }
        
        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null)
        {
            return Vector3Int.zero;
        }
        
        try
        {
            return BuildingManager.Instance.grid.WorldToCell(transform.position);
        }
        catch (System.Exception)
        {
            // Handle missing reference gracefully
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
                    if (ProceduralResourceSpawner.ResourcesAlwaysVisible)
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
        catch (System.Exception e)
        {
            // Handle missing reference gracefully - just make it visible
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

