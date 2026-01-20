using System.Collections.Generic;
using UnityEngine;

public class VisionProvider : MonoBehaviour, IVisionProvider
{
    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 5f;
    [SerializeField] private bool isActive = true;
    [SerializeField] private bool hasOffest;
    [SerializeField] private Vector3 offest = new (0.5f, 0.5f, 0f);
    
    private bool _isRegistered;
    private CircleCollider2D _visionCollider;
    private Grid _grid;
    private HashSet<Vector3Int> _currentAffectedTiles = new ();
    private Vector3 _lastPosition;
    private float _lastVisionRange;
    
    private void Awake()
    {
        _visionCollider = GetComponent<CircleCollider2D>();
        if (_visionCollider == null)
        {
            _visionCollider = gameObject.AddComponent<CircleCollider2D>();
        }
        
        _visionCollider.isTrigger = true;
        _visionCollider.radius = visionRange;
        
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
        RegisterWithFogOfWar();
        
        if (_grid != null && FogOfWarManager.Instance != null && _isRegistered)
        {
            _currentAffectedTiles.Clear();
            UpdateAffectedTiles();
        }
    }
    
    private void Update()
    {
        if (_grid == null || FogOfWarManager.Instance == null) return;
        
        bool positionChanged = Vector3.Distance(transform.position, _lastPosition) > 0.01f;
        bool rangeChanged = Mathf.Abs(visionRange - _lastVisionRange) > 0.01f;
        
        if (positionChanged || rangeChanged)
        {
            if (rangeChanged)
            {
                UpdateColliderRadius();
            }
            
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
        
        HashSet<Vector3Int> newAffectedTiles = GetTilesInCollider();
        HashSet<Vector3Int> enteredTiles = new HashSet<Vector3Int>(newAffectedTiles);
        enteredTiles.ExceptWith(_currentAffectedTiles);
        
        HashSet<Vector3Int> exitedTiles = new HashSet<Vector3Int>(_currentAffectedTiles);
        exitedTiles.ExceptWith(newAffectedTiles);
        
        if (enteredTiles.Count > 0 || exitedTiles.Count > 0)
        {
            FogOfWarManager.Instance.OnVisionProviderTilesChanged(this, enteredTiles, exitedTiles);
        }
        
        _currentAffectedTiles = newAffectedTiles;
    }
    
    private HashSet<Vector3Int> GetTilesInCollider()
    {
        HashSet<Vector3Int> tiles = new HashSet<Vector3Int>();
        
        if (_visionCollider == null || _grid == null) return tiles;
        
        Bounds bounds = _visionCollider.bounds;
        Vector3 center = bounds.center;
        float radius = bounds.extents.x;
        
        int rangeInCells = Mathf.CeilToInt(radius);
        Vector3Int centerCell = _grid.WorldToCell(center);
        
        for (int x = -rangeInCells; x <= rangeInCells; x++)
        {
            for (int y = -rangeInCells; y <= rangeInCells; y++)
            {
                Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                Vector3 cellWorldPos = _grid.GetCellCenterWorld(cell);
                float distance = Vector3.Distance(center, cellWorldPos);
                
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
    
    public void SetVisionRange(float range)
    {
        visionRange = range;
        UpdateColliderRadius();
        
        if (_grid != null && FogOfWarManager.Instance != null)
        {
            UpdateAffectedTiles();
        }
    }
    
    public void SetActive(bool active)
    {
        isActive = active;
    }
    
    public void ForceUpdateAffectedTiles()
    {
        if (_grid != null && FogOfWarManager.Instance != null && _isRegistered)
        {
            UpdateAffectedTiles();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (hasOffest)
        {
            Gizmos.DrawWireSphere(transform.position + offest, visionRange);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, visionRange);
        }
    }
}

