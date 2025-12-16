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

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Info Panel")]
    [SerializeField] private QuestInfoPanel questInfoPanel;
    [SerializeField] private GameObject questCellPrefab;
    [SerializeField] private Transform questCellContentParent;

    [Header("Quest List")]
    [SerializeField] private List<QuestData> allQuests = new ();

    private readonly Dictionary<int, QuestData> _questDataDict = new ();
    private readonly List<QuestCell> _instantiatedQuestCells = new ();
    private readonly Dictionary<int, QuestState> _questStates = new ();
    private readonly HashSet<int> _completedQuestIds = new ();
    private readonly HashSet<int> _activeQuestIds = new ();

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
        InstantiateAvailableQuestCells();
        
        ResourceManager.OnResourceAmountChanged += CheckQuestCompletion;
    }

    private void OnDestroy()
    {
        ResourceManager.OnResourceAmountChanged -= CheckQuestCompletion;
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

    private void InstantiateAvailableQuestCells()
    {
        if (questCellPrefab == null || questCellContentParent == null || questInfoPanel == null)
        {
            return;
        }

        ClearQuestCells();
        List<QuestData> availableQuests = GetAvailableQuests();

        foreach (QuestData quest in availableQuests)
        {
            GameObject cellObject = Instantiate(questCellPrefab, questCellContentParent);
            QuestCell questCell = cellObject.GetComponent<QuestCell>();

            if (questCell != null)
            {
                questCell.Initialize(quest, questInfoPanel);
                _instantiatedQuestCells.Add(questCell);
            }
            else
            {
                Debug.LogWarning($"QuestCell component not found on prefab {questCellPrefab.name}");
                Destroy(cellObject);
            }
        }
    }

    private void ClearQuestCells()
    {
        foreach (QuestCell cell in _instantiatedQuestCells)
        {
            if (cell != null)
            {
                Destroy(cell.gameObject);
            }
        }
        _instantiatedQuestCells.Clear();

        if (questCellContentParent != null)
        {
            foreach (Transform child in questCellContentParent)
            {
                Destroy(child.gameObject);
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
        
        // Can only start if quest is available
        if (currentState != QuestState.Available)
        {
            return false;
        }

        // Check if prerequisite is met
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

        // Allow multiple active quests at the same time
        _activeQuestIds.Add(questId);
        _questStates[questId] = QuestState.Active;

        // Notify QuestInfoPanel to update UI (hide accept button)
        if (questInfoPanel != null)
        {
            questInfoPanel.RefreshQuestState(questId);
        }

        // Update quest cell badge (hide available badge when quest becomes active)
        UpdateQuestCellBadge(questId);

        // Check if quest is already complete when it becomes active
        // (player might have had the required resources before activating the quest)
        if (_questDataDict.ContainsKey(questId))
        {
            QuestData quest = _questDataDict[questId];
            CheckSingleQuestCompletion(questId);
        }

        return true;
    }

    private void CheckQuestCompletion(ResourceType resourceType, int amount)
    {
        if (ResourceManager.Instance == null)
        {
            return;
        }

        // Only check Active quests that require this resource type
        foreach (QuestData quest in _questDataDict.Values)
        {
            QuestState currentState = _questStates[quest.questId];
            
            // Only check Active quests (not Available/inactive quests)
            if (currentState != QuestState.Active)
            {
                continue;
            }

            // Check if this quest requires the resource type that just changed
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

            // Skip if this quest doesn't require the resource that changed
            if (!requiresThisResource)
            {
                continue;
            }

            // Check if all resource requirements are met
            if (AreAllQuestRequirementsMet(quest))
            {
                CompleteQuest(quest.questId);
            }
        }
    }

    private void CheckSingleQuestCompletion(int questId)
    {
        if (ResourceManager.Instance == null)
        {
            return;
        }

        if (!_questDataDict.ContainsKey(questId))
        {
            return;
        }

        QuestData quest = _questDataDict[questId];
        QuestState currentState = _questStates[questId];

        // Only check if quest is Active
        if (currentState != QuestState.Active)
        {
            return;
        }

        // Check if all resource requirements are met
        if (AreAllQuestRequirementsMet(quest))
        {
            CompleteQuest(questId);
        }
    }

    private bool AreAllQuestRequirementsMet(QuestData quest)
    {
        if (ResourceManager.Instance == null)
        {
            return false;
        }

        if (quest.requiredResources == null || quest.requiredResources.Length == 0)
        {
            // If no resources are required, consider it complete
            return true;
        }

        // Check if all required resources meet the required amounts
        foreach (ResourceCost cost in quest.requiredResources)
        {
            int currentAmount = ResourceManager.Instance.GetResourceAmount(cost.resourceType);
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
        
        // Only allow completing Active quests
        if (currentState != QuestState.Active)
        {
            Debug.LogWarning($"Quest {questId} is {currentState} and cannot be completed. Quest must be Active.");
            return false;
        }

        _questStates[questId] = QuestState.Completed;
        _completedQuestIds.Add(questId);
        
        // Remove from active quests
        _activeQuestIds.Remove(questId);

        // Notify QuestInfoPanel to show finish button
        if (questInfoPanel != null)
        {
            questInfoPanel.RefreshQuestState(questId);
        }

        // Update quest cell badge to show finished state
        UpdateQuestCellBadge(questId);
        
        // Update all quest cell badges in case state changes affect other cells
        UpdateAllQuestCellBadges();

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
        
        // Only allow finishing Completed quests
        if (currentState != QuestState.Completed)
        {
            Debug.LogWarning($"Quest {questId} is {currentState} and cannot be finished. Quest must be Completed.");
            return false;
        }

        // Remove the quest cell from the UI
        RemoveQuestCell(questId);

        // Unlock quests that depend on this one
        UnlockDependentQuests(questId);

        return true;
    }

    private void RemoveQuestCell(int questId)
    {
        for (int i = _instantiatedQuestCells.Count - 1; i >= 0; i--)
        {
            QuestCell cell = _instantiatedQuestCells[i];
            if (cell != null && cell.GetQuestData() != null && cell.GetQuestData().questId == questId)
            {
                Destroy(cell.gameObject);
                _instantiatedQuestCells.RemoveAt(i);
            }
        }
    }

    private void UpdateQuestCellBadge(int questId)
    {
        foreach (QuestCell cell in _instantiatedQuestCells)
        {
            if (cell != null && cell.GetQuestData() != null && cell.GetQuestData().questId == questId)
            {
                cell.UpdateBadge();
            }
        }
    }

    private void UnlockDependentQuests(int completedQuestId)
    {
        foreach (QuestData quest in _questDataDict.Values)
        {
            if (quest.previousQuestId == completedQuestId && 
                _questStates[quest.questId] == QuestState.Locked)
            {
                _questStates[quest.questId] = QuestState.Available;
                
                // Instantiate a new quest cell for the newly unlocked quest
                if (questCellPrefab != null && questCellContentParent != null && questInfoPanel != null)
                {
                    GameObject cellObject = Instantiate(questCellPrefab, questCellContentParent);
                    QuestCell questCell = cellObject.GetComponent<QuestCell>();

                    if (questCell != null)
                    {
                        questCell.Initialize(quest, questInfoPanel);
                        _instantiatedQuestCells.Add(questCell);
                    }
                }
            }
        }
    }

    private void UpdateAllQuestCellBadges()
    {
        foreach (QuestCell cell in _instantiatedQuestCells)
        {
            if (cell != null)
            {
                cell.UpdateBadge();
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

