using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

public enum TutorialUIPanel
{
    ResourcePanel,
    ResourceInfoPanel,
    StatsPanel,
    DebuffPanel,
    TimeSlider,
    GameSpeed,
    NoisePanel,
    UnitPopulationPanel,
    AlertPanel,
    MainControlPanel,
    LaunchButton,
    QuestPanel
}

[CreateAssetMenu(fileName = "New Tutorial Step", menuName = "Tutorial/Tutorial Step Data")]
public class TutorialStepData : ScriptableObject
{
    [Header("Step Information")]
    public int stepIndex;
    [TextArea(3, 10)] public string text;

    [Header("Step Type")]
    public TutorialStepType stepType;

    [Header("Condition Settings")]
    public float duration;
    public int count;

    [Header("Resource Settings")]
    public ResourceType resourceType;

    [Header("Building Settings")]
    public string buildingType;

    [Header("Unit Settings")]
    public string unitType;

    [Header("Item Settings")]
    public string itemType;

    [Header("UI Settings")]
    public bool showProgressBar;
    public List<TutorialUIPanel> enableUIPanels = new List<TutorialUIPanel>();
    public float tutorialPanelMoveDownOffset;

    [Header("Step Start Actions")]
    public UnitData[] spawnUnits;
    public ResourceCost[] grantResources;

    [Header("Step Completion")]
    public EventReference completionSound;

    [Header("Material Highlight Settings")]
    public bool enableMaterialHighlight;
    public List<string> highlightTargetIDs = new List<string>();

    [Header("Arrow UI Settings")]
    public bool showArrowUI;
    public string arrowID;

    [Header("Target Bracket Settings")]
    public bool showTargetBracket;
    public string targetBracketBuildingType;
    public bool includeConstructionSiteForTargetBracket = true;
}
