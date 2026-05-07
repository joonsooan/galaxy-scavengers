using UnityEngine;

public enum QuestProvider
{
    NPC_1,
    NPC_2,
    NPC_3,
    NPC_4
}

public enum QuestType
{
    BaseQuest,
    RequestQuest,
    CoreRepairQuest
}

[System.Serializable]
public class QuestReward
{
    [Header("Resource Rewards")]
    public ResourceCost[] resourceRewards;
    
    [Header("Module Rewards")]
    public ModuleRecipe[] moduleRewards;

    [Header("Building Unlocks")]
    [Tooltip("Buildings unlocked when this quest is completed")]
    public BuildingData[] unlockedBuildings;
    
    [Header("New Units")]
    [Tooltip("Units shown as 'new' in quest rewards (tutorial/informational)")]
    public UnitData[] newUnits;
}

[CreateAssetMenu(fileName = "New Quest", menuName = "Quest System/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("Quest Identification")]
    public int questId;
    
    [Header("Quest Type")]
    public QuestType questType = QuestType.BaseQuest;
    
    [Header("Quest Provider")]
    public QuestProvider questProvider = QuestProvider.NPC_1;

    [Header("Quest Prerequisite")]
    [Tooltip("Multiple prerequisite quest IDs. All must be completed.")]
    public int[] previousQuestIds;

    [Header("Quest Information")]
    public string questName;
    [TextArea(3, 5)]
    public string questInfo;

    [Header("Quest Requirements")]
    public ResourceCost[] requiredResources;
    
    [Header("Quest Tracking Requirements")]
    [Tooltip("Additional quest completion checks beyond resource requirements")]
    public QuestCheckData[] questCheckRequirements;
    
    [Header("Quest Finish Reward")]
    public QuestReward questFinishReward;
}
