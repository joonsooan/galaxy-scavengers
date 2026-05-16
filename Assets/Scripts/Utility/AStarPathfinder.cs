using System;
using System.Collections.Generic;
using UnityEngine;

public static class AStarPathfinder
{
    private static readonly Vector3Int[] NeighborOffsets = {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0), new Vector3Int(-1, 1, 0), new Vector3Int(-1, -1, 0)
    };

    private static readonly List<Node> NodePool = new List<Node>(1000);
    private static int nodePoolIndex;

    private static readonly Dictionary<Vector3Int, Node> AllNodes = new Dictionary<Vector3Int, Node>(1000);
    private static readonly HashSet<Vector3Int> ClosedSet = new HashSet<Vector3Int>();
    private static readonly MinHeap OpenSet = new MinHeap(1000);

    private static readonly float D = 1f;
    private static readonly float D2 = 1.41421356f;

    public static Queue<Vector3> FindPath(Grid grid, Vector3 startPos, Vector3 endPos, bool isConstructUnit)
    {
        nodePoolIndex = 0;
        AllNodes.Clear();
        ClosedSet.Clear();
        OpenSet.Clear();

        Vector3Int startCell = grid.WorldToCell(startPos);
        Vector3Int endCell = grid.WorldToCell(endPos);

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
                return ReconstructPath(grid, currentNode);
            }

            foreach (Vector3Int offset in NeighborOffsets)
            {
                Vector3Int neighborPos = currentNode.position + offset;
                if (ClosedSet.Contains(neighborPos)) continue;

                if (BuildingManager.Instance != null && !BuildingManager.Instance.IsCellWalkable(neighborPos, isConstructUnit))
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
                    OpenSet.UpdateItem(neighborNode);
                }
            }
        }

        Debug.LogWarning($"Path not found. start={startCell}, end={endCell}, iter={iterations}/{maxIterations}");
        return new Queue<Vector3>();
    }

    private static Queue<Vector3> ReconstructPath(Grid grid, Node endNode)
    {
        List<Vector3> path = new List<Vector3>();
        Node currentNode = endNode;
        while (currentNode != null)
        {
            path.Add(grid.GetCellCenterWorld(currentNode.position));
            currentNode = currentNode.parent;
        }
        path.Reverse();
        return new Queue<Vector3>(path);
    }

    public static float GetDistance(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return D * (dx + dy) + (D2 - 2f * D) * Mathf.Min(dx, dy);
    }

    private static Node GetNodeFromPool(Vector3Int position, Node parent, float gCost, float hCost)
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
        public int heapIndex;

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
            item.heapIndex = Count;
            SortUp(Count);
            Count++;
        }

        public Node Pop()
        {
            Node firstItem = _items[0];
            Count--;
            _items[0] = _items[Count];
            _items[0].heapIndex = 0;
            SortDown(0);
            return firstItem;
        }

        public void UpdateItem(Node item)
        {
            SortUp(item.heapIndex);
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
            
            _items[a].heapIndex = a;
            _items[b].heapIndex = b;
        }

        private void Resize()
        {
            Node[] newItems = new Node[_items.Length * 2];
            Array.Copy(_items, newItems, _items.Length);
            _items = newItems;
        }
    }
}
