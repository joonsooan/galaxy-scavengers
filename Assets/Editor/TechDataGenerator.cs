using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TechDataGenerator : EditorWindow
{
    private struct TechNodeDef
    {
        public int index;
        public string manualName;       // only used when no building/unit ref
        public bool isUnlockedByDefault;
        public int[] prereqs;
        public int researchDuration;
        public (ResourceType type, int amount)[] costs;
        public ResourceType[] unlockResources;
        public ModuleStatType[] statTypes;
        public float[] statValues;
    }

    [MenuItem("Tools/Tech Data Generator")]
    public static void ShowWindow()
    {
        GetWindow<TechDataGenerator>("Tech Data Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Tech Data Asset Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Generates 54 TechData ScriptableObject assets into\n" +
            "Assets/Resources/Tech Data/\n" +
            "and registers them into the Tech Research Catalog.\n\n" +
            "Building/Unit nodes use existingData references.\n" +
            "Resource/Stat nodes use Korean manual names.",
            MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("Generate All Tech Data Assets", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Generate Tech Data?",
                "This will delete all existing TechData assets and regenerate all 54 nodes.\nProceed?",
                "Yes", "No"))
            {
                GenerateAll();
            }
        }
    }

    private static void GenerateAll()
    {
        const string outputFolder = "Assets/Resources/Tech Data";

        string[] existingGuids = AssetDatabase.FindAssets("t:TechData", new[] { outputFolder });
        foreach (string guid in existingGuids)
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

        // Load building references
        BuildingData bldMainStructure = LoadBuilding("Main Structure");
        BuildingData bldStorage       = LoadBuilding("0_Storage");
        BuildingData bldGenerator     = LoadBuilding("1_Generator");
        BuildingData bldSmelter       = LoadBuilding("3_Smelter");
        BuildingData bldBattery       = LoadBuilding("6_Battery");
        BuildingData bldCharging      = LoadBuilding("7_ChargingStation");
        BuildingData bldTurret        = LoadBuilding("8_Turret");
        BuildingData bldExtractor     = LoadBuilding("9_Extractor");
        BuildingData bldReceiver      = LoadBuilding("10_Receiver");
        BuildingData bldPlatform      = LoadBuilding("11_Platform");
        BuildingData bldWall          = LoadBuilding("12_Wall");
        BuildingData bldRocketEngine  = LoadBuilding("13_RocketEngine");

        // Load unit references
        UnitData unitMiner     = LoadUnit("Unit_Miner");
        UnitData unitConstruct = LoadUnit("Unit_Construct");
        UnitData unitProcessor = LoadUnit("Unit_Processor");
        UnitData unitScout     = LoadUnit("Unit_Scout");

        // Each entry: (TechNodeDef, BuildingData ref, UnitData ref)
        // If building/unit ref is provided → useExistingData = true
        // Otherwise → useExistingData = false, manualName used
        var nodes = new List<(TechNodeDef def, BuildingData bld, UnitData unit)>
        {
            // ── Phase 0: Default Unlocks ──────────────────────────────────────
            (N(0, isDefault:true), bldMainStructure, null),
            (N(1, isDefault:true), bldStorage,       null),
            (N(2, isDefault:true), null,  unitMiner),
            (N(3, isDefault:true), null,  unitConstruct),

            // ── Phase 1: Early Infrastructure ─────────────────────────────────
            (N(4,  prereqs:P(0),   duration:30, costs:C(ResourceType.Ferrite,20, ResourceType.Aether,10)),
                bldGenerator, null),

            (N(5,  prereqs:P(2),   duration:30, costs:C(ResourceType.Ferrite,15, ResourceType.Biomass,10)),
                null, unitProcessor),

            (N(6,  prereqs:P(3),   duration:30, costs:C(ResourceType.Aether,15, ResourceType.CryoCrystal,10)),
                null, unitScout),

            (N(7,  prereqs:P(4),   duration:40, costs:C(ResourceType.CryoCrystal,20, ResourceType.Ferrite,15)),
                bldBattery, null),

            (N(8,  prereqs:P(4),   duration:40, costs:C(ResourceType.Ferrite,20, ResourceType.Aether,15)),
                bldExtractor, null),

            // 9: AlloyPlate unlock
            (N(9,  "합금 판",     prereqs:P(5,8), duration:50,
                costs:C(ResourceType.Ferrite,30, ResourceType.Biomass,15),
                resources:R(ResourceType.AlloyPlate)),
                null, null),

            // ── Phase 2: Tier 2 Resources + Buildings ─────────────────────────
            (N(10, prereqs:P(9),   duration:50, costs:C(ResourceType.AlloyPlate,15, ResourceType.Ferrite,25)),
                bldSmelter, null),

            (N(11, "복합 골조",   prereqs:P(9),   duration:50,
                costs:C(ResourceType.AlloyPlate,15, ResourceType.Ferrite,20),
                resources:R(ResourceType.CompositeFrame)),
                null, null),

            (N(12, "바이오 케이블", prereqs:P(5), duration:50,
                costs:C(ResourceType.Biomass,25, ResourceType.Aether,15),
                resources:R(ResourceType.BioCable)),
                null, null),

            (N(13, "전자 칩",     prereqs:P(8),   duration:50,
                costs:C(ResourceType.Aether,25, ResourceType.Ferrite,20),
                resources:R(ResourceType.EChip)),
                null, null),

            (N(14, "극저온 용액", prereqs:P(7),   duration:50,
                costs:C(ResourceType.CryoCrystal,30, ResourceType.Ferrite,15),
                resources:R(ResourceType.CryoGel)),
                null, null),

            (N(15, prereqs:P(7),   duration:40, costs:C(ResourceType.CryoCrystal,20, ResourceType.Ferrite,20)),
                bldCharging, null),

            (N(16, "바이오 연료", prereqs:P(12),  duration:60,
                costs:C(ResourceType.BioCable,15, ResourceType.Biomass,20),
                resources:R(ResourceType.BioFuel)),
                null, null),

            (N(17, "동력 큐브",   prereqs:P(7,10), duration:60,
                costs:C(ResourceType.CryoCrystal,20, ResourceType.AlloyPlate,15),
                resources:R(ResourceType.PowerCube)),
                null, null),

            (N(18, "코어 프로세서", prereqs:P(10,13), duration:60,
                costs:C(ResourceType.AlloyPlate,20, ResourceType.EChip,15),
                resources:R(ResourceType.Core)),
                null, null),

            (N(19, "표준 탄약",   prereqs:P(10,12), duration:60,
                costs:C(ResourceType.AlloyPlate,15, ResourceType.BioCable,15),
                resources:R(ResourceType.Ammunition)),
                null, null),

            (N(20, "솔라나 정수", prereqs:P(13,8), duration:60,
                costs:C(ResourceType.EChip,15, ResourceType.Aether,20),
                resources:R(ResourceType.Solana)),
                null, null),

            // ── Phase 3: Remaining Buildings + Population ──────────────────────
            (N(21, prereqs:P(13),    duration:50, costs:C(ResourceType.EChip,15, ResourceType.Aether,20)),
                bldReceiver, null),

            (N(22, prereqs:P(10,11), duration:50, costs:C(ResourceType.CompositeFrame,15, ResourceType.AlloyPlate,20)),
                bldWall, null),

            (N(23, prereqs:P(10),    duration:50, costs:C(ResourceType.AlloyPlate,20, ResourceType.Ferrite,25)),
                bldPlatform, null),

            (N(24, prereqs:P(19,6),  duration:60, costs:C(ResourceType.Ammunition,15, ResourceType.AlloyPlate,20)),
                bldTurret, null),

            (N(25, "인구 수용 증가 I", prereqs:P(4), duration:60,
                costs:C(ResourceType.Ferrite,25, ResourceType.Biomass,20),
                stat:S(ModuleStatType.MaxPopulation, 2f)),
                null, null),

            // ── Phase 4: Tier 3 Resources ──────────────────────────────────────
            (N(26, "중장갑 판",      prereqs:P(11,18), duration:70,
                costs:C(ResourceType.CompositeFrame,20, ResourceType.Core,15),
                resources:R(ResourceType.HeavyPlating)),
                null, null),

            (N(27, "액추에이터",    prereqs:P(18,13), duration:70,
                costs:C(ResourceType.Core,20, ResourceType.EChip,15),
                resources:R(ResourceType.Actuator)),
                null, null),

            (N(28, "유전자 데이터 칩", prereqs:P(16,12), duration:70,
                costs:C(ResourceType.BioCable,20, ResourceType.BioFuel,15),
                resources:R(ResourceType.GenomeChip)),
                null, null),

            (N(29, "초전도 도관",   prereqs:P(14,17), duration:70,
                costs:C(ResourceType.CryoGel,20, ResourceType.PowerCube,15),
                resources:R(ResourceType.CryoConduit)),
                null, null),

            (N(30, "플라즈마 큐브", prereqs:P(17,18), duration:70,
                costs:C(ResourceType.PowerCube,20, ResourceType.Core,15),
                resources:R(ResourceType.PlasmaCube)),
                null, null),

            (N(31, "센서 유닛",     prereqs:P(21,18), duration:70,
                costs:C(ResourceType.Core,15, ResourceType.EChip,20),
                resources:R(ResourceType.SensorUnit)),
                null, null),

            (N(32, "추적 탄두",     prereqs:P(24,19), duration:70,
                costs:C(ResourceType.Ammunition,25, ResourceType.Core,15),
                resources:R(ResourceType.SeekerMissile)),
                null, null),

            (N(33, "신경 매트릭스", prereqs:P(28),    duration:70,
                costs:C(ResourceType.GenomeChip,20, ResourceType.BioCable,20),
                resources:R(ResourceType.NeuralMatrix)),
                null, null),

            (N(34, "넥서스 데이터", prereqs:P(31,18), duration:70,
                costs:C(ResourceType.SensorUnit,15, ResourceType.Core,20),
                resources:R(ResourceType.NexusData)),
                null, null),

            (N(35, "수리 키트",     prereqs:P(16,19), duration:70,
                costs:C(ResourceType.BioFuel,15, ResourceType.Ammunition,20),
                resources:R(ResourceType.PatchKit)),
                null, null),

            // ── Phase 5: Advanced Building + Tier-1 Stat Upgrades ─────────────
            (N(36, prereqs:P(26,32), duration:80,
                costs:C(ResourceType.HeavyPlating,20, ResourceType.SeekerMissile,15)),
                bldRocketEngine, null),

            (N(37, "이동 속도 강화 I",  prereqs:P(5),    duration:60,
                costs:C(ResourceType.AlloyPlate,15, ResourceType.EChip,10),
                stat:S(ModuleStatType.UnitMoveSpeed, 0.1f)),
                null, null),

            (N(38, "작업 속도 강화 I",  prereqs:P(5,13), duration:60,
                costs:C(ResourceType.EChip,15, ResourceType.AlloyPlate,15),
                stat:S(ModuleStatType.UnitWorkSpeed, 0.1f)),
                null, null),

            (N(39, "건물 내구도 강화 I", prereqs:P(10),   duration:60,
                costs:C(ResourceType.AlloyPlate,20, ResourceType.CompositeFrame,10),
                stat:S(ModuleStatType.BuildingHP, 0.1f)),
                null, null),

            (N(40, "자원 생산 강화 I",  prereqs:P(8,13), duration:60,
                costs:C(ResourceType.EChip,15, ResourceType.AlloyPlate,15),
                stat:S(ModuleStatType.ResourceGenerationRate, 0.1f)),
                null, null),

            (N(41, "유닛 내구도 강화 I", prereqs:P(12),   duration:60,
                costs:C(ResourceType.BioCable,15, ResourceType.AlloyPlate,15),
                stat:S(ModuleStatType.UnitHP, 0.1f)),
                null, null),

            // ── Phase 6: Tier-2 Stat Upgrades ─────────────────────────────────
            (N(42, "이동 속도 강화 II",  prereqs:P(37,27), duration:80,
                costs:C(ResourceType.Actuator,10, ResourceType.Core,15),
                stat:S(ModuleStatType.UnitMoveSpeed, 0.15f)),
                null, null),

            (N(43, "작업 속도 강화 II",  prereqs:P(38,27), duration:80,
                costs:C(ResourceType.Actuator,10, ResourceType.EChip,20),
                stat:S(ModuleStatType.UnitWorkSpeed, 0.15f)),
                null, null),

            (N(44, "건물 내구도 강화 II", prereqs:P(39,26), duration:80,
                costs:C(ResourceType.HeavyPlating,20, ResourceType.CompositeFrame,20),
                stat:S(ModuleStatType.BuildingHP, 0.15f)),
                null, null),

            (N(45, "자원 생산 강화 II",  prereqs:P(40,31), duration:80,
                costs:C(ResourceType.SensorUnit,15, ResourceType.Core,20),
                stat:S(ModuleStatType.ResourceGenerationRate, 0.15f)),
                null, null),

            (N(46, "유닛 내구도 강화 II", prereqs:P(41,26), duration:80,
                costs:C(ResourceType.HeavyPlating,15, ResourceType.Core,15),
                stat:S(ModuleStatType.UnitHP, 0.15f)),
                null, null),

            (N(47, "인구 수용 증가 II",  prereqs:P(25,16), duration:80,
                costs:C(ResourceType.BioCable,20, ResourceType.Core,15),
                stat:S(ModuleStatType.MaxPopulation, 3f)),
                null, null),

            // ── Phase 7: Tier-3 Stat Upgrades ─────────────────────────────────
            (N(48, "이동 속도 강화 III",  prereqs:P(42,34), duration:100,
                costs:C(ResourceType.NexusData,10, ResourceType.Actuator,15),
                stat:S(ModuleStatType.UnitMoveSpeed, 0.2f)),
                null, null),

            (N(49, "작업 속도 강화 III",  prereqs:P(43,33), duration:100,
                costs:C(ResourceType.NeuralMatrix,10, ResourceType.Actuator,15),
                stat:S(ModuleStatType.UnitWorkSpeed, 0.2f)),
                null, null),

            (N(50, "건물 내구도 강화 III", prereqs:P(44,27), duration:100,
                costs:C(ResourceType.Actuator,15, ResourceType.HeavyPlating,25),
                stat:S(ModuleStatType.BuildingHP, 0.2f)),
                null, null),

            (N(51, "자원 생산 강화 III",  prereqs:P(45,34), duration:100,
                costs:C(ResourceType.NexusData,15, ResourceType.SensorUnit,20),
                stat:S(ModuleStatType.ResourceGenerationRate, 0.2f)),
                null, null),

            (N(52, "유닛 내구도 강화 III", prereqs:P(46,33), duration:100,
                costs:C(ResourceType.NeuralMatrix,10, ResourceType.HeavyPlating,20),
                stat:S(ModuleStatType.UnitHP, 0.2f)),
                null, null),

            (N(53, "인구 수용 증가 III",  prereqs:P(47,28), duration:100,
                costs:C(ResourceType.NeuralMatrix,10, ResourceType.GenomeChip,15),
                stat:S(ModuleStatType.MaxPopulation, 5f)),
                null, null),
        };

        // Compute successors
        var successorMap = new Dictionary<int, List<int>>();
        foreach (var (def, _, _) in nodes)
            successorMap[def.index] = new List<int>();

        foreach (var (def, _, _) in nodes)
        {
            if (def.prereqs == null) continue;
            foreach (int prereq in def.prereqs)
            {
                if (successorMap.ContainsKey(prereq))
                    successorMap[prereq].Add(def.index);
            }
        }

        // Create assets
        var createdAssets = new List<TechData>();

        foreach (var (def, bld, unit) in nodes)
        {
            TechData asset = CreateInstance<TechData>();
            asset.techIndex = def.index;
            asset.isUnlockedByDefault = def.isUnlockedByDefault;
            asset.researchDuration = def.researchDuration;

            // Determine data source
            bool hasExisting = bld != null || unit != null;
            asset.useExistingData = hasExisting;

            if (hasExisting)
            {
                asset.existingDisplayableData = bld;
                asset.existingUnitData = unit;
            }
            else
            {
                asset.manualName = def.manualName;
            }

            // Research costs
            if (def.costs != null && def.costs.Length > 0)
            {
                var costs = new ResourceCost[def.costs.Length];
                for (int i = 0; i < def.costs.Length; i++)
                    costs[i] = new ResourceCost { resourceType = def.costs[i].type, amount = def.costs[i].amount };
                asset.researchCosts = costs;
            }

            asset.prerequisiteTechIndices = def.prereqs ?? new int[0];
            asset.successorTechIndices    = successorMap[def.index].ToArray();

            // Building unlocks (single building per node)
            if (bld != null)
                asset.unlocksBuildings = new BuildingData[] { bld };

            // Unit unlocks (single unit per node)
            if (unit != null)
                asset.unlocksUnits = new UnitData[] { unit };

            asset.unlocksResources = def.unlockResources;

            if (def.statTypes != null)
            {
                asset.grantStatTypes  = def.statTypes;
                asset.grantStatValues = def.statValues;
            }

            string assetPath = $"{outputFolder}/TechData {def.index}.asset";
            AssetDatabase.CreateAsset(asset, assetPath);
            createdAssets.Add(asset);
        }

        // Register in catalog
        string[] catalogGuids = AssetDatabase.FindAssets("t:TechResearchCatalog");
        if (catalogGuids.Length > 0)
        {
            string catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[0]);
            TechResearchCatalog catalog = AssetDatabase.LoadAssetAtPath<TechResearchCatalog>(catalogPath);
            if (catalog != null)
            {
                SerializedObject so = new SerializedObject(catalog);
                SerializedProperty techsProp = so.FindProperty("techs");
                techsProp.ClearArray();
                for (int i = 0; i < createdAssets.Count; i++)
                {
                    techsProp.InsertArrayElementAtIndex(i);
                    techsProp.GetArrayElementAtIndex(i).objectReferenceValue = createdAssets[i];
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(catalog);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"TechDataGenerator: Created {createdAssets.Count} TechData assets.");
        EditorUtility.DisplayDialog("Done", $"Generated {createdAssets.Count} TechData assets successfully.", "OK");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static BuildingData LoadBuilding(string assetName)
    {
        string path = $"Assets/Resources/Building Data/{assetName}.asset";
        BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
        if (data == null)
            Debug.LogWarning($"TechDataGenerator: BuildingData not found at '{path}'");
        return data;
    }

    private static UnitData LoadUnit(string assetName)
    {
        string path = $"Assets/Resources/Unit Data/{assetName}.asset";
        UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>(path);
        if (data == null)
            Debug.LogWarning($"TechDataGenerator: UnitData not found at '{path}'");
        return data;
    }

    private static int[]          P(params int[] indices)        => indices;
    private static ResourceType[] R(params ResourceType[] types) => types;

    private static (ResourceType type, int amount)[] C(params object[] pairs)
    {
        var result = new List<(ResourceType, int)>();
        for (int i = 0; i + 1 < pairs.Length; i += 2)
            result.Add(((ResourceType)pairs[i], (int)pairs[i + 1]));
        return result.ToArray();
    }

    private static (ModuleStatType[] types, float[] values) S(ModuleStatType type, float value)
        => (new[] { type }, new[] { value });

    private static TechNodeDef N(
        int index,
        string manualName = null,
        bool isDefault = false,
        int[] prereqs = null,
        int duration = 0,
        (ResourceType type, int amount)[] costs = null,
        ResourceType[] resources = null,
        (ModuleStatType[] types, float[] values)? stat = null)
    {
        return new TechNodeDef
        {
            index               = index,
            manualName          = manualName,
            isUnlockedByDefault = isDefault,
            prereqs             = prereqs,
            researchDuration    = duration,
            costs               = costs,
            unlockResources     = resources,
            statTypes           = stat.HasValue ? stat.Value.types : null,
            statValues          = stat.HasValue ? stat.Value.values : null,
        };
    }
}
