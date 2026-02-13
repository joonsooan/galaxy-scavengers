using System.Collections.Generic;
using UnityEngine;

public class VisionProvider : MonoBehaviour, IVisionProvider
{
    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 5f;
    [SerializeField] private bool isActive = true;
    [SerializeField] private bool hasOffest;
    [SerializeField] private Vector3 offest = new Vector3(0.5f, 0.5f, 0f);
    private HashSet<Vector3Int> _currentAffectedTiles = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> _tempNewTiles = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> _tempEnteredTiles = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> _tempExitedTiles = new HashSet<Vector3Int>();
    private Grid _grid;
    private MapGenerator _mapGenerator;

    private bool _isRegistered;
    private Vector3 _lastPosition;
    private float _lastVisionRange;
    private CircleCollider2D _visionCollider;
    private float _updateInterval = 0.1f;
    private float _lastUpdateTime;
    private float _staticUpdateInterval = 0.5f;
    private float _dynamicUpdateInterval = 0.1f;
    private float _movementThreshold = 0.01f;
    private Vector3 _previousFramePosition;

    private void Awake()
    {
        _visionCollider = GetComponent<CircleCollider2D>();
        if (_visionCollider == null) {
            _visionCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        _visionCollider.isTrigger = true;
        _visionCollider.radius = visionRange;

        if (BuildingManager.Instance != null) {
            _grid = BuildingManager.Instance.grid;
        }
        if (_grid == null) {
            _grid = FindFirstObjectByType<Grid>();
        }

        _mapGenerator = FindFirstObjectByType<MapGenerator>();
    }

    private void Start()
    {
        RegisterWithFogOfWar();

        if (_grid != null && FogOfWarManager.Instance != null && FogOfWarManager.Instance.IsInitialized) {
            UpdateAffectedTiles();
        }
    }

    private void Update()
    {
        if (_grid == null || FogOfWarManager.Instance == null) return;

        float currentTime = Time.time;
        bool isMoving = Vector3.SqrMagnitude(transform.position - _previousFramePosition) > _movementThreshold * _movementThreshold;
        _updateInterval = isMoving ? _dynamicUpdateInterval : _staticUpdateInterval;
        _previousFramePosition = transform.position;

        if (currentTime - _lastUpdateTime < _updateInterval) return;

        bool positionChanged = Vector3.SqrMagnitude(transform.position - _lastPosition) > 0.1f;
        bool rangeChanged = Mathf.Abs(visionRange - _lastVisionRange) > 0.01f;

        if (positionChanged || rangeChanged) {
            if (rangeChanged) {
                UpdateColliderRadius();
            }

            UpdateAffectedTiles();
            _lastUpdateTime = currentTime;

            _lastPosition = transform.position;
            _lastVisionRange = visionRange;
        }
    }

    private void OnEnable()
    {
        RegisterWithFogOfWar();
        UpdateColliderRadius();
        _lastPosition = transform.position;
        _lastVisionRange = visionRange;
        _previousFramePosition = transform.position;
    }

    private void OnDisable()
    {
        UnregisterFromFogOfWar();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (hasOffest) {
            Gizmos.DrawWireSphere(transform.position + offest, visionRange);
        }
        else {
            Gizmos.DrawWireSphere(transform.position, visionRange);
        }
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

    private void UpdateColliderRadius()
    {
        if (_visionCollider != null) {
            _visionCollider.radius = visionRange;
        }
    }

    private void UpdateAffectedTiles()
    {
        if (_grid == null || FogOfWarManager.Instance == null || !_isRegistered) return;

        GetTilesInCollider(_tempNewTiles);
        
        _tempEnteredTiles.Clear();
        _tempExitedTiles.Clear();

        foreach (Vector3Int tile in _tempNewTiles)
        {
            if (!_currentAffectedTiles.Contains(tile))
            {
                _tempEnteredTiles.Add(tile);
            }
        }

        foreach (Vector3Int tile in _currentAffectedTiles)
        {
            if (!_tempNewTiles.Contains(tile))
            {
                _tempExitedTiles.Add(tile);
            }
        }

        if (_tempEnteredTiles.Count > 0 || _tempExitedTiles.Count > 0) {
            FogOfWarManager.Instance.OnVisionProviderTilesChanged(this, _tempEnteredTiles, _tempExitedTiles);
        }

        _currentAffectedTiles.Clear();
        _currentAffectedTiles.UnionWith(_tempNewTiles);
    }

    private void GetTilesInCollider(HashSet<Vector3Int> outputTiles)
    {
        outputTiles.Clear();

        if (_visionCollider == null || _grid == null) return;

        Bounds bounds = _visionCollider.bounds;
        Vector3 center = bounds.center;
        float radius = bounds.extents.x;
        float radiusSquared = radius * radius;

        int rangeInCells = Mathf.CeilToInt(radius);
        Vector3Int centerCell = _grid.WorldToCell(center);
        Vector3 visionOrigin = hasOffest ? transform.position + offest : transform.position;

        for (int x = -rangeInCells; x <= rangeInCells; x++) {
            int xSquared = x * x;
            for (int y = -rangeInCells; y <= rangeInCells; y++) {
                int ySquared = y * y;
                if (xSquared + ySquared > rangeInCells * rangeInCells + rangeInCells) {
                    continue;
                }

                Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                Vector3 cellWorldPos = _grid.GetCellCenterWorld(cell);
                float distanceSquared = (center - cellWorldPos).sqrMagnitude;

                if (distanceSquared > radiusSquared) {
                    continue;
                }

                if (!HasLineOfSightToTile(visionOrigin, cell)) {
                    continue;
                }

                outputTiles.Add(cell);
            }
        }
    }

    private bool HasLineOfSightToTile(Vector3 from, Vector3Int targetCell)
    {
        Vector3 to = _grid.GetCellCenterWorld(targetCell);
        Vector3Int startCell = _grid.WorldToCell(from);
        Vector3Int endCell = targetCell;
        int dx = Mathf.Abs(endCell.x - startCell.x);
        int dy = Mathf.Abs(endCell.y - startCell.y);
        int steps = Mathf.Max(dx, dy, 1);

        for (int i = 1; i <= steps; i++) {
            float t = (float)i / steps;
            Vector3 point = Vector3.Lerp(from, to, t);
            Vector3Int cell = _grid.WorldToCell(point);

            if (cell == targetCell) {
                return true;
            }

            if (_mapGenerator != null && _mapGenerator.IsTerrainCell(cell)) {
                return false;
            }
        }

        return true;
    }


    private void RegisterWithFogOfWar()
    {
        if (!_isRegistered && FogOfWarManager.Instance != null) {
            FogOfWarManager.Instance.RegisterVisionProvider(this);
            _isRegistered = true;
        }
    }

    private void UnregisterFromFogOfWar()
    {
        if (_isRegistered && FogOfWarManager.Instance != null) {
            FogOfWarManager.Instance.UnregisterVisionProvider(this);
            _isRegistered = false;
        }
    }

    public void OnFogOfWarManagerReady()
    {
        RegisterWithFogOfWar();
    }

    public void SetVisionRange(float range)
    {
        visionRange = range;
        UpdateColliderRadius();

        if (_grid != null && FogOfWarManager.Instance != null) {
            UpdateAffectedTiles();
        }
    }

    public void SetActive(bool active)
    {
        isActive = active;
    }

    public void ForceUpdateAffectedTiles()
    {
        if (_grid != null && FogOfWarManager.Instance != null && _isRegistered) {
            UpdateAffectedTiles();
        }
    }
}
