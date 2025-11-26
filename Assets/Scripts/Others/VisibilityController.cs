using UnityEngine;

/// <summary>
/// Component that controls the visibility of game objects based on fog of war state.
/// Attach this to enemies, resources, or other objects that should be hidden by fog of war.
/// </summary>
public class VisibilityController : MonoBehaviour
{
    [Header("Visibility Settings")]
    [SerializeField] private VisibilityType visibilityType = VisibilityType.Enemy;
    [SerializeField] private bool hideWhenInvisible = true;
    
    public enum VisibilityType
    {
        Enemy,      // Only visible when fully visible
        Resource,   // Visible when partly or fully visible
        Building,   // Always visible (buildings are player's own)
        Terrain     // Always visible (terrain is always shown)
    }
    
    private SpriteRenderer[] _spriteRenderers;
    private Canvas[] _canvases;
    private bool _wasVisible = true;
    
    private void Awake()
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        _canvases = GetComponentsInChildren<Canvas>();
    }
    
    private void OnEnable()
    {
        if (FogOfWarManager.Instance != null)
        {
            FogOfWarManager.OnVisibilityChanged += OnVisibilityChanged;
        }
        
        // Initial update
        UpdateVisibility();
    }
    
    private void OnDisable()
    {
        FogOfWarManager.OnVisibilityChanged -= OnVisibilityChanged;
    }
    
    private void Update()
    {
        // Update visibility periodically (in case object moves)
        UpdateVisibility();
    }
    
    private void OnVisibilityChanged(Vector3Int cell, FogOfWarState state)
    {
        // Check if this change affects us
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
        if (FogOfWarManager.Instance == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null)
        {
            return Vector3Int.zero;
        }
        
        return BuildingManager.Instance.grid.WorldToCell(transform.position);
    }
    
    private void UpdateVisibility()
    {
        if (FogOfWarManager.Instance == null)
        {
            SetVisible(true);
            return;
        }
        
        Vector3Int cell = GetCurrentCell();
        bool shouldBeVisible = false;
        
        switch (visibilityType)
        {
            case VisibilityType.Enemy:
                shouldBeVisible = FogOfWarManager.Instance.CanSeeEnemies(cell);
                break;
                
            case VisibilityType.Resource:
                shouldBeVisible = FogOfWarManager.Instance.CanSeeResources(cell);
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
    
    private void SetVisible(bool visible)
    {
        // Control sprite renderers
        foreach (var sr in _spriteRenderers)
        {
            if (sr != null)
            {
                sr.enabled = visible;
            }
        }
        
        // Control canvases (for UI elements)
        foreach (var canvas in _canvases)
        {
            if (canvas != null)
            {
                canvas.enabled = visible;
            }
        }
        
        // Also control colliders if needed (optional)
        // Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        // foreach (var col in colliders)
        // {
        //     if (col != null) col.enabled = visible;
        // }
    }
}

