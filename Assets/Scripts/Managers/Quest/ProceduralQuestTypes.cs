using System;
using System.Collections.Generic;
using UnityEngine;

public enum ProceduralQuestState
{
    Waiting,
    ChoiceOffered,
    InProgress,
    Completable,
    Completed
}

[Serializable]
public class ProceduralQuestRewardSpec
{
    public ResourceType resourceType = ResourceType.None;
    public int amount;
}

[Serializable]
public class ProceduralQuestRuntimeData
{
    public int questId;
    public ResourceType targetResourceType;
    public int requiredAmount;
    public List<ProceduralQuestRewardSpec> rewardSpecs = new();
    public int createdAtQuestIndex;
    public ProceduralQuestState state;
}

[Serializable]
public class ProceduralQuestChoiceData
{
    public int questId;
    public ResourceType targetResourceType;
    public int requiredAmount;
    public List<ProceduralQuestRewardSpec> rewardSpecs = new();
    public int createdAtQuestIndex;
}
