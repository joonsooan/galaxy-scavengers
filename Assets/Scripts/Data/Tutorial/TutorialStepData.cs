using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

public enum TutorialUIPanel
{
    ResourcePanel = 0,
    GameSpeedPanel = 1,
    DayNightPanel = 2,
    LeftTimePanel = 3,
    NoisePanel = 4,
    TutorialPanel = 5,
    PausePanel = 6,
    StorageResourceInfoPanel = 8,
    ProcessorInfoPanel = 9,
    DroneProduceInfoPanel = 10,
    BuildingInfoPanel = 11,
    ResourceInfoPanel = 12,
    UnitInfoPanel = 13,
    ExtractorInfoPanel = 14,
    MainControlPanel = 15,
    UnitManagePanel = 16,
    ResourceStatsPanel = 17,
    BuildingDestroyPanel = 18,
    BaseBuildingPanel = 19,
    LaunchButton = 20,
    LaunchResultUI = 21,
    LaunchPanel = 22,
    LaunchPreparePanel = 23,
    AlertPanelParent = 24,
    AlertTooltipPanel = 25
}

public enum TutorialUnitType
{
    None = 0,
    Scout = 1,
    Miner = 2,
    Construct = 3,
    Processor = 4
}

[CreateAssetMenu(fileName = "New Tutorial Step", menuName = "Tutorial/Tutorial Step Data")]
public class TutorialStepData : ScriptableObject
{
    [Header("Step")]
    public int stepIndex;
    [TextArea(3, 10)] public string text;
    public TutorialStepType stepType;
    public float duration;
    public int count;

    [Header("Targets")]
    public ResourceType resourceType;
    public BuildingType buildingType;
    public TutorialUnitType unitType;
    public string itemType;

    [Header("UI")]
    public bool showProgressBar;
    public List<TutorialUIPanel> enableUIPanels = new List<TutorialUIPanel>();

    [Header("Rewards")]
    public ResourceCost[] grantResources;

    [Header("Completion")]
    public EventReference completionSound;

    [Header("Highlight")]
    public bool enableMaterialHighlight;
    public List<string> highlightTargetIDs = new List<string>();

    [Header("Target Bracket")]
    public bool showTargetBracket;
    public string targetBracketBuildingType;
    public bool includeConstructionSiteForTargetBracket = true;
}
