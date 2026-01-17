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
    
    [Header("Test Settings")]
    [Tooltip("If true, all active quests will be completable regardless of requirements")]
    [SerializeField] private bool testMode = false;

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
        
        StartCoroutine(SubscribeToBaseInventoryEvents());
        
        // Initialize QuestTracker if not already initialized
        if (FindFirstObjectByType<QuestTracker>() == null)
        {
            GameObject questTrackerObj = new GameObject("QuestTracker");
            questTrackerObj.AddComponent<QuestTracker>();
        }
    }

    private System.Collections.IEnumerator SubscribeToBaseInventoryEvents()
    {
        BaseInventoryManager inventoryManager = null;
        while (inventoryManager == null)
        {
            inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
            yield return null;
        }
        
        inventoryManager.OnResourceChanged += OnBaseInventoryResourceChanged;
    }

    private void OnDestroy()
    {
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager != null)
        {
            inventoryManager.OnResourceChanged -= OnBaseInventoryResourceChanged;
        }
    }
    
    private void OnBaseInventoryResourceChanged(ResourceType resourceType, int amount)
    {
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
            
            // Check if quest has prerequisites
            bool hasPrerequisites = quest.previousQuestIds != null && quest.previousQuestIds.Length > 0;
            
            if (!hasPrerequisites)
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
        
        // Check multiple prerequisites
        if (quest.previousQuestIds != null && quest.previousQuestIds.Length > 0)
        {
            foreach (int prerequisiteId in quest.previousQuestIds)
            {
                if (!_completedQuestIds.Contains(prerequisiteId))
                {
                    return false;
                }
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

    private void CheckSingleQuestCompletion(int questId)
    {
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
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        
        // Check resource requirements
        if (quest.requiredResources != null && quest.requiredResources.Length > 0)
        {
            foreach (ResourceCost cost in quest.requiredResources)
            {
                int currentAmount = inventoryManager.GetResourceAmount(cost.resourceType);
                if (currentAmount < cost.amount)
                {
                    return false;
                }
            }
        }
        
        // Check quest tracking requirements
        if (quest.questCheckRequirements != null && quest.questCheckRequirements.Length > 0)
        {
            foreach (QuestCheckData checkData in quest.questCheckRequirements)
            {
                if (!checkData.IsRequirementMet())
                {
                    return false;
                }
            }
        }

        return true;
    }
    
    public bool CheckQuestCompletion(int questId)
    {
        if (!_questDataDict.ContainsKey(questId))
        {
            return false;
        }
        
        QuestData quest = _questDataDict[questId];
        QuestState currentState = _questStates[questId];
        
        if (currentState != QuestState.Active)
        {
            return false;
        }
        
        // In test mode, all active quests are completable
        if (testMode)
        {
            return true;
        }
        
        return AreAllQuestRequirementsMet(quest);
    }
    
    public void SetTestMode(bool enabled)
    {
        testMode = enabled;
        Debug.Log($"QuestDataManager: Test mode {(enabled ? "enabled" : "disabled")}. All active quests are {(enabled ? "completable" : "requirement-based")}.");
    }
    
    public void ResetAllQuestProgress()
    {
        _questStates.Clear();
        _completedQuestIds.Clear();
        _activeQuestIds.Clear();
        
        // Reset quest check data progress
        foreach (QuestData quest in _questDataDict.Values)
        {
            if (quest == null) continue;
            
            if (quest.questCheckRequirements != null)
            {
                foreach (QuestCheckData checkData in quest.questCheckRequirements)
                {
                    checkData.ResetProgress();
                }
            }
        }
        
        // Reset QuestTracker if it exists
        QuestTracker questTracker = FindFirstObjectByType<QuestTracker>();
        if (questTracker != null)
        {
            questTracker.ResetAllQuestProgress();
        }
        
        // Re-initialize all quests to their default states
        foreach (QuestData quest in _questDataDict.Values)
        {
            if (quest == null) continue;
            
            // Check if quest has prerequisites
            bool hasPrerequisites = quest.previousQuestIds != null && quest.previousQuestIds.Length > 0;
            
            if (!hasPrerequisites)
            {
                _questStates[quest.questId] = QuestState.Available;
            }
            else
            {
                _questStates[quest.questId] = QuestState.Locked;
            }
        }
        
        // Notify all quests that their state changed
        foreach (int questId in _questStates.Keys)
        {
            OnQuestStateChanged?.Invoke(questId);
        }
        
        Debug.Log("QuestDataManager: All quest progress has been reset.");
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
            if (_questStates[quest.questId] != QuestState.Locked)
            {
                continue;
            }
            
            // Check multiple prerequisites
            bool shouldUnlock = false;
            if (quest.previousQuestIds != null && quest.previousQuestIds.Length > 0)
            {
                bool allPrerequisitesMet = true;
                foreach (int prerequisiteId in quest.previousQuestIds)
                {
                    if (!_completedQuestIds.Contains(prerequisiteId))
                    {
                        allPrerequisitesMet = false;
                        break;
                    }
                }
                shouldUnlock = allPrerequisitesMet;
            }
            
            if (shouldUnlock)
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
    
    public List<QuestData> GetAllQuests()
    {
        return new List<QuestData>(_questDataDict.Values);
    }
    
    public List<QuestData> GetQuestsByProvider(QuestProvider provider)
    {
        return _questDataDict.Values
            .Where(quest => quest.questProvider == provider)
            .ToList();
    }
    
    public List<QuestData> GetCurrentQuestsByProvider(QuestProvider provider)
    {
        return _questDataDict.Values
            .Where(quest => quest.questProvider == provider && _questStates[quest.questId] != QuestState.Locked)
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

