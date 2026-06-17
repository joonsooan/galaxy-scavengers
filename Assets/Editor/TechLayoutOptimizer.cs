using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class TechLayoutOptimizer : EditorWindow
{
    private class Edge
    {
        public int from;
        public int to;

        public Edge(int from, int to)
        {
            this.from = from;
            this.to = to;
        }
    }

    private class OptimizerState
    {
        public Dictionary<int, int> nodeRows = new Dictionary<int, int>();

        public OptimizerState Clone()
        {
            OptimizerState clone = new OptimizerState();
            foreach (KeyValuePair<int, int> pair in nodeRows)
            {
                clone.nodeRows[pair.Key] = pair.Value;
            }
            return clone;
        }
    }

    [MenuItem("Tools/Tech Layout Optimizer")]
    public static void ShowWindow()
    {
        GetWindow<TechLayoutOptimizer>("Tech Layout Optimizer");
    }

    [Header("References")]
    [SerializeField] private TechResearchGraphPanel targetPanel;

    [Header("Layout Parameters")]
    [SerializeField] private float spacingX = 240f;
    [SerializeField] private float spacingY = 120f;
    [SerializeField] private int maxRows = 5;
    [SerializeField] private bool centerLayout = true;
    [SerializeField] private float offsetX = 0f;
    [SerializeField] private float offsetY = 0f;

    [Header("Optimization Weights")]
    [SerializeField] private float crossingWeight = 1000f;
    [SerializeField] private float lengthWeight = 10f;
    [SerializeField] private float slopeWeight = 5f;
    [SerializeField] private float occlusionWeight = 5000f;
    [SerializeField] private float centeringWeight = 2f;

    [Header("Agent Simulation Settings")]
    [SerializeField] private float initialTemperature = 100f;
    [SerializeField] private float coolingRate = 0.995f;
    [SerializeField] private bool livePreview = true;

    // Runtime state
    private readonly List<TechDataCell> _cells = new List<TechDataCell>();
    private readonly Dictionary<int, int> _nodeColumns = new Dictionary<int, int>();
    private readonly Dictionary<int, List<int>> _nodesByColumn = new Dictionary<int, List<int>>();
    private readonly Dictionary<TechDataCell, Vector2> _originalPositions = new Dictionary<TechDataCell, Vector2>();
    private readonly List<float> _fitnessHistory = new List<float>();

    private OptimizerState _currentState;
    private OptimizerState _bestState;
    private float _currentCost = float.MaxValue;
    private float _bestCost = float.MaxValue;
    private float _temp = 0f;
    private bool _isOptimizing = false;
    private int _generations = 0;
    private Vector2 _scrollPos;

    private void OnEnable()
    {
        EditorApplication.update += UpdateOptimization;
        LoadSettings();
        // Auto-detect target panel in the scene if null
        if (targetPanel == null)
        {
            targetPanel = FindFirstObjectByType<TechResearchGraphPanel>();
        }
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdateOptimization;
        SaveSettings();
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        GUILayout.Label("Tech UI Layout Optimizer Agent", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 1. References
        EditorGUILayout.LabelField("Target Reference", EditorStyles.boldLabel);
        TechResearchGraphPanel prevPanel = targetPanel;
        targetPanel = EditorGUILayout.ObjectField("Graph Panel", targetPanel, typeof(TechResearchGraphPanel), true) as TechResearchGraphPanel;
        if (targetPanel != prevPanel)
        {
            StopOptimization();
            _originalPositions.Clear();
            _fitnessHistory.Clear();
        }

        if (targetPanel == null)
        {
            EditorGUILayout.HelpBox("Please assign a TechResearchGraphPanel from the active scene.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();

        // 2. Layout settings
        EditorGUILayout.LabelField("Grid Parameters", EditorStyles.boldLabel);
        spacingX = EditorGUILayout.FloatField("Spacing X (Column)", spacingX);
        spacingY = EditorGUILayout.FloatField("Spacing Y (Row)", spacingY);
        maxRows = EditorGUILayout.IntSlider("Max Rows", maxRows, 1, 10);
        centerLayout = EditorGUILayout.Toggle("Center Layout", centerLayout);
        offsetX = EditorGUILayout.FloatField("Offset X", offsetX);
        offsetY = EditorGUILayout.FloatField("Offset Y", offsetY);

        EditorGUILayout.Space();

        // 3. Optimization Weights
        EditorGUILayout.LabelField("Optimization Weights (Agent Cost)", EditorStyles.boldLabel);
        crossingWeight = EditorGUILayout.FloatField("Line Crossing Penalty", crossingWeight);
        lengthWeight = EditorGUILayout.FloatField("Line Length Penalty (Sq)", lengthWeight);
        slopeWeight = EditorGUILayout.FloatField("Slope Penalty (Abs)", slopeWeight);
        occlusionWeight = EditorGUILayout.FloatField("Node Occlusion Penalty", occlusionWeight);
        centeringWeight = EditorGUILayout.FloatField("Centering Balance Bonus", centeringWeight);

        EditorGUILayout.Space();

        // 4. Learning parameters
        EditorGUILayout.LabelField("Optimizer Agent Settings", EditorStyles.boldLabel);
        initialTemperature = EditorGUILayout.FloatField("Initial Temperature", initialTemperature);
        coolingRate = EditorGUILayout.Slider("Cooling Rate (Alpha)", coolingRate, 0.9f, 0.999f);
        livePreview = EditorGUILayout.Toggle("Realtime Live Preview", livePreview);

        EditorGUILayout.Space();

        // 5. Controls
        EditorGUILayout.LabelField("Simulation Controls", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (!_isOptimizing)
            {
                if (GUILayout.Button("Start Learning/Optimization", GUILayout.Height(30)))
                {
                    StartOptimization();
                }
            }
            else
            {
                if (GUILayout.Button("Pause Optimization", GUILayout.Height(30)))
                {
                    StopOptimization();
                }
            }

            if (GUILayout.Button("Reset to Original", GUILayout.Height(30)))
            {
                StopOptimization();
                RestoreOriginalPositions();
            }
        }

        if (_bestState != null && !_isOptimizing)
        {
            if (GUILayout.Button("Apply Best Found Layout", GUILayout.Height(30)))
            {
                ApplyLayoutState(_bestState);
            }
        }

        EditorGUILayout.Space();

        // 6. Statistics
        EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"State: {(_isOptimizing ? "Learning (Optimizing)..." : "Idle")}");
        EditorGUILayout.LabelField($"Generations Evaluated: {_generations}");
        EditorGUILayout.LabelField($"Best Fitness Score: {(_bestCost == float.MaxValue ? "N/A" : _bestCost.ToString("F1"))}");
        EditorGUILayout.LabelField($"Current Temperature: {_temp:F3}");

        EditorGUILayout.Space();

        // 7. Graph Rendering
        DrawFitnessGraph();

        EditorGUILayout.EndScrollView();

        if (EditorGUI.EndChangeCheck())
        {
            SaveSettings();
        }
    }

    private void StartOptimization()
    {
        GatherCells();
        if (_cells.Count == 0)
        {
            Debug.LogWarning("No TechDataCells found under the target Graph Panel.");
            return;
        }

        // Store positions for reset if we haven't already
        if (_originalPositions.Count == 0)
        {
            StoreOriginalPositions();
        }

        CalculateColumns();
        _currentState = InitializeState();
        _currentCost = CalculateCost(_currentState);
        _bestState = _currentState.Clone();
        _bestCost = _currentCost;

        _temp = initialTemperature;
        _generations = 0;
        _fitnessHistory.Clear();
        _fitnessHistory.Add(_bestCost);
        _isOptimizing = true;
    }

    private void StopOptimization()
    {
        _isOptimizing = false;
    }

    private void UpdateOptimization()
    {
        if (!_isOptimizing) return;

        int stepsPerFrame = 500;
        for (int i = 0; i < stepsPerFrame; i++)
        {
            RunOptimizationStep(ref _currentState, ref _currentCost, ref _temp);
            _temp *= coolingRate;

            if (_temp < 0.001f)
            {
                _isOptimizing = false;
                ApplyLayoutState(_bestState);
                Debug.Log($"Tech UI layout optimization completed. Best Cost: {_bestCost}");
                break;
            }
        }

        _generations++;
        _fitnessHistory.Add(_bestCost);
        if (_fitnessHistory.Count > 300)
        {
            _fitnessHistory.RemoveAt(0);
        }

        Repaint();
    }

    private void RunOptimizationStep(ref OptimizerState currentState, ref float currentCost, ref float temp)
    {
        if (_nodesByColumn.Count == 0) return;

        OptimizerState candidate = currentState.Clone();

        // Pick a random column
        List<int> cols = new List<int>(_nodesByColumn.Keys);
        int randomCol = cols[UnityEngine.Random.Range(0, cols.Count)];
        List<int> colNodes = _nodesByColumn[randomCol];

        if (colNodes.Count == 0) return;

        // Mutate: either swap two row positions, or move a node to a free row position in this column
        if (colNodes.Count >= 2 && UnityEngine.Random.value < 0.5f)
        {
            int idx1 = UnityEngine.Random.Range(0, colNodes.Count);
            int idx2 = UnityEngine.Random.Range(0, colNodes.Count);
            while (idx2 == idx1)
            {
                idx2 = UnityEngine.Random.Range(0, colNodes.Count);
            }

            int n1 = colNodes[idx1];
            int n2 = colNodes[idx2];

            int r1 = candidate.nodeRows[n1];
            int r2 = candidate.nodeRows[n2];

            candidate.nodeRows[n1] = r2;
            candidate.nodeRows[n2] = r1;
        }
        else
        {
            int n = colNodes[UnityEngine.Random.Range(0, colNodes.Count)];
            HashSet<int> usedRows = new HashSet<int>();
            foreach (int nodeIdx in colNodes)
            {
                usedRows.Add(candidate.nodeRows[nodeIdx]);
            }

            List<int> freeRows = new List<int>();
            for (int r = 0; r < maxRows; r++)
            {
                if (!usedRows.Contains(r))
                {
                    freeRows.Add(r);
                }
            }

            if (freeRows.Count > 0)
            {
                int newRow = freeRows[UnityEngine.Random.Range(0, freeRows.Count)];
                candidate.nodeRows[n] = newRow;
            }
        }

        float candidateCost = CalculateCost(candidate);
        float delta = candidateCost - currentCost;

        // Metropolis acceptance criteria
        if (delta < 0 || UnityEngine.Random.value < Mathf.Exp(-delta / temp))
        {
            currentState = candidate;
            currentCost = candidateCost;

            if (currentCost < _bestCost)
            {
                _bestCost = currentCost;
                _bestState = currentState.Clone();
                if (livePreview)
                {
                    ApplyLayoutState(_bestState);
                }
            }
        }
    }

    private float CalculateCost(OptimizerState state)
    {
        float cost = 0f;
        int crossings = 0;
        float totalLengthSq = 0f;
        float totalSlope = 0f;

        List<Edge> edges = new List<Edge>();
        foreach (TechDataCell cell in _cells)
        {
            int u = cell.TechData.techIndex;
            if (cell.TechData.successorTechIndices != null)
            {
                foreach (int v in cell.TechData.successorTechIndices)
                {
                    if (state.nodeRows.ContainsKey(u) && state.nodeRows.ContainsKey(v))
                    {
                        edges.Add(new Edge(u, v));
                    }
                }
            }
        }

        for (int i = 0; i < edges.Count; i++)
        {
            Edge e1 = edges[i];
            int u1 = e1.from;
            int v1 = e1.to;
            Vector2 a = new Vector2(_nodeColumns[u1], state.nodeRows[u1]);
            Vector2 b = new Vector2(_nodeColumns[v1], state.nodeRows[v1]);

            float dy = b.y - a.y;
            totalLengthSq += dy * dy;
            totalSlope += Mathf.Abs(dy);

            for (int j = i + 1; j < edges.Count; j++)
            {
                Edge e2 = edges[j];
                int u2 = e2.from;
                int v2 = e2.to;

                Vector2 c = new Vector2(_nodeColumns[u2], state.nodeRows[u2]);
                Vector2 d = new Vector2(_nodeColumns[v2], state.nodeRows[v2]);

                if (AreSegmentsCrossing(a, b, c, d))
                {
                    crossings++;
                }
            }

            // Occlusion check: line from A to B shouldn't pass directly through another node w in intermediate column
            int minCol = Mathf.Min(_nodeColumns[u1], _nodeColumns[v1]);
            int maxCol = Mathf.Max(_nodeColumns[u1], _nodeColumns[v1]);

            if (maxCol - minCol > 1)
            {
                foreach (KeyValuePair<int, int> pair in state.nodeRows)
                {
                    int w = pair.Key;
                    int wCol = _nodeColumns[w];
                    if (wCol > minCol && wCol < maxCol)
                    {
                        int wRow = pair.Value;
                        float t = (float)(wCol - minCol) / (maxCol - minCol);
                        float lineY = a.y + t * (b.y - a.y);

                        if (Mathf.Abs(lineY - wRow) < 0.1f)
                        {
                            cost += occlusionWeight;
                        }
                    }
                }
            }
        }

        cost += crossings * crossingWeight;
        cost += totalLengthSq * lengthWeight;
        cost += totalSlope * slopeWeight;

        // Soft penalty for row alignment to center row
        float middleRow = (maxRows - 1) * 0.5f;
        foreach (KeyValuePair<int, int> pair in state.nodeRows)
        {
            int row = pair.Value;
            float diff = row - middleRow;
            cost += diff * diff * centeringWeight;
        }

        // Hard Overlap Penalty
        foreach (KeyValuePair<int, List<int>> pair in _nodesByColumn)
        {
            List<int> colNodes = pair.Value;
            HashSet<int> seenRows = new HashSet<int>();
            foreach (int nodeIdx in colNodes)
            {
                if (state.nodeRows.TryGetValue(nodeIdx, out int row))
                {
                    if (seenRows.Contains(row))
                    {
                        cost += 100000f; // Extreme penalty for same column & same row overlap
                    }
                    seenRows.Add(row);
                }
            }
        }

        return cost;
    }

    private static bool AreSegmentsCrossing(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (a == c || a == d || b == c || b == d) return false;

        float d1 = CrossProduct(b - a, c - a);
        float d2 = CrossProduct(b - a, d - a);
        float d3 = CrossProduct(d - c, a - c);
        float d4 = CrossProduct(d - c, b - c);

        return (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
                ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f)));
    }

    private static float CrossProduct(Vector2 u, Vector2 v)
    {
        return u.x * v.y - u.y * v.x;
    }

    private void GatherCells()
    {
        if (targetPanel == null) return;
        _cells.Clear();
        TechDataCell[] foundCells = targetPanel.GetComponentsInChildren<TechDataCell>(true);
        HashSet<int> seenIndices = new HashSet<int>();
        foreach (TechDataCell cell in foundCells)
        {
            if (cell != null && cell.TechData != null)
            {
                int idx = cell.TechData.techIndex;
                if (seenIndices.Contains(idx))
                {
                    Debug.LogWarning($"Duplicate TechDataCell found in hierarchy with techIndex: {idx}. Skipping to prevent overlap.");
                    continue;
                }
                seenIndices.Add(idx);
                _cells.Add(cell);
            }
        }
    }

    private void CalculateColumns()
    {
        _nodeColumns.Clear();
        _nodesByColumn.Clear();

        if (_cells.Count == 0) return;

        Dictionary<int, HashSet<int>> prerequisitesMap = new Dictionary<int, HashSet<int>>();
        Dictionary<int, HashSet<int>> successorsMap = new Dictionary<int, HashSet<int>>();
        HashSet<int> activeNodeIndices = new HashSet<int>();

        foreach (TechDataCell cell in _cells)
        {
            int idx = cell.TechData.techIndex;
            activeNodeIndices.Add(idx);
            prerequisitesMap[idx] = new HashSet<int>();
            successorsMap[idx] = new HashSet<int>();
        }

        foreach (TechDataCell cell in _cells)
        {
            int idx = cell.TechData.techIndex;
            if (cell.TechData.prerequisiteTechIndices != null)
            {
                foreach (int prereq in cell.TechData.prerequisiteTechIndices)
                {
                    if (activeNodeIndices.Contains(prereq))
                    {
                        prerequisitesMap[idx].Add(prereq);
                    }
                }
            }
            if (cell.TechData.successorTechIndices != null)
            {
                foreach (int succ in cell.TechData.successorTechIndices)
                {
                    if (activeNodeIndices.Contains(succ))
                    {
                        successorsMap[idx].Add(succ);
                    }
                }
            }
        }

        Dictionary<int, int> nodeCols = new Dictionary<int, int>();
        foreach (int idx in activeNodeIndices)
        {
            nodeCols[idx] = 0;
        }

        int nodeCount = activeNodeIndices.Count;
        bool changed = true;
        int iterations = 0;

        while (changed && iterations < nodeCount)
        {
            changed = false;
            iterations++;

            foreach (int u in activeNodeIndices)
            {
                foreach (int v in successorsMap[u])
                {
                    int expectedCol = nodeCols[u] + 1;
                    if (nodeCols[v] < expectedCol)
                    {
                        nodeCols[v] = expectedCol;
                        changed = true;
                    }
                }
            }
        }

        // Split overloaded columns so no column exceeds maxRows
        EnforceMaxRowsPerColumn(nodeCols, successorsMap, activeNodeIndices);

        // Pull non-root nodes left to minimise edge column-span
        CompressColumns(nodeCols, prerequisitesMap, activeNodeIndices);

        // Push root nodes (no predecessors) right to sit directly before their earliest successor
        PositionRootNodes(nodeCols, prerequisitesMap, successorsMap, activeNodeIndices);

        foreach (KeyValuePair<int, int> pair in nodeCols)
        {
            int idx = pair.Key;
            int col = pair.Value;
            _nodeColumns[idx] = col;

            if (!_nodesByColumn.ContainsKey(col))
            {
                _nodesByColumn[col] = new List<int>();
            }
            _nodesByColumn[col].Add(idx);
        }
    }

    private void PositionRootNodes(Dictionary<int, int> nodeCols, Dictionary<int, HashSet<int>> predecessorsMap, Dictionary<int, HashSet<int>> successorsMap, HashSet<int> activeNodeIndices)
    {
        // Root nodes have no active predecessors, so CompressColumns skips them.
        // Push each root right to sit exactly 1 column before its closest successor,
        // eliminating large gaps between column 0 and where successors actually land.
        bool anyMoved = true;
        while (anyMoved)
        {
            anyMoved = false;

            Dictionary<int, int> colCounts = new Dictionary<int, int>();
            foreach (int idx in activeNodeIndices)
            {
                int c = nodeCols[idx];
                colCounts[c] = colCounts.ContainsKey(c) ? colCounts[c] + 1 : 1;
            }

            foreach (int v in activeNodeIndices.OrderBy(n => nodeCols[n]))
            {
                // Only root nodes (no active predecessors)
                bool isRoot = true;
                if (predecessorsMap.ContainsKey(v))
                {
                    foreach (int u in predecessorsMap[v])
                    {
                        if (nodeCols.ContainsKey(u)) { isRoot = false; break; }
                    }
                }
                if (!isRoot) continue;
                if (!successorsMap.ContainsKey(v) || successorsMap[v].Count == 0) continue;

                // Target: directly left of the closest successor
                int minSuccCol = int.MaxValue;
                foreach (int s in successorsMap[v])
                {
                    if (nodeCols.ContainsKey(s) && nodeCols[s] < minSuccCol)
                        minSuccCol = nodeCols[s];
                }
                if (minSuccCol == int.MaxValue) continue;

                int idealCol  = minSuccCol - 1;
                int currentCol = nodeCols[v];
                if (idealCol <= currentCol) continue; // already adjacent or at 0

                // Find the rightmost column with room in [currentCol+1, idealCol]
                for (int c = idealCol; c > currentCol; c--)
                {
                    int cnt = colCounts.ContainsKey(c) ? colCounts[c] : 0;
                    if (cnt < maxRows)
                    {
                        colCounts[currentCol]--;
                        colCounts[c] = cnt + 1;
                        nodeCols[v] = c;
                        anyMoved = true;
                        break;
                    }
                }
            }
        }
    }

    private void CompressColumns(Dictionary<int, int> nodeCols, Dictionary<int, HashSet<int>> predecessorsMap, HashSet<int> activeNodeIndices)
    {
        // Iteratively pull each node left to max(predecessors.col)+1 while column has room.
        // Multiple passes cascade compression through the DAG.
        int maxPasses = activeNodeIndices.Count;
        for (int pass = 0; pass < maxPasses; pass++)
        {
            // Rebuild column counts each pass (nodes may have moved)
            Dictionary<int, int> colCounts = new Dictionary<int, int>();
            foreach (int idx in activeNodeIndices)
            {
                int c = nodeCols[idx];
                colCounts[c] = colCounts.ContainsKey(c) ? colCounts[c] + 1 : 1;
            }

            bool anyMoved = false;
            foreach (int v in activeNodeIndices.OrderBy(n => nodeCols[n]).ThenBy(n => n))
            {
                if (!predecessorsMap.ContainsKey(v) || predecessorsMap[v].Count == 0) continue;

                int maxPredCol = -1;
                foreach (int u in predecessorsMap[v])
                {
                    if (nodeCols.ContainsKey(u) && nodeCols[u] > maxPredCol)
                        maxPredCol = nodeCols[u];
                }
                if (maxPredCol < 0) continue;

                int idealCol  = maxPredCol + 1;
                int currentCol = nodeCols[v];
                if (idealCol >= currentCol) continue; // already optimal or can't go left

                int targetCount = colCounts.ContainsKey(idealCol) ? colCounts[idealCol] : 0;
                if (targetCount >= maxRows) continue; // target column is full

                // Move v left
                colCounts[currentCol]--;
                colCounts[idealCol] = targetCount + 1;
                nodeCols[v] = idealCol;
                anyMoved = true;
            }

            if (!anyMoved) break;
        }
    }

    private void EnforceMaxRowsPerColumn(Dictionary<int, int> nodeCols, Dictionary<int, HashSet<int>> successorsMap, HashSet<int> activeNodeIndices)
    {
        int maxPasses = activeNodeIndices.Count + 1;
        for (int pass = 0; pass < maxPasses; pass++)
        {
            // Group nodes by current column
            Dictionary<int, List<int>> byCol = new Dictionary<int, List<int>>();
            foreach (int idx in activeNodeIndices)
            {
                int c = nodeCols[idx];
                if (!byCol.ContainsKey(c)) byCol[c] = new List<int>();
                byCol[c].Add(idx);
            }

            // Find the leftmost column that exceeds maxRows
            int overloadedCol = -1;
            foreach (int c in byCol.Keys.OrderBy(x => x))
            {
                if (byCol[c].Count > maxRows)
                {
                    overloadedCol = c;
                    break;
                }
            }
            if (overloadedCol < 0) return; // All columns are within the limit

            // Shift every node at columns > overloadedCol one step right to make room
            foreach (int idx in activeNodeIndices)
            {
                if (nodeCols[idx] > overloadedCol)
                    nodeCols[idx]++;
            }

            // Move overflow nodes (beyond maxRows) from the overloaded column into the new slot
            List<int> colNodes = byCol[overloadedCol].OrderBy(n => n).ToList();
            for (int i = maxRows; i < colNodes.Count; i++)
                nodeCols[colNodes[i]] = overloadedCol + 1;

            // Re-enforce topological constraint: every successor must be strictly right of its predecessor
            bool changed = true;
            int innerPasses = 0;
            while (changed && innerPasses++ < activeNodeIndices.Count)
            {
                changed = false;
                foreach (int u in activeNodeIndices)
                {
                    if (!successorsMap.ContainsKey(u)) continue;
                    foreach (int v in successorsMap[u])
                    {
                        if (!nodeCols.ContainsKey(v)) continue;
                        int required = nodeCols[u] + 1;
                        if (nodeCols[v] < required)
                        {
                            nodeCols[v] = required;
                            changed = true;
                        }
                    }
                }
            }
        }
    }

    private OptimizerState InitializeState()
    {
        OptimizerState state = new OptimizerState();
        foreach (KeyValuePair<int, List<int>> pair in _nodesByColumn)
        {
            int col = pair.Key;
            List<int> nodes = pair.Value;

            List<int> availableRows = new List<int>();
            for (int r = 0; r < maxRows; r++)
            {
                availableRows.Add(r);
            }

            for (int i = availableRows.Count - 1; i > 0; i--)
            {
                int rIdx = UnityEngine.Random.Range(0, i + 1);
                int temp = availableRows[i];
                availableRows[i] = availableRows[rIdx];
                availableRows[rIdx] = temp;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                int nodeIdx = nodes[i];
                state.nodeRows[nodeIdx] = availableRows[i % availableRows.Count];
            }
        }
        return state;
    }

    private void ApplyLayoutState(OptimizerState state)
    {
        if (targetPanel == null || state == null || _cells.Count == 0) return;

        Transform[] transforms = _cells.Select(c => c.transform).ToArray();
        Undo.RecordObjects(transforms, "Optimize Tech Layout");

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        Dictionary<TechDataCell, Vector2> localPos = new Dictionary<TechDataCell, Vector2>();

        foreach (TechDataCell cell in _cells)
        {
            int idx = cell.TechData.techIndex;
            if (state.nodeRows.ContainsKey(idx) && _nodeColumns.ContainsKey(idx))
            {
                int col = _nodeColumns[idx];
                int row = state.nodeRows[idx];

                float x = col * spacingX;
                float y = -row * spacingY;

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);

                localPos[cell] = new Vector2(x, y);
            }
        }

        Vector2 offset = new Vector2(offsetX, offsetY);
        if (centerLayout && _cells.Count > 0)
        {
            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;
            offset -= new Vector2(centerX, centerY);
        }

        foreach (KeyValuePair<TechDataCell, Vector2> pair in localPos)
        {
            RectTransform rt = pair.Key.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = pair.Value + offset;
                EditorUtility.SetDirty(rt);
            }
        }

        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }

    private void StoreOriginalPositions()
    {
        _originalPositions.Clear();
        foreach (TechDataCell cell in _cells)
        {
            RectTransform rt = cell.GetComponent<RectTransform>();
            if (rt != null)
            {
                _originalPositions[cell] = rt.anchoredPosition;
            }
        }
    }

    private void RestoreOriginalPositions()
    {
        if (_originalPositions.Count == 0) return;

        Transform[] transforms = _cells.Select(c => c.transform).ToArray();
        Undo.RecordObjects(transforms, "Reset Tech Layout");

        foreach (KeyValuePair<TechDataCell, Vector2> pair in _originalPositions)
        {
            RectTransform rt = pair.Key.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = pair.Value;
                EditorUtility.SetDirty(rt);
            }
        }
    }

    private void DrawFitnessGraph()
    {
        if (_fitnessHistory.Count < 2)
        {
            EditorGUILayout.HelpBox("Run learning simulation to visualize cost optimization graph.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Agent Learning Curve (Cost/Fitness History):", EditorStyles.boldLabel);
        Rect rect = GUILayoutUtility.GetRect(10, 300, 150, 150);
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));

        float minF = float.MaxValue;
        float maxF = float.MinValue;
        foreach (float f in _fitnessHistory)
        {
            if (f < minF) minF = f;
            if (f > maxF) maxF = f;
        }

        float diff = maxF - minF;
        if (diff < 1f) diff = 1f;
        minF -= diff * 0.05f;
        maxF += diff * 0.05f;

        // Draw horizontal grid lines
        Handles.color = new Color(0.25f, 0.25f, 0.25f, 0.4f);
        for (int i = 1; i <= 4; i++)
        {
            float y = rect.y + rect.height * (i / 5f);
            Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
        }

        // Draw learning curve path
        Handles.color = new Color(0.1f, 0.85f, 0.2f, 0.9f);
        Vector3[] points = new Vector3[_fitnessHistory.Count];
        for (int i = 0; i < _fitnessHistory.Count; i++)
        {
            float tX = (float)i / (_fitnessHistory.Count - 1);
            float tY = (_fitnessHistory[i] - minF) / (maxF - minF);

            float x = rect.x + tX * rect.width;
            float y = rect.yMax - tY * rect.height;

            points[i] = new Vector3(x, y, 0f);
        }

        Handles.DrawAAPolyLine(2.5f, points);

        // Render min/max values
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 5, rect.y + 2, 120, 20), $"Max Cost: {maxF:F1}", EditorStyles.miniLabel);
        GUI.Label(new Rect(rect.x + 5, rect.yMax - 18, 120, 20), $"Min Cost: {minF:F1}", EditorStyles.miniLabel);
    }

    private void SaveSettings()
    {
        string prefix = "TechLayoutOptimizer.";
        EditorPrefs.SetFloat(prefix + "spacingX", spacingX);
        EditorPrefs.SetFloat(prefix + "spacingY", spacingY);
        EditorPrefs.SetInt(prefix + "maxRows", maxRows);
        EditorPrefs.SetBool(prefix + "centerLayout", centerLayout);
        EditorPrefs.SetFloat(prefix + "offsetX", offsetX);
        EditorPrefs.SetFloat(prefix + "offsetY", offsetY);
        EditorPrefs.SetFloat(prefix + "crossingWeight", crossingWeight);
        EditorPrefs.SetFloat(prefix + "lengthWeight", lengthWeight);
        EditorPrefs.SetFloat(prefix + "slopeWeight", slopeWeight);
        EditorPrefs.SetFloat(prefix + "occlusionWeight", occlusionWeight);
        EditorPrefs.SetFloat(prefix + "centeringWeight", centeringWeight);
        EditorPrefs.SetFloat(prefix + "initialTemperature", initialTemperature);
        EditorPrefs.SetFloat(prefix + "coolingRate", coolingRate);
        EditorPrefs.SetBool(prefix + "livePreview", livePreview);
    }

    private void LoadSettings()
    {
        string prefix = "TechLayoutOptimizer.";
        if (EditorPrefs.HasKey(prefix + "spacingX")) spacingX = EditorPrefs.GetFloat(prefix + "spacingX");
        if (EditorPrefs.HasKey(prefix + "spacingY")) spacingY = EditorPrefs.GetFloat(prefix + "spacingY");
        if (EditorPrefs.HasKey(prefix + "maxRows")) maxRows = EditorPrefs.GetInt(prefix + "maxRows");
        if (EditorPrefs.HasKey(prefix + "centerLayout")) centerLayout = EditorPrefs.GetBool(prefix + "centerLayout");
        if (EditorPrefs.HasKey(prefix + "offsetX")) offsetX = EditorPrefs.GetFloat(prefix + "offsetX");
        if (EditorPrefs.HasKey(prefix + "offsetY")) offsetY = EditorPrefs.GetFloat(prefix + "offsetY");
        if (EditorPrefs.HasKey(prefix + "crossingWeight")) crossingWeight = EditorPrefs.GetFloat(prefix + "crossingWeight");
        if (EditorPrefs.HasKey(prefix + "lengthWeight")) lengthWeight = EditorPrefs.GetFloat(prefix + "lengthWeight");
        if (EditorPrefs.HasKey(prefix + "slopeWeight")) slopeWeight = EditorPrefs.GetFloat(prefix + "slopeWeight");
        if (EditorPrefs.HasKey(prefix + "occlusionWeight")) occlusionWeight = EditorPrefs.GetFloat(prefix + "occlusionWeight");
        if (EditorPrefs.HasKey(prefix + "centeringWeight")) centeringWeight = EditorPrefs.GetFloat(prefix + "centeringWeight");
        if (EditorPrefs.HasKey(prefix + "initialTemperature")) initialTemperature = EditorPrefs.GetFloat(prefix + "initialTemperature");
        if (EditorPrefs.HasKey(prefix + "coolingRate")) coolingRate = EditorPrefs.GetFloat(prefix + "coolingRate");
        if (EditorPrefs.HasKey(prefix + "livePreview")) livePreview = EditorPrefs.GetBool(prefix + "livePreview");
    }
}
