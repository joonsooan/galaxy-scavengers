using System.Collections.Generic;
using UnityEngine;

public static class PowerGridConnectivity
{
    private static void BuildGraph(IReadOnlyList<IPowerGridNode> nodes, out List<BoundsInt> boundsList, out List<bool> isSourceFlags, out List<int>[] adj)
    {
        int n = nodes.Count;
        boundsList = new List<BoundsInt>(n);
        isSourceFlags = new List<bool>(n);
        for (int i = 0; i < n; i++) {
            IPowerGridNode node = nodes[i];
            boundsList.Add(node.GetPowerCoverageBounds());
            isSourceFlags.Add(node.IsActivePowerSource());
        }

        adj = new List<int>[n];
        for (int i = 0; i < n; i++) {
            adj[i] = new List<int>();
        }

        for (int i = 0; i < n; i++) {
            for (int j = i + 1; j < n; j++) {
                if (PowerGridGeometry.CoverageRangesTouchOrOverlap(boundsList[i], boundsList[j])) {
                    adj[i].Add(j);
                    adj[j].Add(i);
                }
            }
        }
    }

    public static HashSet<Vector3Int> ComputePoweredCells(IReadOnlyList<IPowerGridNode> nodes)
    {
        HashSet<Vector3Int> powered = new HashSet<Vector3Int>();
        if (nodes == null || nodes.Count == 0) {
            return powered;
        }

        BuildGraph(nodes, out List<BoundsInt> boundsList, out List<bool> isSourceFlags, out List<int>[] adj);

        int n = nodes.Count;
        bool[] visited = new bool[n];
        for (int start = 0; start < n; start++) {
            if (visited[start]) {
                continue;
            }

            bool componentHasSource = false;
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(start);
            visited[start] = true;
            List<int> component = new List<int>();

            while (queue.Count > 0) {
                int u = queue.Dequeue();
                component.Add(u);
                if (isSourceFlags[u]) {
                    componentHasSource = true;
                }

                foreach (int v in adj[u]) {
                    if (!visited[v]) {
                        visited[v] = true;
                        queue.Enqueue(v);
                    }
                }
            }

            if (!componentHasSource) {
                continue;
            }

            foreach (int idx in component) {
                foreach (Vector3Int cell in boundsList[idx].allPositionsWithin) {
                    powered.Add(cell);
                }
            }
        }

        return powered;
    }

    public static bool[] GetNodeConnectedToActiveSourceFlags(IReadOnlyList<IPowerGridNode> nodes)
    {
        int n = nodes == null ? 0 : nodes.Count;
        bool[] connected = new bool[n];
        if (n == 0) {
            return connected;
        }

        BuildGraph(nodes, out List<BoundsInt> boundsList, out List<bool> isSourceFlags, out List<int>[] adj);

        bool[] visited = new bool[n];
        for (int start = 0; start < n; start++) {
            if (visited[start]) {
                continue;
            }

            bool componentHasSource = false;
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(start);
            visited[start] = true;
            List<int> component = new List<int>();

            while (queue.Count > 0) {
                int u = queue.Dequeue();
                component.Add(u);
                if (isSourceFlags[u]) {
                    componentHasSource = true;
                }

                foreach (int v in adj[u]) {
                    if (!visited[v]) {
                        visited[v] = true;
                        queue.Enqueue(v);
                    }
                }
            }

            if (!componentHasSource) {
                continue;
            }

            foreach (int idx in component) {
                connected[idx] = true;
            }
        }

        return connected;
    }

    public static void AnalyzePowerGrid(IReadOnlyList<IPowerGridNode> nodes, out HashSet<Vector3Int> poweredCells,
        out bool[] nodeConnectedToSource, out int[] nodeSourcedComponentId, out List<BoundsInt> nodeBounds)
    {
        poweredCells = new HashSet<Vector3Int>();
        int n = nodes == null ? 0 : nodes.Count;
        nodeConnectedToSource = new bool[n];
        nodeSourcedComponentId = new int[n];
        for (int i = 0; i < n; i++) {
            nodeSourcedComponentId[i] = -1;
        }

        if (n == 0) {
            nodeBounds = new List<BoundsInt>();
            return;
        }

        BuildGraph(nodes, out nodeBounds, out List<bool> isSourceFlags, out List<int>[] adj);

        bool[] visited = new bool[n];
        int nextComponentId = 0;
        for (int start = 0; start < n; start++) {
            if (visited[start]) {
                continue;
            }

            bool componentHasSource = false;
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(start);
            visited[start] = true;
            List<int> component = new List<int>();

            while (queue.Count > 0) {
                int u = queue.Dequeue();
                component.Add(u);
                if (isSourceFlags[u]) {
                    componentHasSource = true;
                }

                foreach (int v in adj[u]) {
                    if (!visited[v]) {
                        visited[v] = true;
                        queue.Enqueue(v);
                    }
                }
            }

            if (!componentHasSource) {
                continue;
            }

            int cid = nextComponentId++;
            foreach (int idx in component) {
                nodeConnectedToSource[idx] = true;
                nodeSourcedComponentId[idx] = cid;
                foreach (Vector3Int cell in nodeBounds[idx].allPositionsWithin) {
                    poweredCells.Add(cell);
                }
            }
        }
    }
}
