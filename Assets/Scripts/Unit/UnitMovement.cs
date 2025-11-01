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
    private Vector3 _currentWaypoint;
    private float _finalStoppingDistance;
    private Vector3 _finalTargetPosition;
    private Grid _grid;

    private Queue<Vector3> _path = new Queue<Vector3>();

    private Rigidbody2D _rb;
    private UnitSpriteController _spriteController;

    public bool IsMoving {
        get {
            return _rb.linearVelocity.sqrMagnitude > 0.01f || _path.Count > 0 || _currentWaypoint != default;
        }
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

    public bool SetNewTarget(Vector2 targetPosition, float stoppingDistance)
    {
        Vector3Int targetCellPos = _grid.WorldToCell(targetPosition);
        Vector3Int targetCellForPathfinding;
        
        if (BuildingManager.Instance.GetBuildingAt(targetCellPos, out Vector3Int anchorCell, out Vector2Int size))
        {
            // 다양한 크기의 건물
            targetCellForPathfinding = FindBestInteractionCell(anchorCell, size, transform.position);
            
            if (targetCellForPathfinding == new Vector3Int(int.MinValue, int.MinValue, int.MinValue))
            {
                Debug.LogWarning($"[{name}] No valid interaction cell found for building at {anchorCell}");
                return false;
            }
        }
        else if (!IsCellWalkable(targetCellPos))
        {
            // 건물이 아니지만 걸을 수 없는 타일 (예: 1x1 자원)
            // 1x1 건물처럼 취급하여 상호작용 위치 탐색
            targetCellForPathfinding = FindBestInteractionCell(targetCellPos, new Vector2Int(1, 1), transform.position);
            
            if (targetCellForPathfinding == new Vector3Int(int.MinValue, int.MinValue, int.MinValue)) // 유효한 셀 없음
            {
                Debug.LogWarning($"[{name}] No valid interaction cell found for resource at {targetCellPos}");
                return false;
            }
        }
        
        _finalTargetPosition = _grid.GetCellCenterWorld(targetCellPos);
        _finalStoppingDistance = stoppingDistance;

        _path = FindPath(transform.position, _finalTargetPosition);

        if (_path.Count > 0) {
            _currentWaypoint = _path.Dequeue();
            return true;
        }
        return false;
    }

    public void MoveToTarget()
    {
        if (_path.Count == 0 && _currentWaypoint == default) {
            StopMovement();
            return;
        }

        Vector3 direction = (_currentWaypoint - transform.position).normalized;
        _rb.linearVelocity = direction * moveSpeed;
        _spriteController?.UpdateSpriteDirection(direction);

        float distanceToWaypoint = Vector3.Distance(transform.position, _currentWaypoint);

        if (distanceToWaypoint < waypointTolerance) {
            if (_path.Count > 0) {
                _currentWaypoint = _path.Dequeue();
            }
            else {
                // _rb.linearVelocity = Vector2.zero;
                StopMovement();
            }
        }
    }

    public void StopMovement()
    {
        _rb.linearVelocity = Vector2.zero;
        _path.Clear();
        _currentWaypoint = default;
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
        return !BuildingManager.Instance.IsResourceTile(cell) && !BuildingManager.Instance.IsBuildingTile(cell);
    }
    
    private List<Vector3Int> GetInteractionCells(Vector3Int anchorCell, Vector2Int size)
    {
        HashSet<Vector3Int> interactionCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> occupiedSet = new HashSet<Vector3Int>();

        // 건물이 차지하는 모든 셀 추가
        for (int x = 0; x < size.x; x++) {
            for (int y = 0; y < size.y; y++) {
                occupiedSet.Add(anchorCell + new Vector3Int(x, y, 0));
            }
        }

        // 차지하는 모든 셀의 '상하좌우' 이웃 탐색
        foreach (Vector3Int occupiedCell in occupiedSet) {
            foreach (Vector3Int offset in CardinalOffsets) {
                Vector3Int neighbor = occupiedCell + offset;
                
                // 이웃이 건물 자신의 일부가 아닌 경우에만 상호작용 셀로 추가
                if (!occupiedSet.Contains(neighbor) && IsCellWalkable(neighbor)) {
                    interactionCells.Add(neighbor);
                }
            }
        }
        return interactionCells.ToList();
    }
    
    private Vector3Int FindBestInteractionCell(Vector3Int anchorCell, Vector2Int size, Vector3 startPos)
    {
        List<Vector3Int> potentialCells = GetInteractionCells(anchorCell, size);
        Vector3Int startCell = _grid.WorldToCell(startPos);
        
        Vector3Int bestCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue); // 유효하지 않은 값으로 초기화
        float minDistance = float.MaxValue;

        foreach (Vector3Int cell in potentialCells)
        {
            if (IsCellWalkable(cell))
            {
                float distance = GetDistance(startCell, cell); 
                if (distance < minDistance)
                {
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
