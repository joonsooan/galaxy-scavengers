using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TechTreeRebuilder : EditorWindow
{
    public enum ResourceTier
    {
        Tier1 = 0, // Basic
        Tier2 = 1, // Mid-Tier
        Tier3 = 2  // High-Tier
    }

    public enum TechCategory
    {
        Economy,
        Military,
        Production
    }

    public enum StructureMode
    {
        ParallelChains, // Sequential chains per category (Highly structured, no crossings)
        LayeredTree     // Branching tree from Tier 1 -> Tier 2 -> Tier 3
    }

    private class TechNode
    {
        public TechData asset;
        public int index;
        public string name;
        public ResourceTier tier;
        public TechCategory category;
        public List<int> prerequisites = new List<int>();
        public List<int> successors = new List<int>();
    }

    [MenuItem("Tools/Tech Tree Rebuilder")]
    public static void ShowWindow()
    {
        GetWindow<TechTreeRebuilder>("Tech Tree Rebuilder");
    }

    [Header("References")]
    [SerializeField] private TechResearchCatalog catalog;

    [Header("Cell Rebuild")]
    [SerializeField] private GameObject techDataCellPrefab;
    [SerializeField] private Transform cellContent;

    [Header("Structure Configurations")]
    [SerializeField] private StructureMode structureMode = StructureMode.ParallelChains;
    [SerializeField] private int maxRows = 5;
    [SerializeField] private int maxPrereqsPerNode = 1;
    [SerializeField] private bool matchCategories = true;
    [SerializeField] private bool applyTransitiveReduction = true;

    [Header("Processor Gate")]
    [SerializeField] private bool useProcessorGate = true;
    [SerializeField] private int processorGateTechIndex = 7;

    // Resource Tier configuration map
    private readonly Dictionary<ResourceType, ResourceTier> _resourceTiers = new Dictionary<ResourceType, ResourceTier>();

    // UI state
    private Vector2 _scrollPos;
    private readonly List<string> _previewLogs = new List<string>();
    private bool _showTiersFoldout = false;
    private int _redundantConnectionsCount = 0;

    // Swap tool state
    private int _swapIndexA = 0;
    private int _swapIndexB = 1;

    private const string PrefKeyPrefix = "TechTreeRebuilder_";

    private void SaveSettings()
    {
        EditorPrefs.SetInt(PrefKeyPrefix + "structureMode", (int)structureMode);
        EditorPrefs.SetInt(PrefKeyPrefix + "maxRows", maxRows);
        EditorPrefs.SetInt(PrefKeyPrefix + "maxPrereqsPerNode", maxPrereqsPerNode);
        EditorPrefs.SetBool(PrefKeyPrefix + "matchCategories", matchCategories);
        EditorPrefs.SetBool(PrefKeyPrefix + "applyTransitiveReduction", applyTransitiveReduction);
        EditorPrefs.SetBool(PrefKeyPrefix + "useProcessorGate", useProcessorGate);
        EditorPrefs.SetInt(PrefKeyPrefix + "processorGateTechIndex", processorGateTechIndex);
    }

    private void LoadSettings()
    {
        if (EditorPrefs.HasKey(PrefKeyPrefix + "structureMode"))
            structureMode = (StructureMode)EditorPrefs.GetInt(PrefKeyPrefix + "structureMode", (int)structureMode);
        maxRows = EditorPrefs.GetInt(PrefKeyPrefix + "maxRows", maxRows);
        maxPrereqsPerNode = EditorPrefs.GetInt(PrefKeyPrefix + "maxPrereqsPerNode", maxPrereqsPerNode);
        matchCategories = EditorPrefs.GetBool(PrefKeyPrefix + "matchCategories", matchCategories);
        applyTransitiveReduction = EditorPrefs.GetBool(PrefKeyPrefix + "applyTransitiveReduction", applyTransitiveReduction);
        useProcessorGate = EditorPrefs.GetBool(PrefKeyPrefix + "useProcessorGate", useProcessorGate);
        processorGateTechIndex = EditorPrefs.GetInt(PrefKeyPrefix + "processorGateTechIndex", processorGateTechIndex);
    }

    private void OnEnable()
    {
        LoadSettings();
        InitializeDefaultResourceTiers();
        // Auto-detect catalog in project
        if (catalog == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:TechResearchCatalog");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                catalog = AssetDatabase.LoadAssetAtPath<TechResearchCatalog>(path);
            }
        }

        if (techDataCellPrefab == null)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("Tech Data Cell t:Prefab");
            if (prefabGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
                techDataCellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
    }

    private void OnDisable()
    {
        SaveSettings();
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        GUILayout.Label("Tech Tree Relationship Rebuilder", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 1. Reference
        EditorGUILayout.LabelField("Catalog Reference", EditorStyles.boldLabel);
        catalog = EditorGUILayout.ObjectField("Tech Catalog", catalog, typeof(TechResearchCatalog), false) as TechResearchCatalog;

        if (catalog == null)
        {
            EditorGUILayout.HelpBox("Please assign the Tech Research Catalog asset.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();

        // 1b. Cell Rebuild
        EditorGUILayout.LabelField("Cell Rebuild", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            techDataCellPrefab = EditorGUILayout.ObjectField("Cell Prefab", techDataCellPrefab, typeof(GameObject), false) as GameObject;
            cellContent = EditorGUILayout.ObjectField("Content (Scene)", cellContent, typeof(Transform), true) as Transform;

            EditorGUILayout.HelpBox(
                "카탈로그의 Tech 순서대로 Cell Prefab을 Content 아래에 생성하고 TechData를 할당합니다.\n기존 TechDataCell 자식 오브젝트는 모두 삭제됩니다.",
                MessageType.Info);

            bool canRebuildCells = techDataCellPrefab != null && cellContent != null;
            using (new EditorGUI.DisabledScope(!canRebuildCells))
            {
                if (GUILayout.Button("Rebuild Cells in Scene", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Rebuild Cells?",
                        $"Content 하위의 기존 TechDataCell을 모두 삭제하고 카탈로그({catalog.Techs.Count}개) 기준으로 다시 생성합니다.",
                        "Yes", "No"))
                    {
                        RebuildCellsInScene();
                    }
                }
            }
        }

        EditorGUILayout.Space();

        // 2. Resource Tier Mapping Configurations
        _showTiersFoldout = EditorGUILayout.Foldout(_showTiersFoldout, "Resource Tier Assignments", true);
        if (_showTiersFoldout)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                ResourceType[] types = (ResourceType[])Enum.GetValues(typeof(ResourceType));
                foreach (ResourceType type in types)
                {
                    if (type == ResourceType.None || type == ResourceType.Electricity) continue;

                    if (!_resourceTiers.ContainsKey(type))
                    {
                        _resourceTiers[type] = ResourceTier.Tier2;
                    }

                    _resourceTiers[type] = (ResourceTier)EditorGUILayout.EnumPopup(type.ToString(), _resourceTiers[type]);
                }
            }
        }

        EditorGUILayout.Space();

        // 3. Structure Settings
        EditorGUILayout.LabelField("Rebuilder Options", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        structureMode = (StructureMode)EditorGUILayout.EnumPopup("Structuring Mode", structureMode);
        maxRows = EditorGUILayout.IntSlider("Max Rows Limit", maxRows, 1, 10);
        if (structureMode == StructureMode.LayeredTree)
        {
            maxPrereqsPerNode = EditorGUILayout.IntSlider("Max Prerequisites per Node", maxPrereqsPerNode, 1, 3);
            matchCategories = EditorGUILayout.Toggle("Category Matching", matchCategories);
        }
        applyTransitiveReduction = EditorGUILayout.Toggle("Apply Transitive Reduction", applyTransitiveReduction);

        EditorGUILayout.Space();

        // 3b. Processor Gate
        EditorGUILayout.LabelField("Processor Gate", EditorStyles.boldLabel);
        useProcessorGate = EditorGUILayout.Toggle("Enable Processor Gate", useProcessorGate);
        if (useProcessorGate)
        {
            processorGateTechIndex = EditorGUILayout.IntField("Gate Tech Index", processorGateTechIndex);
            EditorGUILayout.HelpBox(
                $"All techs that unlock Tier 2+ resources will be forced to require tech index {processorGateTechIndex} (Processor Drone) as a prerequisite.\n" +
                "This ensures the Processor unit is available before any Tier 2 resource can be researched.",
                MessageType.Info);
        }
        if (EditorGUI.EndChangeCheck()) SaveSettings();

        EditorGUILayout.Space();

        // 4. Actions
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Analyze Current Tree", GUILayout.Height(30)))
            {
                AnalyzeCurrentTree();
            }

            if (GUILayout.Button("Redesign & Save Relationships", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Redesign Tech Tree?",
                    "This will modify prerequisite and successor connections in all TechData assets. Are you sure you want to proceed?", "Yes", "No"))
                {
                    RebuildRelationships();
                }
            }
        }

        EditorGUILayout.Space();

        // 5. Swap Tool
        EditorGUILayout.LabelField("Swap Tech Indices", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "두 tech index를 입력하면 해당 TechData 자산의 techIndex와 모든 선후행 관계(prerequisite/successor)를 서로 맞바꿉니다.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                _swapIndexA = EditorGUILayout.IntField("Index A", _swapIndexA);
                _swapIndexB = EditorGUILayout.IntField("Index B", _swapIndexB);
            }

            if (GUILayout.Button("Swap", GUILayout.Height(28)))
            {
                if (_swapIndexA == _swapIndexB)
                {
                    EditorUtility.DisplayDialog("Invalid Input", "두 인덱스가 같습니다.", "OK");
                }
                else if (EditorUtility.DisplayDialog("Swap Tech Indices?",
                    $"Tech {_swapIndexA}와 Tech {_swapIndexB}의 위치 및 선후행 관계를 교환합니다. 계속하시겠습니까?",
                    "Yes", "No"))
                {
                    SwapTechs(_swapIndexA, _swapIndexB);
                }
            }
        }

        EditorGUILayout.Space();

        // 6. Preview / Logs
        if (_previewLogs.Count > 0)
        {
            EditorGUILayout.LabelField("Analysis & Rebuild Results Preview:", EditorStyles.boldLabel);
            if (_redundantConnectionsCount > 0)
            {
                EditorGUILayout.HelpBox($"Found {_redundantConnectionsCount} redundant connections (e.g. A->B->C and A->C). Transitive reduction will prune these.", MessageType.Info);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (string log in _previewLogs)
                {
                    EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void InitializeDefaultResourceTiers()
    {
        _resourceTiers.Clear();
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (type == ResourceType.None) continue;

            if (type == ResourceType.Ferrite || type == ResourceType.Aether ||
                type == ResourceType.Biomass || type == ResourceType.CryoCrystal ||
                type == ResourceType.Electricity)
            {
                _resourceTiers[type] = ResourceTier.Tier1;
            }
            else if (type == ResourceType.HeavyPlating || type == ResourceType.Actuator ||
                     type == ResourceType.GenomeChip || type == ResourceType.SensorUnit ||
                     type == ResourceType.PlasmaCube || type == ResourceType.CryoConduit ||
                     type == ResourceType.SeekerMissile || type == ResourceType.NexusData ||
                     type == ResourceType.NeuralMatrix || type == ResourceType.PatchKit)
            {
                _resourceTiers[type] = ResourceTier.Tier3;
            }
            else
            {
                _resourceTiers[type] = ResourceTier.Tier2;
            }
        }
    }

    private ResourceTier GetTechResourceTier(TechData tech)
    {
        if (tech == null || tech.researchCosts == null || tech.researchCosts.Length == 0)
        {
            return ResourceTier.Tier1;
        }

        ResourceTier maxTier = ResourceTier.Tier1;
        foreach (ResourceCost cost in tech.researchCosts)
        {
            if (_resourceTiers.TryGetValue(cost.resourceType, out ResourceTier tier))
            {
                if (tier > maxTier)
                {
                    maxTier = tier;
                }
            }
        }
        return maxTier;
    }

    private TechCategory GetTechCategory(TechData tech)
    {
        if (tech == null) return TechCategory.Economy;

        // Classification based on unlocked content and stats
        bool hasUnits = tech.unlocksUnits != null && tech.unlocksUnits.Length > 0;
        bool hasBuildings = tech.unlocksBuildings != null && tech.unlocksBuildings.Length > 0;
        bool hasResources = tech.unlocksResources != null && tech.unlocksResources.Length > 0;

        bool hasCombatStats = false;
        if (tech.grantStatTypes != null)
        {
            foreach (ModuleStatType statType in tech.grantStatTypes)
            {
                string statName = statType.ToString().ToLower();
                if (statName.Contains("damage") || statName.Contains("health") ||
                    statName.Contains("defense") || statName.Contains("shield") ||
                    statName.Contains("weapon") || statName.Contains("attack"))
                {
                    hasCombatStats = true;
                    break;
                }
            }
        }

        if (hasUnits || hasCombatStats)
        {
            return TechCategory.Military;
        }
        if (hasBuildings || hasResources)
        {
            return TechCategory.Production;
        }
        return TechCategory.Economy;
    }

    private List<TechNode> BuildNodesList()
    {
        List<TechNode> nodes = new List<TechNode>();
        if (catalog == null || catalog.Techs == null) return nodes;

        foreach (TechData tech in catalog.Techs)
        {
            if (tech == null) continue;

            TechNode node = new TechNode
            {
                asset = tech,
                index = tech.techIndex,
                name = tech.GetTechName(),
                tier = GetTechResourceTier(tech),
                category = GetTechCategory(tech)
            };
            nodes.Add(node);
        }
        return nodes;
    }

    private void AnalyzeCurrentTree()
    {
        _previewLogs.Clear();
        _redundantConnectionsCount = 0;

        List<TechNode> nodes = BuildNodesList();
        if (nodes.Count == 0)
        {
            _previewLogs.Add("No technologies found in the catalog.");
            return;
        }

        _previewLogs.Add($"--- Catalog Analysis ({nodes.Count} Nodes) ---");

        // Count Tiers
        int t1Count = nodes.Count(n => n.tier == ResourceTier.Tier1);
        int t2Count = nodes.Count(n => n.tier == ResourceTier.Tier2);
        int t3Count = nodes.Count(n => n.tier == ResourceTier.Tier3);
        _previewLogs.Add($"Tiers: Basic (Tier 1) = {t1Count}, Mid (Tier 2) = {t2Count}, High (Tier 3) = {t3Count}");

        // Count Categories
        int ecoCount = nodes.Count(n => n.category == TechCategory.Economy);
        int milCount = nodes.Count(n => n.category == TechCategory.Military);
        int prodCount = nodes.Count(n => n.category == TechCategory.Production);
        _previewLogs.Add($"Themes: Economy = {ecoCount}, Military = {milCount}, Production = {prodCount}");

        // Map nodes for lookups
        Dictionary<int, TechNode> nodeMap = nodes.ToDictionary(n => n.index);

        // Populate current relationships
        foreach (TechNode node in nodes)
        {
            if (node.asset.prerequisiteTechIndices != null)
            {
                foreach (int prereqIdx in node.asset.prerequisiteTechIndices)
                {
                    if (nodeMap.ContainsKey(prereqIdx))
                    {
                        node.prerequisites.Add(prereqIdx);
                        nodeMap[prereqIdx].successors.Add(node.index);
                    }
                }
            }
        }

        // Identify redundant connections (transitive reduction check)
        List<(int from, int to)> redundantEdges = GetRedundantEdges(nodes);
        _redundantConnectionsCount = redundantEdges.Count;

        if (_redundantConnectionsCount > 0)
        {
            _previewLogs.Add($"Redundant Connections detected: {_redundantConnectionsCount}");
            foreach (var edge in redundantEdges.Take(5))
            {
                string fromName = nodeMap[edge.from].name;
                string toName = nodeMap[edge.to].name;
                _previewLogs.Add($"  * {fromName} -> {toName} is redundant (transitive)");
            }
            if (_redundantConnectionsCount > 5)
            {
                _previewLogs.Add("  * ... and more.");
            }
        }
        else
        {
            _previewLogs.Add("No redundant connections found. The dependency graph is clean!");
        }

        // Check for progression violations
        int tierViolations = 0;
        foreach (TechNode node in nodes)
        {
            foreach (int prereqIdx in node.prerequisites)
            {
                if (nodeMap.TryGetValue(prereqIdx, out TechNode prereq))
                {
                    if (prereq.tier > node.tier)
                    {
                        tierViolations++;
                        _previewLogs.Add($"Tier Violation: Prerequisite '{prereq.name}' (Tier {prereq.tier}) is higher than '{node.name}' (Tier {node.tier})");
                    }
                }
            }
        }

        if (tierViolations == 0)
        {
            _previewLogs.Add("No tier progression violations found.");
        }

        // Check Processor Gate integrity
        if (useProcessorGate)
        {
            _previewLogs.Add("--- Processor Gate Analysis ---");
            if (!nodeMap.TryGetValue(processorGateTechIndex, out TechNode gateNode))
            {
                _previewLogs.Add($"[Gate] WARNING: No tech with index {processorGateTechIndex} found.");
            }
            else
            {
                int gateViolations = 0;
                foreach (TechNode node in nodes)
                {
                    if (node.index == processorGateTechIndex) continue;
                    if (node.asset.unlocksResources == null || node.asset.unlocksResources.Length == 0) continue;

                    bool unlocksT2 = false;
                    foreach (ResourceType rt in node.asset.unlocksResources)
                    {
                        if (_resourceTiers.TryGetValue(rt, out ResourceTier tier) && tier >= ResourceTier.Tier2)
                        {
                            unlocksT2 = true;
                            break;
                        }
                    }

                    if (!unlocksT2) continue;

                    bool hasGateAsPrereq = node.prerequisites.Contains(processorGateTechIndex);
                    bool isReachable = false;
                    if (!hasGateAsPrereq)
                    {
                        // Check if reachable transitively
                        Dictionary<int, List<int>> adj = nodes.ToDictionary(n => n.index, n => new List<int>(n.prerequisites));
                        isReachable = IsReachable(processorGateTechIndex, node.index, adj);
                    }

                    if (!hasGateAsPrereq && !isReachable)
                    {
                        gateViolations++;
                        _previewLogs.Add($"[Gate] VIOLATION: '{node.name}' unlocks Tier 2 resources but does NOT require '{gateNode.name}' (directly or transitively).");
                    }
                }

                if (gateViolations == 0)
                {
                    _previewLogs.Add($"[Gate] OK: All Tier 2 resource unlock techs properly require '{gateNode.name}'.");
                }
                else
                {
                    _previewLogs.Add($"[Gate] {gateViolations} violation(s) found. Run 'Redesign & Save Relationships' to fix.");
                }

                // Check that gate tech itself has no Tier 2 costs
                bool gateHasT2Cost = false;
                if (gateNode.asset.researchCosts != null)
                {
                    foreach (ResourceCost cost in gateNode.asset.researchCosts)
                    {
                        if (_resourceTiers.TryGetValue(cost.resourceType, out ResourceTier costTier) && costTier >= ResourceTier.Tier2)
                        {
                            gateHasT2Cost = true;
                            break;
                        }
                    }
                }

                if (gateHasT2Cost)
                {
                    _previewLogs.Add($"[Gate] WARNING: '{gateNode.name}' has Tier 2 resource costs — this creates a circular dependency!");
                }
                else
                {
                    _previewLogs.Add($"[Gate] OK: '{gateNode.name}' costs only Tier 1 resources.");
                }
            }
        }
    }

    private void RebuildRelationships()
    {
        _previewLogs.Clear();
        List<TechNode> nodes = BuildNodesList();
        if (nodes.Count == 0) return;

        Dictionary<int, TechNode> nodeMap = nodes.ToDictionary(n => n.index);

        // 1. Structure Connections based on Mode
        if (structureMode == StructureMode.ParallelChains)
        {
            BuildParallelChains(nodes);
        }
        else
        {
            BuildLayeredTree(nodes);
        }

        // 1.5 Apply Processor Gate: force all Tier 2 resource unlock techs to require the gate tech
        if (useProcessorGate)
        {
            ApplyProcessorGate(nodes, nodeMap);
        }

        // 2. Apply Transitive Reduction if requested
        if (applyTransitiveReduction)
        {
            List<(int from, int to)> redundantEdges = GetRedundantEdges(nodes);
            foreach (var edge in redundantEdges)
            {
                if (nodeMap.TryGetValue(edge.to, out TechNode toNode))
                {
                    toNode.prerequisites.Remove(edge.from);
                }
                if (nodeMap.TryGetValue(edge.from, out TechNode fromNode))
                {
                    fromNode.successors.Remove(edge.to);
                }
            }
            _previewLogs.Add($"Pruned {redundantEdges.Count} redundant connections via Transitive Reduction.");
        }

        // 3. Save to Assets
        UnityEngine.Object[] assetsToUndo = nodes.Select(n => (UnityEngine.Object)n.asset).ToArray();
        Undo.RecordObjects(assetsToUndo, "Rebuild Tech Tree Connections");

        foreach (TechNode node in nodes)
        {
            node.asset.prerequisiteTechIndices = node.prerequisites.ToArray();
            node.asset.successorTechIndices = node.successors.ToArray();
            EditorUtility.SetDirty(node.asset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _previewLogs.Add("Successfully rebuilt and saved all tech tree connections!");
        Debug.Log("Tech Tree relationships rebuilt successfully.");
        AnalyzeCurrentTree();
    }

    private void ApplyProcessorGate(List<TechNode> nodes, Dictionary<int, TechNode> nodeMap)
    {
        if (!nodeMap.TryGetValue(processorGateTechIndex, out TechNode gateTech))
        {
            _previewLogs.Add($"[Processor Gate] WARNING: No tech found with index {processorGateTechIndex}. Gate not applied.");
            return;
        }

        int connectedCount = 0;
        foreach (TechNode node in nodes)
        {
            if (node.index == processorGateTechIndex) continue;
            if (node.asset.unlocksResources == null || node.asset.unlocksResources.Length == 0) continue;

            bool unlocksT2Resource = false;
            foreach (ResourceType rt in node.asset.unlocksResources)
            {
                if (_resourceTiers.TryGetValue(rt, out ResourceTier tier) && tier >= ResourceTier.Tier2)
                {
                    unlocksT2Resource = true;
                    break;
                }
            }

            if (!unlocksT2Resource) continue;

            if (!node.prerequisites.Contains(processorGateTechIndex))
            {
                node.prerequisites.Add(processorGateTechIndex);
                connectedCount++;
            }
            if (!gateTech.successors.Contains(node.index))
            {
                gateTech.successors.Add(node.index);
            }

            _previewLogs.Add($"[Processor Gate] '{node.name}' (idx {node.index}) now requires '{gateTech.name}' (idx {processorGateTechIndex}).");
        }

        _previewLogs.Add($"[Processor Gate] Applied to {connectedCount} Tier 2+ resource unlock techs.");
    }

    private void BuildParallelChains(List<TechNode> nodes)
    {
        // Parallel Chains creates 3 clean sequential thematic paths sorted by Resource Tier
        foreach (TechCategory category in Enum.GetValues(typeof(TechCategory)))
        {
            List<TechNode> categoryNodes = nodes
                .Where(n => n.category == category)
                .OrderBy(n => (int)n.tier)
                .ThenBy(n => n.index)
                .ToList();

            _previewLogs.Add($"Structuring Chain for Theme: {category} ({categoryNodes.Count} nodes)");

            for (int i = 1; i < categoryNodes.Count; i++)
            {
                TechNode prev = categoryNodes[i - 1];
                TechNode current = categoryNodes[i];

                current.prerequisites.Add(prev.index);
                prev.successors.Add(current.index);
            }
        }
    }

    private void BuildLayeredTree(List<TechNode> nodes)
    {
        _previewLogs.Add("Structuring Layered Tree with Column Splitting (Max Rows constraint)...");

        // Group by tier, sorted by category then index to maintain thematic tracks
        Dictionary<ResourceTier, List<TechNode>> nodesByTier = new Dictionary<ResourceTier, List<TechNode>>();
        foreach (ResourceTier tier in Enum.GetValues(typeof(ResourceTier)))
        {
            nodesByTier[tier] = nodes
                .Where(n => n.tier == tier)
                .OrderBy(n => n.category)
                .ThenBy(n => n.index)
                .ToList();
        }

        // We segment nodes of each tier into blocks of size at most 'maxRows'.
        // This spreads overflow nodes horizontally (increasing columns) instead of vertically (increasing rows).
        Dictionary<ResourceTier, List<List<TechNode>>> blocksByTier = new Dictionary<ResourceTier, List<List<TechNode>>>();

        foreach (ResourceTier tier in Enum.GetValues(typeof(ResourceTier)))
        {
            List<TechNode> tierNodes = nodesByTier[tier];
            List<List<TechNode>> blocks = new List<List<TechNode>>();
            blocksByTier[tier] = blocks;

            if (tierNodes.Count == 0) continue;

            for (int i = 0; i < tierNodes.Count; i += maxRows)
            {
                int count = Math.Min(maxRows, tierNodes.Count - i);
                blocks.Add(tierNodes.GetRange(i, count));
            }

            _previewLogs.Add($"Tier {tier}: {tierNodes.Count} nodes split into {blocks.Count} sub-columns.");

            // Link blocks sequentially within the same tier
            for (int b = 1; b < blocks.Count; b++)
            {
                List<TechNode> prevBlock = blocks[b - 1];
                List<TechNode> currentBlock = blocks[b];

                for (int j = 0; j < currentBlock.Count; j++)
                {
                    TechNode current = currentBlock[j];
                    TechNode prev = prevBlock[j % prevBlock.Count];

                    current.prerequisites.Add(prev.index);
                    prev.successors.Add(current.index);
                }
            }
        }

        // Link the first block of Tier k to the last block of Tier k-1
        for (int tIdx = 1; tIdx <= 2; tIdx++)
        {
            ResourceTier currentTier = (ResourceTier)tIdx;
            ResourceTier prevTier = (ResourceTier)(tIdx - 1);

            List<List<TechNode>> currentBlocks = blocksByTier[currentTier];
            List<List<TechNode>> prevBlocks = blocksByTier[prevTier];

            if (currentBlocks.Count == 0 || prevBlocks.Count == 0) continue;

            List<TechNode> firstCurrentBlock = currentBlocks[0];
            List<TechNode> lastPrevBlock = prevBlocks[prevBlocks.Count - 1];

            for (int j = 0; j < firstCurrentBlock.Count; j++)
            {
                TechNode current = firstCurrentBlock[j];
                TechNode prev = lastPrevBlock[j % lastPrevBlock.Count];

                current.prerequisites.Add(prev.index);
                prev.successors.Add(current.index);
            }
        }
    }

    private List<(int from, int to)> GetRedundantEdges(List<TechNode> nodes)
    {
        List<(int from, int to)> redundant = new List<(int, int)>();
        Dictionary<int, List<int>> adjacencyList = nodes.ToDictionary(n => n.index, n => new List<int>(n.prerequisites));

        foreach (TechNode node in nodes)
        {
            List<int> prereqs = new List<int>(node.prerequisites);
            foreach (int prereq in prereqs)
            {
                // Temporarily remove edge prereq -> node.index
                adjacencyList[node.index].Remove(prereq);

                // Check if node.index is still reachable from prereq
                if (IsReachable(prereq, node.index, adjacencyList))
                {
                    redundant.Add((prereq, node.index));
                }
                else
                {
                    // Restore edge
                    adjacencyList[node.index].Add(prereq);
                }
            }
        }
        return redundant;
    }

    private bool IsReachable(int start, int target, Dictionary<int, List<int>> adjacencyList)
    {
        Queue<int> queue = new Queue<int>();
        HashSet<int> visited = new HashSet<int>();

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (current == target) return true;

            // Follow prerequisites backward to check path
            // In our graph, edge is from -> to (prereq -> node)
            // So to traverse from prereq to node, we look for nodes that have 'current' as a prerequisite
            foreach (var pair in adjacencyList)
            {
                int nodeIdx = pair.Key;
                List<int> prereqs = pair.Value;

                if (prereqs.Contains(current) && !visited.Contains(nodeIdx))
                {
                    visited.Add(nodeIdx);
                    queue.Enqueue(nodeIdx);
                }
            }
        }
        return false;
    }

    private static int[] ReadIndexArray(TechData tech, string propName)
    {
        SerializedObject so = new SerializedObject(tech);
        so.Update();
        SerializedProperty arr = so.FindProperty(propName);
        if (arr == null || !arr.isArray) return new int[0];
        int[] result = new int[arr.arraySize];
        for (int i = 0; i < arr.arraySize; i++)
            result[i] = arr.GetArrayElementAtIndex(i).intValue;
        return result;
    }

    private static void WriteIndexArray(SerializedObject so, string propName, int[] values)
    {
        SerializedProperty arr = so.FindProperty(propName);
        if (arr == null || !arr.isArray) return;
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            arr.GetArrayElementAtIndex(i).intValue = values[i];
    }

    private static void SwapIndexInArray(SerializedObject so, string arrayPropertyName, int indexA, int indexB)
    {
        SerializedProperty arrayProp = so.FindProperty(arrayPropertyName);
        if (arrayProp == null || !arrayProp.isArray) return;

        const int sentinel = -99999;

        // Pass 1 (within same SO buffer): A→sentinel, B→A
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty elem = arrayProp.GetArrayElementAtIndex(i);
            if (elem.intValue == indexA)
                elem.intValue = sentinel;
            else if (elem.intValue == indexB)
                elem.intValue = indexA;
        }

        // Pass 2 (within same SO buffer): sentinel→B
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty elem = arrayProp.GetArrayElementAtIndex(i);
            if (elem.intValue == sentinel)
                elem.intValue = indexB;
        }
    }

    private void RebuildCellsInScene()
    {
        if (techDataCellPrefab == null || cellContent == null || catalog == null) return;

        Undo.RegisterFullObjectHierarchyUndo(cellContent.gameObject, "Rebuild Tech Data Cells");

        // Remove existing TechDataCell children
        for (int i = cellContent.childCount - 1; i >= 0; i--)
        {
            Transform child = cellContent.GetChild(i);
            if (child.GetComponent<TechDataCell>() != null)
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        int created = 0;
        foreach (TechData tech in catalog.Techs)
        {
            if (tech == null) continue;

            GameObject cellObj = PrefabUtility.InstantiatePrefab(techDataCellPrefab, cellContent) as GameObject;
            Undo.RegisterCreatedObjectUndo(cellObj, "Create Tech Cell");
            cellObj.name = $"TechCell_{tech.techIndex}_{tech.GetTechName()}";

            SerializedObject so = new SerializedObject(cellObj.GetComponent<TechDataCell>());
            SerializedProperty techDataProp = so.FindProperty("techData");
            techDataProp.objectReferenceValue = tech;
            so.ApplyModifiedPropertiesWithoutUndo();

            created++;
        }

        EditorSceneManager.MarkSceneDirty(cellContent.gameObject.scene);

        _previewLogs.Clear();
        _previewLogs.Add($"Cell Rebuild 완료: {created}개 TechDataCell 생성됨 (Content: {cellContent.name})");
        Debug.Log($"[TechTreeRebuilder] {created} TechDataCell(s) created under '{cellContent.name}'.");
    }

    private void SwapTechs(int indexA, int indexB)
    {
        if (catalog == null || catalog.Techs == null)
        {
            Debug.LogError("SwapTechs: catalog is not assigned.");
            return;
        }

        TechData techA = catalog.GetTechByIndex(indexA);
        TechData techB = catalog.GetTechByIndex(indexB);

        if (techA == null)
        {
            EditorUtility.DisplayDialog("Not Found", $"Tech index {indexA}를 카탈로그에서 찾을 수 없습니다.", "OK");
            return;
        }
        if (techB == null)
        {
            EditorUtility.DisplayDialog("Not Found", $"Tech index {indexB}를 카탈로그에서 찾을 수 없습니다.", "OK");
            return;
        }

        List<TechData> allTechs = new List<TechData>();
        foreach (TechData t in catalog.Techs)
        {
            if (t != null) allTechs.Add(t);
        }

        // Snapshot A and B's connection arrays before any writes
        int[] aPrereqs = ReadIndexArray(techA, "prerequisiteTechIndices");
        int[] aSuccs = ReadIndexArray(techA, "successorTechIndices");
        int[] bPrereqs = ReadIndexArray(techB, "prerequisiteTechIndices");
        int[] bSuccs = ReadIndexArray(techB, "successorTechIndices");

        // Step 1: A takes B's connections, B takes A's connections (techIndex unchanged)
        SerializedObject soA = new SerializedObject(techA);
        soA.Update();
        WriteIndexArray(soA, "prerequisiteTechIndices", bPrereqs);
        WriteIndexArray(soA, "successorTechIndices", bSuccs);
        soA.ApplyModifiedProperties();

        SerializedObject soB = new SerializedObject(techB);
        soB.Update();
        WriteIndexArray(soB, "prerequisiteTechIndices", aPrereqs);
        WriteIndexArray(soB, "successorTechIndices", aSuccs);
        soB.ApplyModifiedProperties();

        // Step 2: In every other tech, swap all references to A and B
        foreach (TechData tech in allTechs)
        {
            if (tech == techA || tech == techB) continue;

            SerializedObject so = new SerializedObject(tech);
            so.Update();
            SwapIndexInArray(so, "prerequisiteTechIndices", indexA, indexB);
            SwapIndexInArray(so, "successorTechIndices", indexA, indexB);
            so.ApplyModifiedProperties();
        }

        AssetDatabase.SaveAssets();

        _previewLogs.Clear();
        _previewLogs.Add($"Swap 완료: Tech {indexA}({techA.GetTechName()})과 Tech {indexB}({techB.GetTechName()})의 위치(선후행 관계)를 교환했습니다.");

        // Swap cell positions in scene
        if (cellContent != null)
        {
            TechDataCell cellA = null;
            TechDataCell cellB = null;
            TechDataCell[] sceneCells = cellContent.GetComponentsInChildren<TechDataCell>(true);
            for (int i = 0; i < sceneCells.Length; i++)
            {
                if (sceneCells[i].TechData == techA) cellA = sceneCells[i];
                else if (sceneCells[i].TechData == techB) cellB = sceneCells[i];
            }

            if (cellA != null && cellB != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(cellContent.gameObject, $"Swap Cell Positions {indexA} <-> {indexB}");

                int siblingA = cellA.transform.GetSiblingIndex();
                int siblingB = cellB.transform.GetSiblingIndex();

                // Move the lower-index sibling first to avoid index shifting issues
                if (siblingA < siblingB)
                {
                    cellA.transform.SetSiblingIndex(siblingB);
                    cellB.transform.SetSiblingIndex(siblingA);
                }
                else if (siblingA > siblingB)
                {
                    cellB.transform.SetSiblingIndex(siblingA);
                    cellA.transform.SetSiblingIndex(siblingB);
                }

                cellA.gameObject.name = $"TechCell_{indexA}_{techA.GetTechName()}";
                cellB.gameObject.name = $"TechCell_{indexB}_{techB.GetTechName()}";

                EditorSceneManager.MarkSceneDirty(cellContent.gameObject.scene);
                _previewLogs.Add($"씬 Cell 위치 교환 완료: {cellA.gameObject.name} <-> {cellB.gameObject.name}");

                // Refresh gizmo lines in scene view
                UnityEditor.SceneView.RepaintAll();

                // Runtime: rebuild line Image objects
                if (Application.isPlaying)
                {
                    TechResearchGraphPanel panel = cellContent.GetComponentInParent<TechResearchGraphPanel>();
                    if (panel == null)
                        panel = FindFirstObjectByType<TechResearchGraphPanel>();
                    if (panel != null)
                        panel.RedrawLines();
                }
            }
            else
            {
                _previewLogs.Add("씬 Cell을 찾을 수 없어 위치 교환 생략 (Cell Rebuild 후 다시 시도하세요).");
            }
        }

        Debug.Log($"[TechTreeRebuilder] Swapped tech indices {indexA} <-> {indexB}");
    }
}
