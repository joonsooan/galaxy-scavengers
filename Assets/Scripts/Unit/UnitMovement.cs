using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class UnitMovement : MonoBehaviour
{
    private static readonly List<Node> NodePool = new ();
    private static int nodePoolIndex;
    private static readonly Vector3Int[] NeighborOffsets = {
        new (1, 0, 0), new (-1, 0, 0), new (0, 1, 0), new (0, -1, 0),
        new (1, 1, 0), new (1, -1, 0), new (-1, 1, 0), new (-1, -1, 0)
    };
    private static readonly Vector3Int[] CardinalOffsets = {
        new (1, 0, 0), new (-1, 0, 0), new (0, 1, 0), new (0, -1, 0)
    };

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Pathfinding")]
    public float waypointTolerance = 0.1f;

    private Vector3 _currentWaypoint;
    private float _finalStoppingDistance;
    private Vector3 _finalTargetPosition;
    private Grid _grid;
    private bool _isAtFinalTarget;
    private bool _isForceStopped;

    private Queue<Vector3> _path = new ();

    private Rigidbody2D _rb;
    private UnitSpriteController _spriteController;
    private UnitBase _unitBase;

    public bool IsMoving => _rb.linearVelocity.sqrMagnitude > 0.01f || _path.Count > 0 || _currentWaypoint != default;
    public Vector3 FinalTargetPosition => _finalTargetPosition;

    public bool HasReachedTarget(float tolerance = 0.1f) {
        if (_finalTargetPosition == default) {
            return false;
        }
        return _isAtFinalTarget || Vector3.Distance(transform.position, _finalTargetPosition) <= tolerance;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _unitBase = GetComponent<UnitBase>();
        _spriteController = GetComponent<UnitSpriteController>();
    }

    private void Start()
    {
        _grid = BuildingManager.Instance.grid;
    }
    
    private Vector3Int _lastExploredCell = new (int.MinValue, int.MinValue, int.MinValue);
    
    private void FixedUpdate()
    {
        if (_isForceStopped)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }
        
        bool isEnemy = _unitBase != null && _unitBase.unitType == UnitBase.UnitType.Enemy;
        if (!isEnemy && _grid != null && FogOfWarManager.Instance != null)
        {
            Vector3Int currentCell = _grid.WorldToCell(transform.position);
            if (currentCell != _lastExploredCell)
            {
                FogOfWarManager.Instance.ExploreTile(currentCell);
                _lastExploredCell = currentCell;
            }
        }
        
        // Update sprite direction based on velocity, but only if velocity is significant
        // This prevents rapid updates that could cause animation speed issues
        if (_rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            Vector2 normalizedVelocity = _rb.linearVelocity.normalized;
            _spriteController?.UpdateSpriteDirection(normalizedVelocity);
        }
        
        if (_currentWaypoint == default) 
        {
            if (_rb.linearVelocity.sqrMagnitude > 0.01f)
            {
                StopMovement();
            }
            return;
        }

        float distanceToWaypoint = Vector3.Distance(transform.position, _currentWaypoint);
        bool isFinalWaypoint = _path.Count == 0;

        float currentTolerance = isFinalWaypoint ? 0.02f : waypointTolerance;

        if (distanceToWaypoint < currentTolerance) 
        {
            if (!isFinalWaypoint) 
            {
                _currentWaypoint = _path.Dequeue();
            }
            else 
            {
                _isAtFinalTarget = true;
                StopMovement();
                return;
            }
        }

        Vector3 direction = (_currentWaypoint - transform.position).normalized;
        
        if (direction.sqrMagnitude > 0.001f)
        {
            float currentSpeed = moveSpeed;

            if (isFinalWaypoint && distanceToWaypoint < 0.5f)
            {
                float slowDownFactor = Mathf.Clamp01(distanceToWaypoint / 0.5f);
                currentSpeed = Mathf.Lerp(0.5f, moveSpeed, slowDownFactor); 
            }

            _rb.linearVelocity = direction * currentSpeed;
        }
        else
        {
            _rb.linearVelocity = Vector2.zero;
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

    public Vector3 GetMoveDirection()
    {
        if (_rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            return _rb.linearVelocity.normalized;
        }
        if (_currentWaypoint != default && _path.Count > 0)
        {
            return (_currentWaypoint - transform.position).normalized;
        }
        return Vector3.zero;
    }

    public bool SetNewTarget(Vector2 targetPosition)
    {
        return SetNewTarget(targetPosition, waypointTolerance);
    }
    
    public bool SetNewTargetDirect(Vector2 targetPosition, float stoppingDistance)
    {
        if (_rb == null || _grid == null) {
            return false;
        }

        _rb.linearVelocity = Vector2.zero;
        _isForceStopped = false;
        _finalTargetPosition = targetPosition;
        _finalStoppingDistance = stoppingDistance;
        _isAtFinalTarget = false;
        
        _path = FindPath(transform.position, _finalTargetPosition);
        
        if (_path.Count > 0) {
            _currentWaypoint = _path.Dequeue();
            return true;
        }
        return false;
    }

    public bool SetNewTarget(Vector2 targetPosition, float stoppingDistance)
    {
        if (_rb == null || _grid == null) {
            return false;
        }

        _rb.linearVelocity = Vector2.zero;
        _isForceStopped = false;
        Vector3Int targetCellPos = _grid.WorldToCell(targetPosition);
        Vector3Int targetCellForPathfinding = targetCellPos;

        if (BuildingManager.Instance != null && BuildingManager.Instance.GetBuildingAt(targetCellPos, out List<Vector3Int> cells)) {
            targetCellForPathfinding = FindBestInteractionCell(cells, transform.position);

            if (targetCellForPathfinding == new Vector3Int(int.MinValue, int.MinValue, int.MinValue)) {
                Debug.LogWarning($"[{name}] No valid interaction cell found for building at {targetCellPos}");
                return false;
            }
        }
        else if (!IsCellWalkable(targetCellPos)) {
            List<Vector3Int> occupiedCells = new List<Vector3Int> { targetCellPos };
            targetCellForPathfinding = FindBestInteractionCell(occupiedCells, transform.position);

            if (targetCellForPathfinding == new Vector3Int(int.MinValue, int.MinValue, int.MinValue))
            {
                Debug.LogWarning($"[{name}] No valid interaction cell found for resource at {targetCellPos}");
                return false;
            }
        }

        _finalTargetPosition = _grid.GetCellCenterWorld(targetCellForPathfinding);
        _finalStoppingDistance = stoppingDistance;
        _isAtFinalTarget = false;

        _path = FindPath(transform.position, _finalTargetPosition);

        if (_path.Count > 0) {
            _currentWaypoint = _path.Dequeue();
            return true;
        }
        return false;
    }

    public void StopMovement()
    {
        _rb.linearVelocity = Vector2.zero;
        _path.Clear();
        _currentWaypoint = default;
    }
    
    public void ForceStopAllMovement()
    {
        _isForceStopped = true;
        _rb.linearVelocity = Vector2.zero;
        _path.Clear();
        _currentWaypoint = default;
        _isAtFinalTarget = false;
        _finalTargetPosition = default;
    }
    
    public void ResumeMovement()
    {
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
        nodePoolIndex = 0;

        Vector3Int startCell = _grid.WorldToCell(startPos);
        Vector3Int endCell = _grid.WorldToCell(endPos);

        List<Node> openList = new List<Node>();
        HashSet<Vector3Int> closedList = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, Node> allNodes = new Dictionary<Vector3Int, Node>();

        Node startNode = GetNodeFromPool(startCell, null, 0, GetDistance(startCell, endCell));
        openList.Add(startNode);
        allNodes.Add(startCell, startNode);

        int iterations = 0;
        const int maxIterations = 50000;

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

                if (!IsCellWalkable(neighborPos)) {
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
        if (BuildingManager.Instance.IsTerrainCell(cell) ||
            BuildingManager.Instance.IsResourceTile(cell))
        {
            return false;
        }
        
        if (BuildingManager.Instance.IsMainStructureCell(cell) || 
            BuildingManager.Instance.GetBuildingAt(cell, out _))
        {
            return false;
        }
        
        return true;
    }

    private List<Vector3Int> GetInteractionCells(List<Vector3Int> occupiedCells)
    {
        HashSet<Vector3Int> interactionCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> occupiedSet = new HashSet<Vector3Int>(occupiedCells);

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

        Vector3Int bestCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
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
        if (nodePoolIndex >= NodePool.Count) {
            NodePool.Add(new Node());
        }

        Node node = NodePool[nodePoolIndex++];
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

        public float FCost => gCost + hCost;
    }
}