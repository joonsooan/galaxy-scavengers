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
        
        UpdateVisibility();
    }
    
    private void OnDisable()
    {
        FogOfWarManager.OnVisibilityChanged -= OnVisibilityChanged;
    }
    
    private void Update()
    {
        UpdateVisibility();
    }
    
    private void OnVisibilityChanged(Vector3Int cell, FogOfWarState state)
    {
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

