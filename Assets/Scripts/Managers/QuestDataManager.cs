using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum QuestState
{
    Locked,     // Quest is locked (prerequisite not met)
    Available,  // Quest is available to be picked up
    Active,     // Quest is currently active
    Completable, // Quest is active and all requirements are met
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
        LoadQuestProgress();
        
        StartCoroutine(SubscribeToBaseInventoryEvents());
        
        // Initialize QuestTracker if not already initialized
        if (FindFirstObjectByType<QuestTracker>() == null)
        {
            GameObject questTrackerObj = new GameObject("QuestTracker");
            questTrackerObj.AddComponent<QuestTracker>();
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveQuestProgress();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveQuestProgress();
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
        SaveQuestProgress();
        
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
            
            // Only check Active and Completable quests
            if (currentState != QuestState.Active && currentState != QuestState.Completable)
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

            // Also check quest check requirements that might be affected by resource changes
            bool hasQuestCheckRequirements = quest.questCheckRequirements != null && quest.questCheckRequirements.Length > 0;

            if (!requiresThisResource && !hasQuestCheckRequirements)
            {
                continue;
            }

            bool requirementsMet = AreAllQuestRequirementsMet(quest);

            // Handle state transitions
            if (currentState == QuestState.Active && requirementsMet)
            {
                // Transition from Active to Completable
                _questStates[quest.questId] = QuestState.Completable;
                OnQuestStateChanged?.Invoke(quest.questId);
                SaveQuestProgress();
            }
            else if (currentState == QuestState.Completable && !requirementsMet)
            {
                // Transition from Completable back to Active
                _questStates[quest.questId] = QuestState.Active;
                OnQuestStateChanged?.Invoke(quest.questId);
                SaveQuestProgress();
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
            // -1 means no prerequisites (first quest, always available)
            bool hasPrerequisites = HasRealPrerequisites(quest.previousQuestIds);
            
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

    private bool HasRealPrerequisites(int[] previousQuestIds)
    {
        // -1 means no prerequisites (first quest, always available)
        if (previousQuestIds == null || previousQuestIds.Length == 0)
        {
            return false;
        }
        
        // If array contains -1, it means no prerequisites
        if (previousQuestIds.Contains(-1))
        {
            return false;
        }
        
        // Check if all entries are -1 (Unity serializes empty arrays as containing -1)
        bool allMinusOne = true;
        foreach (int id in previousQuestIds)
        {
            if (id != -1)
            {
                allMinusOne = false;
                break;
            }
        }
        
        return !allMinusOne;
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
        // -1 means no prerequisites (skip check)
        if (HasRealPrerequisites(quest.previousQuestIds))
        {
            foreach (int prerequisiteId in quest.previousQuestIds)
            {
                // Skip -1 entries
                if (prerequisiteId == -1) continue;
                
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
        SaveQuestProgress();

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

        if (currentState == QuestState.Completable)
        {
            // Completable quests already have all requirements met, but don't auto-complete
            // They wait for user to click the complete button
            return;
        }

        if (currentState != QuestState.Active)
        {
            return;
        }

        if (AreAllQuestRequirementsMet(quest))
        {
            // Transition to Completable state instead of completing directly
            _questStates[questId] = QuestState.Completable;
            OnQuestStateChanged?.Invoke(questId);
            SaveQuestProgress();
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
        
        // Completable state means requirements are already met
        if (currentState == QuestState.Completable)
        {
            return true;
        }
        
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
        
        // Clear saved progress from PlayerPrefs
        ClearSavedQuestProgress();
        
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
        
        QuestUIHandler questUIHandler = FindFirstObjectByType<QuestUIHandler>();
        if (questUIHandler != null)
        {
            questUIHandler.ClearQuestProgress();
        }
        
        // Re-initialize all quests to their default states
        foreach (QuestData quest in _questDataDict.Values)
        {
            if (quest == null) continue;
            
            // Check if quest has prerequisites
            // -1 means no prerequisites (first quest, always available)
            bool hasPrerequisites = HasRealPrerequisites(quest.previousQuestIds);
            
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
    
    private void SaveQuestProgress()
    {
        foreach (var kvp in _questStates)
        {
            string key = $"QuestState_{kvp.Key}";
            PlayerPrefs.SetInt(key, (int)kvp.Value);
        }
        
        string activeIds = string.Join(",", _activeQuestIds);
        PlayerPrefs.SetString("QuestActiveIds", activeIds);
        
        string completedIds = string.Join(",", _completedQuestIds);
        PlayerPrefs.SetString("QuestCompletedIds", completedIds);
        
        PlayerPrefs.Save();
    }
    
    private void LoadQuestProgress()
    {
        foreach (QuestData quest in _questDataDict.Values)
        {
            if (quest == null) continue;
            
            string stateKey = $"QuestState_{quest.questId}";
            if (PlayerPrefs.HasKey(stateKey))
            {
                int savedState = PlayerPrefs.GetInt(stateKey);
                _questStates[quest.questId] = (QuestState)savedState;
                
                if (_questStates[quest.questId] == QuestState.Active || 
                    _questStates[quest.questId] == QuestState.Completable)
                {
                    _activeQuestIds.Add(quest.questId);
                }
                
                if (_questStates[quest.questId] == QuestState.Completed)
                {
                    _completedQuestIds.Add(quest.questId);
                }
            }
        }
        
        if (PlayerPrefs.HasKey("QuestActiveIds"))
        {
            string activeIds = PlayerPrefs.GetString("QuestActiveIds");
            if (!string.IsNullOrEmpty(activeIds))
            {
                string[] ids = activeIds.Split(',');
                foreach (string idStr in ids)
                {
                    if (int.TryParse(idStr, out int questId))
                    {
                        _activeQuestIds.Add(questId);
                    }
                }
            }
        }
        
        if (PlayerPrefs.HasKey("QuestCompletedIds"))
        {
            string completedIds = PlayerPrefs.GetString("QuestCompletedIds");
            if (!string.IsNullOrEmpty(completedIds))
            {
                string[] ids = completedIds.Split(',');
                foreach (string idStr in ids)
                {
                    if (int.TryParse(idStr, out int questId))
                    {
                        _completedQuestIds.Add(questId);
                    }
                }
            }
        }
        
        foreach (int completedId in _completedQuestIds)
        {
            UnlockDependentQuests(completedId);
        }
        
        foreach (int questId in _questStates.Keys)
        {
            OnQuestStateChanged?.Invoke(questId);
        }
    }
    
    private void ClearSavedQuestProgress()
    {
        foreach (QuestData quest in _questDataDict.Values)
        {
            if (quest == null) continue;
            string stateKey = $"QuestState_{quest.questId}";
            if (PlayerPrefs.HasKey(stateKey))
            {
                PlayerPrefs.DeleteKey(stateKey);
            }
        }
        
        if (PlayerPrefs.HasKey("QuestActiveIds"))
        {
            PlayerPrefs.DeleteKey("QuestActiveIds");
        }
        
        if (PlayerPrefs.HasKey("QuestCompletedIds"))
        {
            PlayerPrefs.DeleteKey("QuestCompletedIds");
        }
        
        PlayerPrefs.Save();
    }

    public bool CompleteQuest(int questId)
    {
        if (!_questDataDict.ContainsKey(questId))
        {
            Debug.LogWarning($"Quest with ID {questId} not found.");
            return false;
        }

        QuestState currentState = _questStates[questId];
        
        if (currentState != QuestState.Active && currentState != QuestState.Completable)
        {
            Debug.LogWarning($"Quest {questId} is {currentState} and cannot be completed. Quest must be Active or Completable.");
            return false;
        }

        _questStates[questId] = QuestState.Completed;
        _completedQuestIds.Add(questId);
        _activeQuestIds.Remove(questId);
        OnQuestStateChanged?.Invoke(questId);
        SaveQuestProgress();

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
            // -1 means no prerequisites (skip check)
            bool shouldUnlock = false;
            if (HasRealPrerequisites(quest.previousQuestIds))
            {
                bool allPrerequisitesMet = true;
                foreach (int prerequisiteId in quest.previousQuestIds)
                {
                    // Skip -1 entries
                    if (prerequisiteId == -1) continue;
                    
                    if (!_completedQuestIds.Contains(prerequisiteId))
                    {
                        allPrerequisitesMet = false;
                        break;
                    }
                }
                shouldUnlock = allPrerequisitesMet;
            }
            else
            {
                // No prerequisites means it should already be available (handled in InitializeQuests)
                shouldUnlock = false;
            }
            
            if (shouldUnlock)
            {
                _questStates[quest.questId] = QuestState.Available;
                OnQuestStateChanged?.Invoke(quest.questId);
                SaveQuestProgress();
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
        var matchingProviderQuests = _questDataDict.Values
            .Where(quest => quest.questProvider == provider)
            .ToList();
        
        var unlockedQuests = matchingProviderQuests
            .Where(quest => 
                _questStates.ContainsKey(quest.questId) && 
                _questStates[quest.questId] != QuestState.Locked)
            .ToList();
        
        if (unlockedQuests.Count == 0 && matchingProviderQuests.Count > 0)
        {
            // Debug.LogWarning($"QuestDataManager: Found {matchingProviderQuests.Count} quest(s) for provider {provider}, but all are locked. Quest states:");
            foreach (var quest in matchingProviderQuests)
            {
                QuestState state = _questStates.ContainsKey(quest.questId) ? _questStates[quest.questId] : QuestState.Locked;
                // Debug.Log($"  Quest {quest.questId} ({quest.questName}): State={state}");
            }
        }
        
        return unlockedQuests;
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

