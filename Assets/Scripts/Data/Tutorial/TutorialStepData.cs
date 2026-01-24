using UnityEngine;

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

    [Header("Step Start Actions")]
    public UnitData[] spawnUnits;
    public ResourceCost[] grantResources;
}
