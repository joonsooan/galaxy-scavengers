using UnityEngine;

public class VisibilityController : MonoBehaviour
{
    [Header("Visibility Settings")]
    [SerializeField] private VisibilityType visibilityType = VisibilityType.Enemy;

    private enum VisibilityType
    {
        Enemy,
        Resource,
        Building,
        Terrain   
    }
    
    private SpriteRenderer[] _spriteRenderers;
    private Canvas[] _canvases;
    private bool _wasVisible = true;
    private bool _isSubscribed;
    private Vector3Int _lastRegisteredCell = new (int.MaxValue, int.MaxValue, int.MaxValue);
    
    private void Awake()
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        _canvases = GetComponentsInChildren<Canvas>();
    }
    
    private void OnEnable()
    {
        if (visibilityType == VisibilityType.Resource && MapObjectSpawner.IsSpawningResources)
        {
            _isSubscribed = false;
            _wasVisible = false;
            SetVisible(false); 
            return;
        }
        
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
        if (_isSubscribed) _isSubscribed = false;
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
            if (_lastRegisteredCell.x != int.MaxValue)
            {
                FogOfWarManager.Instance.UnregisterVisibilityController(this, _lastRegisteredCell);
            }
            
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
    
    public void OnVisibilityChangedDirect(Vector3Int cell, FogOfWarState state)
    {
        if (this == null || gameObject == null)
        {
            return;
        }
        
        UpdateVisibility();
    }

    private Vector3Int GetCurrentCell()
    {
        if (this == null || gameObject == null)
        {
            return Vector3Int.zero;
        }
        
        if (transform == null)
        {
            return Vector3Int.zero;
        }
        
        if (BuildingManager.Instance == null)
        {
            return Vector3Int.zero;
        }
        
        try
        {
            if (BuildingManager.Instance.grid == null)
            {
                return Vector3Int.zero;
            }
            
            return BuildingManager.Instance.grid.WorldToCell(transform.position);
        }
        catch (System.Exception)
        {
            return Vector3Int.zero;
        }
    }
    
    private void UpdateVisibility()
    {
        if (this == null || gameObject == null)
        {
            return;
        }
        
        if (GetComponent<ResourceNode>() != null || GetComponentInParent<ResourceNode>() != null)
        {
            SetVisible(false);
            return;
        }
        
        Vector3Int currentCell = GetCurrentCell();
        if (currentCell != _lastRegisteredCell)
        {
            RegisterWithFogOfWar();
        }
        
        if (FogOfWarManager.Instance == null || !FogOfWarManager.Instance.IsInitialized)
        {
            SetVisible(false);
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
                    if (FogOfWarManager.Instance != null)
                    {
                        shouldBeVisible = FogOfWarManager.Instance.CanSeeResources(cell);
                    }
                    break;
                    
                case VisibilityType.Building:
                case VisibilityType.Terrain:
                    shouldBeVisible = true;
                    break;
            }
            
            if (_wasVisible != shouldBeVisible)
            {
                SetVisible(shouldBeVisible);
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
        if (GetComponent<ResourceNode>() != null || GetComponentInParent<ResourceNode>() != null)
        {
            visible = false;
        }
        
        _wasVisible = visible;
        
        if (_spriteRenderers != null)
        {
            foreach (var sr in _spriteRenderers)
            {
                if (sr != null)
                {
                    sr.enabled = visible;
                }
            }
        }
        
        if (_canvases != null)
        {
            foreach (var canvas in _canvases)
            {
                if (canvas != null)
                {
                    canvas.enabled = visible;
                }
            }
        }
    }
}

