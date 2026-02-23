using UnityEngine;

public enum CorePart
{
    Engine,
    Storage,
    Barrier,
    Controller,
    Repeater
}

[CreateAssetMenu(fileName = "New Core Part Data", menuName = "Core/Core Part Data")]
public class CorePartData : ScriptableObject
{
    [Header("Part Information")]
    public CorePart partType;
    public string partName;
    [TextArea(2, 4)]
    public string partDescription;
    public Color questTitleColor = Color.cyan;

    [Header("Repair Requirements")]
    public ResourceCost[] requiredResources;

    [Header("Repair Rewards")]
    public QuestReward repairReward;

    [Header("Debuff Effects")]
    public float debuffValue = 0.3f;
    public Sprite debuffIcon;
}
