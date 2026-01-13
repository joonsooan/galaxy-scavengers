using UnityEngine;

public enum QuestProvider
{
    NPC_1,
    NPC_2,
    NPC_3,
    NPC_4
}

[System.Serializable]
public class QuestReward
{
    [Header("Resource Rewards")]
    public ResourceCost[] resourceRewards;
    
    [Header("Module Rewards")]
    public ModuleRecipe[] moduleRewards;
}

[CreateAssetMenu(fileName = "New Quest", menuName = "Quest System/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("Quest Identification")]
    public int questId;
    
    [Header("Quest Provider")]
    public QuestProvider questProvider = QuestProvider.NPC_1;

    [Header("Quest Prerequisite")]
    public int previousQuestId = -1;

    [Header("Quest Information")]
    public string questName;
    [TextArea(3, 5)]
    public string questInfo;

    [Header("Quest Requirements")]
    public ResourceCost[] requiredResources;
    
    [Header("Quest Finish Reward")]
    public QuestReward questFinishReward;
}
