using System.Collections.Generic;
using UnityEngine;

public static class PowerGridConnectivity
{
    public static HashSet<Vector3Int> ComputePoweredCells(IReadOnlyList<IPowerGridNode> nodes)
    {
        HashSet<Vector3Int> powered = new HashSet<Vector3Int>();
        if (nodes == null || nodes.Count == 0) {
            return powered;
        }

        List<BoundsInt> boundsList = new List<BoundsInt>(nodes.Count);
        List<bool> isSourceFlags = new List<bool>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++) {
            IPowerGridNode node = nodes[i];
            boundsList.Add(node.GetPowerCoverageBounds());
            isSourceFlags.Add(node.IsActivePowerSource());
        }

        int n = nodes.Count;
        List<int>[] adj = new List<int>[n];
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

        bool[] visited = new bool[n];
        for (int start = 0; start < n; start++) {
            if (visited[start]) continue;

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
}
