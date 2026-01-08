using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum QuestState
{
    Locked,     // Quest is locked (prerequisite not met)
    Available,  // Quest is available to be picked up
    Active,     // Quest is currently active
    Completed   // Quest has been completed
}

public class QuestDataManager : MonoBehaviour
{
    public static QuestDataManager Instance { get; private set; }

    [Header("Quest List")]
    [SerializeField] private List<QuestData> allQuests = new ();

    private readonly Dictionary<int, QuestData> _questDataDict = new ();
    private readonly Dictionary<int, QuestState> _questStates = new ();
    private readonly HashSet<int> _completedQuestIds = new ();
    private readonly HashSet<int> _activeQuestIds = new ();

    public event Action<int> OnQuestStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LoadQuestsFromResources();
        InitializeQuests();
        
        ResourceDataManager.OnResourceAmountChanged += CheckQuestCompletion;
    }

    private void OnDestroy()
    {
        ResourceDataManager.OnResourceAmountChanged -= CheckQuestCompletion;
    }

    private void LoadQuestsFromResources()
    {
        QuestData[] loadedQuests = Resources.LoadAll<QuestData>("Quest Data");
        
        if (loadedQuests != null && loadedQuests.Length > 0)
        {
            allQuests.Clear();
            allQuests.AddRange(loadedQuests);
        }
        else
        {
            Debug.LogWarning("No quests found in Resources/Quest Data folder. Make sure QuestData assets are placed in Resources/Quest Data/");
        }
    }

    private void InitializeQuests()
    {
        _questDataDict.Clear();
        _questStates.Clear();
        _completedQuestIds.Clear();
        _activeQuestIds.Clear();

        foreach (QuestData quest in allQuests)
        {
            if (quest == null) continue;

            _questDataDict[quest.questId] = quest;
            
            if (quest.previousQuestId == -1)
            {
                _questStates[quest.questId] = QuestState.Available;
            }
            else
            {
                _questStates[quest.questId] = QuestState.Locked;
            }
        }
    }

    private bool CanStartQuest(int questId)
    {
        if (!_questDataDict.ContainsKey(questId))
        {
            Debug.LogWarning($"Quest with ID {questId} not found.");
            return false;
        }

        QuestState currentState = _questStates[questId];
        
        if (currentState != QuestState.Available)
        {
            return false;
        }

        QuestData quest = _questDataDict[questId];
        if (quest.previousQuestId != -1)
        {
            if (!_completedQuestIds.Contains(quest.previousQuestId))
            {
                return false;
            }
        }

        return true;
    }

    public bool StartQuest(int questId)
    {
        if (!CanStartQuest(questId))
        {
            Debug.LogWarning($"Cannot start quest {questId}. Prerequisites may not be met or quest is not available.");
            return false;
        }

        _activeQuestIds.Add(questId);
        _questStates[questId] = QuestState.Active;
        
        OnQuestStateChanged?.Invoke(questId);

        if (_questDataDict.ContainsKey(questId))
        {
            CheckSingleQuestCompletion(questId);
        }

        return true;
    }

    private void CheckQuestCompletion(ResourceType resourceType, int amount)
    {
        if (ResourceDataManager.Instance == null)
        {
            return;
        }

        foreach (QuestData quest in _questDataDict.Values)
        {
            QuestState currentState = _questStates[quest.questId];
            
            if (currentState != QuestState.Active)
            {
                continue;
            }

            bool requiresThisResource = false;
            if (quest.requiredResources != null)
            {
                foreach (ResourceCost cost in quest.requiredResources)
                {
                    if (cost.resourceType == resourceType)
                    {
                        requiresThisResource = true;
                        break;
                    }
                }
            }

            if (!requiresThisResource)
            {
                continue;
            }

            if (AreAllQuestRequirementsMet(quest))
            {
                CompleteQuest(quest.questId);
            }
        }
    }

    private void CheckSingleQuestCompletion(int questId)
    {
        if (ResourceDataManager.Instance == null)
        {
            return;
        }

        if (!_questDataDict.ContainsKey(questId))
        {
            return;
        }

        QuestData quest = _questDataDict[questId];
        QuestState currentState = _questStates[questId];

        if (currentState != QuestState.Active)
        {
            return;
        }

        if (AreAllQuestRequirementsMet(quest))
        {
            CompleteQuest(questId);
        }
    }

    private bool AreAllQuestRequirementsMet(QuestData quest)
    {
        if (ResourceDataManager.Instance == null)
        {
            return false;
        }

        if (quest.requiredResources == null || quest.requiredResources.Length == 0)
        {
            return true;
        }

        foreach (ResourceCost cost in quest.requiredResources)
        {
            int currentAmount = ResourceDataManager.Instance.GetResourceAmount(cost.resourceType);
            if (currentAmount < cost.amount)
            {
                return false;
            }
        }

        return true;
    }

    public bool CompleteQuest(int questId)
    {
        if (!_questDataDict.ContainsKey(questId))
        {
            Debug.LogWarning($"Quest with ID {questId} not found.");
            return false;
        }

        QuestState currentState = _questStates[questId];
        
        if (currentState != QuestState.Active)
        {
            Debug.LogWarning($"Quest {questId} is {currentState} and cannot be completed. Quest must be Active.");
            return false;
        }

        _questStates[questId] = QuestState.Completed;
        _completedQuestIds.Add(questId);
        _activeQuestIds.Remove(questId);
        OnQuestStateChanged?.Invoke(questId);

        return true;
    }

    public bool FinishQuest(int questId)
    {
        if (!_questDataDict.ContainsKey(questId))
        {
            Debug.LogWarning($"Quest with ID {questId} not found.");
            return false;
        }

        QuestState currentState = _questStates[questId];
        
        if (currentState != QuestState.Completed)
        {
            Debug.LogWarning($"Quest {questId} is {currentState} and cannot be finished. Quest must be Completed.");
            return false;
        }

        UnlockDependentQuests(questId);

        return true;
    }

    private void UnlockDependentQuests(int completedQuestId)
    {
        foreach (QuestData quest in _questDataDict.Values)
        {
            if (quest.previousQuestId == completedQuestId && 
                _questStates[quest.questId] == QuestState.Locked)
            {
                _questStates[quest.questId] = QuestState.Available;
                OnQuestStateChanged?.Invoke(quest.questId);
            }
        }
    }

    public QuestState GetQuestState(int questId)
    {
        if (_questStates.ContainsKey(questId))
        {
            return _questStates[questId];
        }
        return QuestState.Locked;
    }

    public QuestData GetQuestData(int questId)
    {
        if (_questDataDict.ContainsKey(questId))
        {
            return _questDataDict[questId];
        }
        return null;
    }

    public HashSet<int> GetActiveQuestIds()
    {
        return new HashSet<int>(_activeQuestIds);
    }

    public bool IsQuestActive(int questId)
    {
        return _activeQuestIds.Contains(questId);
    }

    public List<QuestData> GetAvailableQuests()
    {
        return _questDataDict.Values
            .Where(quest => _questStates[quest.questId] == QuestState.Available)
            .ToList();
    }

    public HashSet<int> GetCompletedQuestIds()
    {
        return new HashSet<int>(_completedQuestIds);
    }

    public bool IsQuestCompleted(int questId)
    {
        return _completedQuestIds.Contains(questId);
    }
}

