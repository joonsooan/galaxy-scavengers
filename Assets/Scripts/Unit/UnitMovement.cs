using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class UnitMovement : MonoBehaviour
{
    private static readonly List<Node> NodePool = new List<Node>();
    private static int _nodePoolIndex;
    private static readonly Vector3Int[] NeighborOffsets = {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
        new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0), new Vector3Int(-1, 1, 0), new Vector3Int(-1, -1, 0)
    };
    private static readonly Vector3Int[] CardinalOffsets = {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
    };

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Pathfinding")]
    public float waypointTolerance = 0.1f;
    [SerializeField] private float alignmentSpeed = 3f; // Speed for aligning to cell center
    private Vector3 _currentWaypoint;
    private float _finalStoppingDistance;
    private Vector3 _finalTargetPosition;
    private Grid _grid;
    private bool _isAtFinalTarget = false;
    private bool _isAligningToCenter = false;
    private Vector3 _targetCellCenter;
    private bool _isForceStopped = false; // Flag to prevent any movement updates
    private bool _disableAlignment = false; // Flag to disable center alignment for specific tasks

    private Queue<Vector3> _path = new Queue<Vector3>();

    private Rigidbody2D _rb;
    private UnitSpriteController _spriteController;

    public bool IsMoving {
        get {
            return _rb.linearVelocity.sqrMagnitude > 0.01f || _path.Count > 0 || _currentWaypoint != default;
        }
    }
    
    public Vector3 FinalTargetPosition {
        get {
            return _finalTargetPosition;
        }
    }

    public bool HasReachedTarget(float tolerance = 0.1f) {
        if (_finalTargetPosition == default) {
            return false;
        }
        float distanceToTarget = Vector3.Distance(transform.position, _finalTargetPosition);
        return distanceToTarget <= tolerance;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteController = GetComponent<UnitSpriteController>();
    }

    private void Start()
    {
        _grid = BuildingManager.Instance.grid;
    }
    
    private void FixedUpdate()
    {
        // If force stopped, ensure velocity is zero and don't update anything
        if (_isForceStopped)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }
        
        // 0. If we're aligning to center, handle that first
        if (_isAligningToCenter)
        {
            float distanceToCenter = Vector3.Distance(transform.position, _targetCellCenter);
            if (distanceToCenter < 0.01f)
            {
                // Reached center, stop aligning
                transform.position = new Vector3(_targetCellCenter.x, _targetCellCenter.y, transform.position.z);
                _rb.linearVelocity = Vector2.zero;
                _isAligningToCenter = false;
                _isAtFinalTarget = false; // Reset flag after alignment completes
                return;
            }
            else
            {
                // Smoothly move towards center
                Vector3 alignDirection = (_targetCellCenter - transform.position).normalized;
                _rb.linearVelocity = alignDirection * alignmentSpeed;
                _spriteController?.UpdateSpriteDirection(alignDirection);
                return;
            }
        }
        
        // 1. 이동할 웨이포인트가 없으면(경로가 없거나 완료) 즉시 정지하고 종료
        if (_currentWaypoint == default) 
        {
            // StopMovement()를 호출하면 속도가 0이 되므로, 
            // 이미 0이면 불필요한 호출을 방지합니다.
            if (_rb.linearVelocity.sqrMagnitude > 0.01f)
            {
                StopMovement();
            }
            return;
        }

        // 2. 웨이포인트를 향해 이동
        // Calculate direction and set velocity - this ensures clean movement start
        Vector3 direction = (_currentWaypoint - transform.position).normalized;
        // Only set velocity if direction is valid (not zero)
        if (direction.sqrMagnitude > 0.001f)
        {
            _rb.linearVelocity = direction * moveSpeed;
            _spriteController?.UpdateSpriteDirection(direction);
        }
        else
        {
            // If direction is invalid, zero velocity
            _rb.linearVelocity = Vector2.zero;
        }

        // 3. 웨이포인트에 도착했는지 확인
        float distanceToWaypoint = Vector3.Distance(transform.position, _currentWaypoint);
        if (distanceToWaypoint < waypointTolerance) 
        {
            // 3-1. 아직 경로에 다음 웨이포인트가 남아있다면
            if (_path.Count > 0) 
            {
                // 다음 웨이포인트를 목표로 설정
                _currentWaypoint = _path.Dequeue();
            }
            // 3-2. 마지막 웨이포인트에 도착했다면
            else 
            {
                // Check if this is the final target (interaction cell)
                if (_finalTargetPosition != default && Vector3.Distance(_currentWaypoint, _finalTargetPosition) < 0.01f)
                {
                    _isAtFinalTarget = true;
                    // Only start aligning if alignment is not disabled
                    if (!_disableAlignment)
                    {
                        StartAligningToCellCenter();
                    }
                }
                
                // 이동 완료 및 정지
                StopMovement();
            }
        }
    }
    
    private void StartAligningToCellCenter()
    {
        if (_grid == null || _finalTargetPosition == default) return;
        
        // Get the cell position of the final target
        Vector3Int targetCell = _grid.WorldToCell(_finalTargetPosition);
        
        // Get the exact center of that cell
        _targetCellCenter = _grid.GetCellCenterWorld(targetCell);
        _targetCellCenter = new Vector3(_targetCellCenter.x, _targetCellCenter.y, transform.position.z);
        
        // Start aligning smoothly
        _isAligningToCenter = true;
    }
    
    public void SnapToTargetCellCenter()
    {
        // Public method to start aligning to the final target cell center
        // Used when units need to align immediately (e.g., when starting to attack)
        if (_finalTargetPosition != default)
        {
            _isAtFinalTarget = true;
            StartAligningToCellCenter();
        }
    }

    private void OnEnable()
    {
        BuildingManager.OnTilemapChanged += HandleTilemapChange;
    }

    private void OnDisable()
    {
        BuildingManager.OnTilemapChanged -= HandleTilemapChange;
    }

    private void OnDrawGizmosSelected()
    {
        if (_path.Count == 0) return;

        Gizmos.color = Color.yellow;
        Vector3 prevPos = transform.position;
        foreach (Vector3 waypoint in _path) {
            Gizmos.DrawLine(prevPos, waypoint);
            prevPos = waypoint;
        }
    }

    public bool SetNewTarget(Vector2 targetPosition)
    {
        return SetNewTarget(targetPosition, waypointTolerance);
    }
    
    public bool SetNewTargetDirect(Vector2 targetPosition, float stoppingDistance)
    {
        return SetNewTargetDirect(targetPosition, stoppingDistance, false);
    }
    
    public bool SetNewTargetDirect(Vector2 targetPosition, float stoppingDistance, bool disableAlignment)
    {
        // Set target directly without finding interaction cells (used for assigned interaction positions)
        // Explicitly zero velocity before starting new movement to prevent drift
        _rb.linearVelocity = Vector2.zero;
        _isForceStopped = false; // Resume movement when setting new target
        _finalTargetPosition = targetPosition;
        _finalStoppingDistance = stoppingDistance;
        _isAtFinalTarget = false;
        _isAligningToCenter = false; // Reset alignment when setting new target
        _disableAlignment = disableAlignment; // Store flag to prevent alignment
        
        _path = FindPath(transform.position, _finalTargetPosition);
        
        if (_path.Count > 0) {
            _currentWaypoint = _path.Dequeue();
            return true;
        }
        return false;
    }

    public bool SetNewTarget(Vector2 targetPosition, float stoppingDistance)
    {
        // Explicitly zero velocity before starting new movement to prevent drift
        _rb.linearVelocity = Vector2.zero;
        _isForceStopped = false; // Resume movement when setting new target
        Vector3Int targetCellPos = _grid.WorldToCell(targetPosition);
        Vector3Int targetCellForPathfinding = targetCellPos;

        if (BuildingManager.Instance != null && BuildingManager.Instance.GetBuildingAt(targetCellPos, out List<Vector3Int> cells)) {
            // 다양한 크기의 건물
            targetCellForPathfinding = FindBestInteractionCell(cells, transform.position);

            if (targetCellForPathfinding == new Vector3Int(int.MinValue, int.MinValue, int.MinValue)) {
                Debug.LogWarning($"[{name}] No valid interaction cell found for building at {targetCellPos}");
                return false;
            }
        }
        else if (!IsCellWalkable(targetCellPos)) {
            // 건물이 아니지만 걸을 수 없는 타일 (예: 1x1 자원)
            // 1x1 건물처럼 취급하여 상호작용 위치 탐색
            List<Vector3Int> occupiedCells = new List<Vector3Int> { targetCellPos };

            targetCellForPathfinding = FindBestInteractionCell(occupiedCells, transform.position);

            if (targetCellForPathfinding == new Vector3Int(int.MinValue, int.MinValue, int.MinValue)) // 유효한 셀 없음
            {
                Debug.LogWarning($"[{name}] No valid interaction cell found for resource at {targetCellPos}");
                return false;
            }
        }

        // _finalTargetPosition = _grid.GetCellCenterWorld(targetCellPos);
        _finalTargetPosition = _grid.GetCellCenterWorld(targetCellForPathfinding);
        _finalStoppingDistance = stoppingDistance;
        _isAtFinalTarget = false;
        _isAligningToCenter = false; // Reset alignment when setting new target

        _path = FindPath(transform.position, _finalTargetPosition);

        if (_path.Count > 0) {
            _currentWaypoint = _path.Dequeue();
            return true;
        }
        return false;
    }

    // public void MoveToTarget()
    // {
    //     if (_path.Count == 0 && _currentWaypoint == default) {
    //         StopMovement();
    //         return;
    //     }
    //
    //     Vector3 direction = (_currentWaypoint - transform.position).normalized;
    //     _rb.linearVelocity = direction * moveSpeed;
    //     _spriteController?.UpdateSpriteDirection(direction);
    //
    //     float distanceToWaypoint = Vector3.Distance(transform.position, _currentWaypoint);
    //
    //     if (distanceToWaypoint < waypointTolerance) {
    //         if (_path.Count > 0) {
    //             _currentWaypoint = _path.Dequeue();
    //         }
    //         else {
    //             // _rb.linearVelocity = Vector2.zero;
    //             StopMovement();
    //         }
    //     }
    // }

    public void StopMovement()
    {
        // If we're at the final target (interaction cell), start aligning to center
        // Only if alignment is not disabled
        if (_isAtFinalTarget && _finalTargetPosition != default && !_isAligningToCenter && !_disableAlignment)
        {
            StartAligningToCellCenter();
        }
        
        _rb.linearVelocity = Vector2.zero;
        _path.Clear();
        _currentWaypoint = default;
        // Don't reset _isAtFinalTarget here - let alignment complete first
    }
    
    public void ForceStopAllMovement()
    {
        // Force stop all movement including alignment - used when unit needs to wait
        _isForceStopped = true;
        _rb.linearVelocity = Vector2.zero;
        _path.Clear();
        _currentWaypoint = default;
        _isAligningToCenter = false;
        _isAtFinalTarget = false;
        _finalTargetPosition = default;
        _disableAlignment = false; // Reset alignment flag when force stopping
    }
    
    public void ResumeMovement()
    {
        // Resume movement after force stop
        // Explicitly zero velocity to prevent any residual movement
        _rb.linearVelocity = Vector2.zero;
        _isForceStopped = false;
    }

    private void HandleTilemapChange(Vector3Int changedCellPosition)
    {
        if (_path.Count == 0 && _rb.linearVelocity == Vector2.zero) return;

        bool pathIsBlocked = _path.Any(waypoint => _grid.WorldToCell(waypoint) == changedCellPosition);
        if (_grid.WorldToCell(_currentWaypoint) == changedCellPosition) {
            pathIsBlocked = true;
        }

        if (pathIsBlocked) {
            SetNewTarget(_finalTargetPosition, _finalStoppingDistance);
        }
    }

    private Queue<Vector3> FindPath(Vector3 startPos, Vector3 endPos)
    {
        _nodePoolIndex = 0;

        Vector3Int startCell = _grid.WorldToCell(startPos);
        Vector3Int endCell = _grid.WorldToCell(endPos);

        List<Node> openList = new List<Node>();
        HashSet<Vector3Int> closedList = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, Node> allNodes = new Dictionary<Vector3Int, Node>();

        Node startNode = GetNodeFromPool(startCell, null, 0, GetDistance(startCell, endCell));
        openList.Add(startNode);
        allNodes.Add(startCell, startNode);

        int iterations = 0;
        const int maxIterations = 2000;

        while (openList.Count > 0 && iterations < maxIterations) {
            iterations++;

            Node currentNode = openList[0];
            for (int i = 1; i < openList.Count; i++) {
                if (openList[i].FCost < currentNode.FCost ||
                    Mathf.Approximately(openList[i].FCost, currentNode.FCost) && openList[i].hCost < currentNode.hCost) {
                    currentNode = openList[i];
                }
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode.position);

            if (currentNode.position == endCell) {
                return ReconstructPath(currentNode);
            }

            foreach (Vector3Int offset in NeighborOffsets) {
                Vector3Int neighborPos = currentNode.position + offset;
                if (closedList.Contains(neighborPos)) continue;

                if (neighborPos != endCell && !IsCellWalkable(neighborPos)) {
                    continue;
                }

                float newGCost = currentNode.gCost + GetDistance(currentNode.position, neighborPos);

                if (!allNodes.TryGetValue(neighborPos, out Node neighborNode)) {
                    neighborNode = GetNodeFromPool(neighborPos, currentNode, newGCost, GetDistance(neighborPos, endCell));
                    allNodes.Add(neighborPos, neighborNode);
                    openList.Add(neighborNode);
                }
                else if (newGCost < neighborNode.gCost) {
                    neighborNode.parent = currentNode;
                    neighborNode.gCost = newGCost;
                }
            }
        }

        Debug.LogWarning("Path not found or search limit exceeded.");
        return new Queue<Vector3>();
    }

    private bool IsCellWalkable(Vector3Int cell)
    {
        if (BuildingManager.Instance == null) {
            return true; // If BuildingManager is null, assume cell is walkable
        }
        
        // Check if cell is a resource tile, building tile, or temporary construction tile
        if (BuildingManager.Instance.IsResourceTile(cell) || 
            BuildingManager.Instance.IsBuildingTile(cell) ||
            BuildingManager.Instance.IsTemporaryTile(cell))
        {
            return false;
        }
        
        // Check if there's a building piece GameObject at this cell (even without a tile)
        BuildingPiece piece = BuildingManager.Instance.GetPieceAt(cell);
        if (piece != null)
        {
            return false;
        }
        
        return true;
    }

    private List<Vector3Int> GetInteractionCells(List<Vector3Int> occupiedCells)
    {
        HashSet<Vector3Int> interactionCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> occupiedSet = new HashSet<Vector3Int>(occupiedCells);

        // 건물이 차지하는 모든 셀 추가
        foreach (Vector3Int occupiedCell in occupiedSet) {
            foreach (Vector3Int offset in CardinalOffsets) {
                Vector3Int neighbor = occupiedCell + offset;

                if (!occupiedSet.Contains(neighbor) && IsCellWalkable(neighbor)) {
                    interactionCells.Add(neighbor);
                }
            }
        }
        return interactionCells.ToList();
    }

    private Vector3Int FindBestInteractionCell(List<Vector3Int> occupiedCells, Vector3 startPos)
    {
        List<Vector3Int> potentialCells = GetInteractionCells(occupiedCells);
        Vector3Int startCell = _grid.WorldToCell(startPos);

        Vector3Int bestCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue); // 유효하지 않은 값으로 초기화
        float minDistance = float.MaxValue;

        foreach (Vector3Int cell in potentialCells) {
            if (IsCellWalkable(cell)) {
                float distance = GetDistance(startCell, cell);
                if (distance < minDistance) {
                    minDistance = distance;
                    bestCell = cell;
                }
            }
        }
        return bestCell;
    }

    private Queue<Vector3> ReconstructPath(Node endNode)
    {
        List<Vector3> path = new List<Vector3>();
        Node currentNode = endNode;
        while (currentNode != null) {
            path.Add(_grid.GetCellCenterWorld(currentNode.position));
            currentNode = currentNode.parent;
        }
        path.Reverse();
        return new Queue<Vector3>(path);
    }

    private float GetDistance(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx + dy;
    }

    private Node GetNodeFromPool(Vector3Int position, Node parent, float gCost, float hCost)
    {
        if (_nodePoolIndex >= NodePool.Count) {
            NodePool.Add(new Node());
        }

        Node node = NodePool[_nodePoolIndex++];
        node.position = position;
        node.parent = parent;
        node.gCost = gCost;
        node.hCost = hCost;
        return node;
    }

    private class Node
    {
        public float gCost;
        public float hCost;
        public Node parent;
        public Vector3Int position;

        public float FCost {
            get {
                return gCost + hCost;
            }
        }
    }
}
