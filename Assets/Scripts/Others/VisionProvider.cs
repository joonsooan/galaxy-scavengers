using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component that can be added to buildings and units to provide vision in the fog of war system.
/// Uses a circle collider to detect which tiles the vision range actually touches.
/// </summary>
public class VisionProvider : MonoBehaviour, IVisionProvider
{
    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 5f;
    [SerializeField] private bool isActive = true;
    
    public float VisionRange => visionRange;
    public bool IsActive => isActive;
    
    private bool _isRegistered = false;
    private CircleCollider2D _visionCollider;
    private Grid _grid;
    private HashSet<Vector3Int> _currentAffectedTiles = new HashSet<Vector3Int>();
    private Vector3 _lastPosition;
    private float _lastVisionRange;
    
    private void Awake()
    {
        // Get or create circle collider for vision detection
        _visionCollider = GetComponent<CircleCollider2D>();
        if (_visionCollider == null)
        {
            _visionCollider = gameObject.AddComponent<CircleCollider2D>();
        }
        
        // Configure collider as trigger
        _visionCollider.isTrigger = true;
        _visionCollider.radius = visionRange;
        
        // Get grid reference
        if (BuildingManager.Instance != null)
        {
            _grid = BuildingManager.Instance.grid;
        }
        if (_grid == null)
        {
            _grid = FindFirstObjectByType<Grid>();
        }
    }
    
    private void OnEnable()
    {
        RegisterWithFogOfWar();
        UpdateColliderRadius();
        _lastPosition = transform.position;
        _lastVisionRange = visionRange;
    }
    
    private void Start()
    {
        // Try to register again in Start in case FogOfWarManager wasn't ready in OnEnable
        RegisterWithFogOfWar();
        
        // Initial update of affected tiles (after registration)
        if (_grid != null && FogOfWarManager.Instance != null && _isRegistered)
        {
            // Clear current tiles first, then calculate initial set
            // This ensures all initial tiles are treated as "entered"
            _currentAffectedTiles.Clear();
            UpdateAffectedTiles();
        }
    }
    
    private void Update()
    {
        if (_grid == null || FogOfWarManager.Instance == null) return;
        
        // Check if position or range changed
        bool positionChanged = Vector3.Distance(transform.position, _lastPosition) > 0.01f;
        bool rangeChanged = Mathf.Abs(visionRange - _lastVisionRange) > 0.01f;
        
        if (positionChanged || rangeChanged)
        {
            if (rangeChanged)
            {
                UpdateColliderRadius();
            }
            
            // Update which tiles are affected by this vision provider
            UpdateAffectedTiles();
            
            _lastPosition = transform.position;
            _lastVisionRange = visionRange;
        }
    }
    
    private void UpdateColliderRadius()
    {
        if (_visionCollider != null)
        {
            _visionCollider.radius = visionRange;
        }
    }
    
    private void UpdateAffectedTiles()
    {
        if (_grid == null || FogOfWarManager.Instance == null || !_isRegistered) return;
        
        // Calculate which tiles the collider currently overlaps
        HashSet<Vector3Int> newAffectedTiles = GetTilesInCollider();
        
        // Find tiles that entered (new - old)
        HashSet<Vector3Int> enteredTiles = new HashSet<Vector3Int>(newAffectedTiles);
        enteredTiles.ExceptWith(_currentAffectedTiles);
        
        // Find tiles that exited (old - new)
        HashSet<Vector3Int> exitedTiles = new HashSet<Vector3Int>(_currentAffectedTiles);
        exitedTiles.ExceptWith(newAffectedTiles);
        
        // Update FogOfWarManager with only the changed tiles
        // (If this is the first update, all tiles will be "entered")
        if (enteredTiles.Count > 0 || exitedTiles.Count > 0)
        {
            FogOfWarManager.Instance.OnVisionProviderTilesChanged(this, enteredTiles, exitedTiles);
        }
        
        // Update current affected tiles
        _currentAffectedTiles = newAffectedTiles;
    }
    
    private HashSet<Vector3Int> GetTilesInCollider()
    {
        HashSet<Vector3Int> tiles = new HashSet<Vector3Int>();
        
        if (_visionCollider == null || _grid == null) return tiles;
        
        // Get collider bounds
        Bounds bounds = _visionCollider.bounds;
        Vector3 center = bounds.center;
        float radius = bounds.extents.x; // Use x extent as radius (circle collider)
        
        // Calculate which tiles are within the circle
        int rangeInCells = Mathf.CeilToInt(radius);
        Vector3Int centerCell = _grid.WorldToCell(center);
        
        for (int x = -rangeInCells; x <= rangeInCells; x++)
        {
            for (int y = -rangeInCells; y <= rangeInCells; y++)
            {
                Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                Vector3 cellWorldPos = _grid.GetCellCenterWorld(cell);
                float distance = Vector3.Distance(center, cellWorldPos);
                
                // Check if tile center is within vision range
                if (distance <= radius)
                {
                    tiles.Add(cell);
                }
            }
        }
        
        return tiles;
    }
    
    private void OnDisable()
    {
        UnregisterFromFogOfWar();
    }
    
    private void RegisterWithFogOfWar()
    {
        if (!_isRegistered && FogOfWarManager.Instance != null)
        {
            FogOfWarManager.Instance.RegisterVisionProvider(this);
            _isRegistered = true;
        }
    }
    
    private void UnregisterFromFogOfWar()
    {
        if (_isRegistered && FogOfWarManager.Instance != null)
        {
            FogOfWarManager.Instance.UnregisterVisionProvider(this);
            _isRegistered = false;
        }
    }
    
    // Called when FogOfWarManager becomes available
    public void OnFogOfWarManagerReady()
    {
        RegisterWithFogOfWar();
    }
    
    public Vector3 GetPosition()
    {
        return transform.position;
    }
    
    public float GetVisionRange()
    {
        return visionRange;
    }
    
    public bool CheckIsActive()
    {
        return isActive && gameObject.activeInHierarchy;
    }
    
    // Allow runtime changes
    public void SetVisionRange(float range)
    {
        visionRange = range;
        UpdateColliderRadius();
        
        // Update affected tiles immediately
        if (_grid != null && FogOfWarManager.Instance != null)
        {
            UpdateAffectedTiles();
        }
    }
    
    public void SetActive(bool active)
    {
        isActive = active;
    }
    
    // Visualize vision range in editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, visionRange);
    }
}

