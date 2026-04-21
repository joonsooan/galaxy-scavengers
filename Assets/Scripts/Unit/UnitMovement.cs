using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class UnitMovement : MonoBehaviour
{
    private static readonly Vector3Int[] NeighborOffsets = {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0), new Vector3Int(-1, 1, 0), new Vector3Int(-1, -1, 0)
    };
    private static readonly Vector3Int[] CardinalOffsets = {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
    };

    private static readonly List<Node> NodePool = new List<Node>(1000);
    private static int nodePoolIndex;

    private static readonly Dictionary<Vector3Int, Node> AllNodes = new Dictionary<Vector3Int, Node>(1000);
    private static readonly HashSet<Vector3Int> ClosedSet = new HashSet<Vector3Int>();
    private static readonly MinHeap OpenSet = new MinHeap(1000);
    private static readonly Dictionary<Vector3Int, HashSet<UnitMovement>> _cellAssignments = new Dictionary<Vector3Int, HashSet<UnitMovement>>();

    private static int _pathfindingCallsThisFrame;
    private static int _lastProcessedFrame = -1;
    private const int MaxPathfindingPerFrame = 5;
    private static readonly Queue<(UnitMovement unit, Vector3 targetPos, float stoppingDistance)> _pendingRepathRequests = new Queue<(UnitMovement, Vector3, float)>();
    private static readonly Queue<(UnitMovement unit, Vector2 targetPos, float stoppingDistance)> _deferredPathfindingRequests = new Queue<(UnitMovement, Vector2, float)>();

    private static readonly float D = 1f;
    private static readonly float D2 = 1.41421356f;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Pathfinding")]
    public float waypointTolerance = 0.1f;

    private Vector3 _currentWaypoint;
    private float _finalStoppingDistance;
    private Grid _grid;
    private bool _isAtFinalTarget;
    private bool _isForceStopped;

    private Vector3Int _lastExploredCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    private Vector3Int _assignedInteractionCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

    private Queue<Vector3> _path = new Queue<Vector3>();

    private Rigidbody2D _rb;
    private UnitSpriteController _spriteController;
    private UnitBase _unitBase;
    private bool _isEnemy;

    public bool IsMoving
    {
        get
        {
            return _rb.linearVelocity.sqrMagnitude > 0.01f || _path.Count > 0 || _currentWaypoint != default;
        }
    }

    public Vector3 FinalTargetPosition { get; private set; }

    public static bool IsCellAssigned(Vector3Int cell)
    {
        return _cellAssignments.TryGetValue(cell, out var units) && units != null && units.Count > 0;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _unitBase = GetComponent<UnitBase>();
        _spriteController = GetComponentInChildren<UnitSpriteController>();
        _isEnemy = _unitBase != null && _unitBase.unitType == UnitBase.UnitType.Enemy;
    }

    private void Start()
    {
        _grid = BuildingManager.Instance.grid;
    }

    public static void ProcessPathfindingQueues()
    {
        if (Time.frameCount == _lastProcessedFrame) return;
        _lastProcessedFrame = Time.frameCount;
        _pathfindingCallsThisFrame = 0;
        while (_pathfindingCallsThisFrame < MaxPathfindingPerFrame && _pendingRepathRequests.Count > 0)
        {
            var (unit, targetPos, stoppingDistance) = _pendingRepathRequests.Dequeue();
            if (unit != null && unit.isActiveAndEnabled)
            {
                unit.SetNewTarget(targetPos, stoppingDistance);
            }
        }
        while (_pathfindingCallsThisFrame < MaxPathfindingPerFrame && _deferredPathfindingRequests.Count > 0)
        {
            var (unit, targetPos, stoppingDistance) = _deferredPathfindingRequests.Dequeue();
            if (unit != null && unit.isActiveAndEnabled)
            {
                unit.SetNewTarget(targetPos, stoppingDistance);
            }
        }
    }

    private void FixedUpdate()
    {
        if (_isForceStopped)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (_currentWaypoint == default)
        {
            if (_rb.linearVelocity.sqrMagnitude > 0.01f)
            {
                StopMovement();
            }
            return;
        }

        if (!_isEnemy && _grid != null && FogOfWarManager.Instance != null)
        {
            Vector3Int currentCell = _grid.WorldToCell(transform.position);
            if (currentCell != _lastExploredCell)
            {
                FogOfWarManager.Instance.ExploreTile(currentCell);
                _lastExploredCell = currentCell;
            }
        }

        if (_rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            Vector2 normalizedVelocity = _rb.linearVelocity.normalized;
            _spriteController?.UpdateSpriteDirection(normalizedVelocity);
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
        UnregisterInteractionCell();
    }

    private void OnDrawGizmosSelected()
    {
        if (_path.Count == 0) return;

        Gizmos.color = Color.yellow;
        Vector3 prevPos = transform.position;
        foreach (Vector3 waypoint in _path)
        {
            Gizmos.DrawLine(prevPos, waypoint);
            prevPos = waypoint;
        }
    }

    public bool HasReachedTarget(float tolerance = 0.1f)
    {
        if (FinalTargetPosition == default)
        {
            return false;
        }
        return _isAtFinalTarget || Vector3.Distance(transform.position, FinalTargetPosition) <= tolerance;
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
        return SetNewTarget(targetPosition, stoppingDistance);
    }

    public bool SetNewTarget(Vector2 targetPosition, float stoppingDistance)
    {
        if (_rb == null || _grid == null)
        {
            return false;
        }

        _rb.linearVelocity = Vector2.zero;
        _isForceStopped = false;
        Vector3Int targetCellPos = _grid.WorldToCell(targetPosition);
        Vector3Int targetCellForPathfinding = targetCellPos;

        if (BuildingManager.Instance != null && BuildingManager.Instance.GetBuildingAt(targetCellPos, out List<Vector3Int> cells))
        {
            targetCellForPathfinding = FindBestInteractionCell(cells, transform.position);

            if (targetCellForPathfinding == new Vector3Int(int.MinValue, int.MinValue, int.MinValue))
            {
                Debug.LogWarning($"[{name}] No valid interaction cell found for building at {targetCellPos}");
                return false;
            }
        }
        else if (!BuildingManager.Instance.IsCellWalkable(targetCellPos, _unitBase is Unit_Construct))
        {
            List<Vector3Int> occupiedCells = new List<Vector3Int> { targetCellPos };
            targetCellForPathfinding = FindBestInteractionCell(occupiedCells, transform.position);

            if (targetCellForPathfinding == new Vector3Int(int.MinValue, int.MinValue, int.MinValue))
            {
                Debug.LogWarning($"[{name}] No valid interaction cell found for resource at {targetCellPos}");
                return false;
            }
        }

        UnregisterInteractionCell();

        FinalTargetPosition = _grid.GetCellCenterWorld(targetCellForPathfinding);
        _finalStoppingDistance = stoppingDistance;
        _isAtFinalTarget = false;

        RegisterInteractionCell(targetCellForPathfinding);

        if (!TryConsumePathfindingBudget())
        {
            _deferredPathfindingRequests.Enqueue((this, (Vector2)FinalTargetPosition, stoppingDistance));
            return false;
        }

        _path = FindPath(transform.position, FinalTargetPosition);

        if (_path.Count > 1)
        {
            Vector3 firstPoint = _path.Peek();
            Vector3Int firstCell = _grid.WorldToCell(firstPoint);
            Vector3Int currentCell = _grid.WorldToCell(transform.position);

            if (firstCell == currentCell)
            {
                _path.Dequeue();
            }
        }

        if (_path.Count > 0)
        {
            _currentWaypoint = _path.Dequeue();
            return true;
        }
        return false;
    }

    private static bool TryConsumePathfindingBudget()
    {
        if (_pathfindingCallsThisFrame >= MaxPathfindingPerFrame) return false;
        _pathfindingCallsThisFrame++;
        return true;
    }

    public void StopMovement()
    {
        _rb.linearVelocity = Vector2.zero;
        _path.Clear();
        _currentWaypoint = default;
        _isAtFinalTarget = false;
        FinalTargetPosition = default;
        UnregisterInteractionCell();
    }

    public void ForceStopAllMovement()
    {
        _isForceStopped = true;
        _rb.linearVelocity = Vector2.zero;
        _path.Clear();
        _currentWaypoint = default;
        _isAtFinalTarget = false;
        FinalTargetPosition = default;
        UnregisterInteractionCell();
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
        if (_grid.WorldToCell(_currentWaypoint) == changedCellPosition)
        {
            pathIsBlocked = true;
        }

        if (pathIsBlocked)
        {
            _pendingRepathRequests.Enqueue((this, FinalTargetPosition, _finalStoppingDistance));
        }
    }

    private Queue<Vector3> FindPath(Vector3 startPos, Vector3 endPos)
    {
        nodePoolIndex = 0;
        AllNodes.Clear();
        ClosedSet.Clear();
        OpenSet.Clear();

        Vector3Int startCell = _grid.WorldToCell(startPos);
        Vector3Int endCell = _grid.WorldToCell(endPos);

        Node startNode = GetNodeFromPool(startCell, null, 0, GetDistance(startCell, endCell));

        AllNodes.Add(startCell, startNode);
        OpenSet.Push(startNode);

        int iterations = 0;
        int dx = Mathf.Abs(endCell.x - startCell.x);
        int dy = Mathf.Abs(endCell.y - startCell.y);
        int maxIterations = Mathf.Clamp((dx + dy) * 20, 2000, 50000);

        while (OpenSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            Node currentNode = OpenSet.Pop();
            if (ClosedSet.Contains(currentNode.position)) continue;
            ClosedSet.Add(currentNode.position);
            if (currentNode.position == endCell)
            {
                return ReconstructPath(currentNode);
            }

            foreach (Vector3Int offset in NeighborOffsets)
            {
                Vector3Int neighborPos = currentNode.position + offset;
                if (ClosedSet.Contains(neighborPos)) continue;

                if (!BuildingManager.Instance.IsCellWalkable(neighborPos, _unitBase is Unit_Construct))
                {
                    continue;
                }

                float newGCost = currentNode.gCost + GetDistance(currentNode.position, neighborPos);

                if (!AllNodes.TryGetValue(neighborPos, out Node neighborNode))
                {
                    neighborNode = GetNodeFromPool(neighborPos, currentNode, newGCost, GetDistance(neighborPos, endCell));
                    AllNodes.Add(neighborPos, neighborNode);
                    OpenSet.Push(neighborNode);
                }
                else if (newGCost < neighborNode.gCost)
                {
                    neighborNode.parent = currentNode;
                    neighborNode.gCost = newGCost;
                    OpenSet.Push(neighborNode);
                }
            }
        }

        if (iterations >= maxIterations)
        {
        }

        Debug.LogWarning($"Path not found. start={startCell}, end={endCell}, iter={iterations}/{maxIterations}");
        return new Queue<Vector3>();
    }

    private List<Vector3Int> GetInteractionCells(List<Vector3Int> occupiedCells)
    {
        HashSet<Vector3Int> interactionCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> occupiedSet = new HashSet<Vector3Int>(occupiedCells);

        foreach (Vector3Int occupiedCell in occupiedSet)
        {
            foreach (Vector3Int offset in CardinalOffsets)
            {
                Vector3Int neighbor = occupiedCell + offset;

                if (!occupiedSet.Contains(neighbor) && BuildingManager.Instance.IsCellWalkable(neighbor, _unitBase is Unit_Construct))
                {
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

        foreach (Vector3Int cell in potentialCells)
        {
            if (!BuildingManager.Instance.IsCellWalkable(cell, _unitBase is Unit_Construct)) continue;

            bool isAssigned = _cellAssignments.TryGetValue(cell, out var assignedUnits) &&
                             assignedUnits != null && assignedUnits.Count > 0 &&
                             !assignedUnits.Contains(this);

            if (isAssigned) continue;

            float distance = GetDistance(startCell, cell);
            if (distance < minDistance)
            {
                minDistance = distance;
                bestCell = cell;
            }
        }

        if (bestCell == new Vector3Int(int.MinValue, int.MinValue, int.MinValue))
        {
            foreach (Vector3Int cell in potentialCells)
            {
                if (BuildingManager.Instance.IsCellWalkable(cell, _unitBase is Unit_Construct))
                {
                    float distance = GetDistance(startCell, cell);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestCell = cell;
                    }
                }
            }
        }

        return bestCell;
    }

    private void RegisterInteractionCell(Vector3Int cell)
    {
        if (cell == new Vector3Int(int.MinValue, int.MinValue, int.MinValue))
        {
            return;
        }

        _assignedInteractionCell = cell;

        if (!_cellAssignments.TryGetValue(cell, out var units))
        {
            units = new HashSet<UnitMovement>();
            _cellAssignments[cell] = units;
        }

        units.Add(this);
    }

    private void UnregisterInteractionCell()
    {
        if (_assignedInteractionCell == new Vector3Int(int.MinValue, int.MinValue, int.MinValue))
        {
            return;
        }

        if (_cellAssignments.TryGetValue(_assignedInteractionCell, out var units))
        {
            units.Remove(this);
            if (units.Count == 0)
            {
                _cellAssignments.Remove(_assignedInteractionCell);
            }
        }

        _assignedInteractionCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    }

    private Queue<Vector3> ReconstructPath(Node endNode)
    {
        List<Vector3> path = new List<Vector3>();
        Node currentNode = endNode;
        while (currentNode != null)
        {
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
        return D * (dx + dy) + (D2 - 2f * D) * Mathf.Min(dx, dy);
    }

    private Node GetNodeFromPool(Vector3Int position, Node parent, float gCost, float hCost)
    {
        if (nodePoolIndex >= NodePool.Count)
        {
            NodePool.Add(new Node());
        }

        Node node = NodePool[nodePoolIndex++];
        node.position = position;
        node.parent = parent;
        node.gCost = gCost;
        node.hCost = hCost;
        return node;
    }

    public class Node : IComparable<Node>
    {
        public float gCost;
        public float hCost;
        public Node parent;
        public Vector3Int position;

        public float FCost
        {
            get
            {
                return gCost + hCost;
            }
        }

        public int CompareTo(Node other)
        {
            int compare = FCost.CompareTo(other.FCost);
            if (compare == 0)
            {
                compare = hCost.CompareTo(other.hCost);
            }
            return compare;
        }
    }

    public class MinHeap
    {
        private Node[] _items;

        public MinHeap(int capacity)
        {
            _items = new Node[capacity];
            Count = 0;
        }

        public int Count { get; private set; }

        public void Clear()
        {
            Count = 0;
        }

        public void Push(Node item)
        {
            if (Count == _items.Length) Resize();
            _items[Count] = item;
            SortUp(Count);
            Count++;
        }

        public Node Pop()
        {
            Node firstItem = _items[0];
            Count--;
            _items[0] = _items[Count];
            SortDown(0);
            return firstItem;
        }

        private void SortUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (_items[index].FCost < _items[parentIndex].FCost)
                {
                    Swap(index, parentIndex);
                    index = parentIndex;
                }
                else break;
            }
        }

        private void SortDown(int index)
        {
            while (true)
            {
                int childIndexLeft = index * 2 + 1;
                int childIndexRight = index * 2 + 2;
                int swapIndex = 0;

                if (childIndexLeft < Count)
                {
                    swapIndex = childIndexLeft;
                    if (childIndexRight < Count)
                    {
                        if (_items[childIndexRight].FCost < _items[childIndexLeft].FCost)
                            swapIndex = childIndexRight;
                    }

                    if (_items[swapIndex].FCost < _items[index].FCost)
                    {
                        Swap(index, swapIndex);
                        index = swapIndex;
                    }
                    else return;
                }
                else return;
            }
        }

        private void Swap(int a, int b)
        {
            Node temp = _items[a];
            _items[a] = _items[b];
            _items[b] = temp;
        }

        private void Resize()
        {
            Node[] newItems = new Node[_items.Length * 2];
            Array.Copy(_items, newItems, _items.Length);
            _items = newItems;
        }
    }
}
