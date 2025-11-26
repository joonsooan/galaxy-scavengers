using UnityEngine;

[CreateAssetMenu(fileName = "New Quest", menuName = "Quest System/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("Quest Identification")]
    public int questId;

    [Header("Quest Prerequisite")]
    public int previousQuestId = -1;

    [Header("Quest Information")]
    public string questName;
    [TextArea(3, 5)]
    public string questInfo;

    [Header("Quest Requirements")]
    public ResourceCost[] requiredResources;
}
