using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum QuestState
{
    Locked, // Quest is locked (prerequisite not met)
    Available, // Quest is available to be picked up
    Active, // Quest is currently active
    Completable, // Quest is active and all requirements are met
    Completed // Quest has been completed
}

public class QuestDataManager : MonoBehaviour
{
    [Header("Quest List")]
    [SerializeField] private List<QuestData> allQuests = new List<QuestData>();
    private readonly HashSet<int> _activeQuestIds = new HashSet<int>();
    private readonly HashSet<int> _completedQuestIds = new HashSet<int>();

    private readonly Dictionary<int, QuestData> _questDataDict = new Dictionary<int, QuestData>();
    private readonly Dictionary<int, QuestState> _questStates = new Dictionary<int, QuestState>();

    private BaseInventoryManager _baseInventoryManager;
    private InventorySystem _inventorySystem;
    public static QuestDataManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) {
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

        StartCoroutine(SubscribeToInventoryEvents());

        // Initialize QuestTracker if not already initialized
        if (FindFirstObjectByType<QuestTracker>() == null) {
            GameObject questTrackerObj = new GameObject("QuestTracker");
            questTrackerObj.AddComponent<QuestTracker>();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeFromInventoryEvents();
    }

    private void OnDestroy()
    {
        SaveQuestProgress();
        UnsubscribeFromInventoryEvents();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) {
            SaveQuestProgress();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) {
            SaveQuestProgress();
        }
    }

    public event Action<int> OnQuestStateChanged;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _baseInventoryManager = null;
        _inventorySystem = null;
        UnsubscribeFromInventoryEvents();
        StartCoroutine(SubscribeToInventoryEvents());
    }

    private IEnumerator SubscribeToInventoryEvents()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == "BaseScene") {
            while (_baseInventoryManager == null) {
                _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
                yield return null;
            }
            _baseInventoryManager.OnResourceChanged += OnInventoryResourceChanged;
        }
        else if (sceneName == "GameScene") {
            while (_inventorySystem == null) {
                _inventorySystem = FindFirstObjectByType<InventorySystem>();
                yield return null;
            }
            StartCoroutine(PeriodicallyCheckInventoryResources());
        }
    }

    private void UnsubscribeFromInventoryEvents()
    {
        if (_baseInventoryManager != null) {
            _baseInventoryManager.OnResourceChanged -= OnInventoryResourceChanged;
        }
    }

    private IEnumerator PeriodicallyCheckInventoryResources()
    {
        while (_inventorySystem != null && SceneManager.GetActiveScene().name == "GameScene") {
            yield return new WaitForSeconds(0.5f);
            CheckAllActiveQuests();
        }
    }

    private void OnInventoryResourceChanged(ResourceType resourceType, int amount)
    {
        foreach (QuestData quest in _questDataDict.Values) {
            QuestState currentState = _questStates[quest.questId];

            // Only check Active and Completable quests
            if (currentState != QuestState.Active && currentState != QuestState.Completable) {
                continue;
            }

            bool requiresThisResource = false;
            if (quest.requiredResources != null) {
                foreach (ResourceCost cost in quest.requiredResources) {
                    if (cost.resourceType == resourceType) {
                        requiresThisResource = true;
                        break;
                    }
                }
            }

            // Also check quest check requirements that might be affected by resource changes
            bool hasQuestCheckRequirements = quest.questCheckRequirements != null && quest.questCheckRequirements.Length > 0;

            if (!requiresThisResource && !hasQuestCheckRequirements) {
                continue;
            }

            bool requirementsMet = AreAllQuestRequirementsMet(quest);

            // Handle state transitions
            if (currentState == QuestState.Active && requirementsMet) {
                // Transition from Active to Completable
                _questStates[quest.questId] = QuestState.Completable;
                OnQuestStateChanged?.Invoke(quest.questId);
                SaveQuestProgress();
            }
            else if (currentState == QuestState.Completable && !requirementsMet) {
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

        if (loadedQuests != null && loadedQuests.Length > 0) {
            allQuests.Clear();
            allQuests.AddRange(loadedQuests);
        }
        else {
            Debug.LogWarning("No quests found in Resources/Quest Data folder. Make sure QuestData assets are placed in Resources/Quest Data/");
        }
    }

    private void InitializeQuests()
    {
        _questDataDict.Clear();
        _questStates.Clear();
        _completedQuestIds.Clear();
        _activeQuestIds.Clear();

        foreach (QuestData quest in allQuests) {
            if (quest == null) continue;

            _questDataDict[quest.questId] = quest;

            // Check if quest has prerequisites
            // -1 means no prerequisites (first quest, always available)
            bool hasPrerequisites = HasRealPrerequisites(quest.previousQuestIds);

            if (!hasPrerequisites) {
                _questStates[quest.questId] = QuestState.Available;
            }
            else {
                _questStates[quest.questId] = QuestState.Locked;
            }
        }
    }

    private bool HasRealPrerequisites(int[] previousQuestIds)
    {
        // -1 means no prerequisites (first quest, always available)
        if (previousQuestIds == null || previousQuestIds.Length == 0) {
            return false;
        }

        // If array contains -1, it means no prerequisites
        if (previousQuestIds.Contains(-1)) {
            return false;
        }

        // Check if all entries are -1 (Unity serializes empty arrays as containing -1)
        bool allMinusOne = true;
        foreach (int id in previousQuestIds) {
            if (id != -1) {
                allMinusOne = false;
                break;
            }
        }

        return !allMinusOne;
    }

    private bool CanStartQuest(int questId)
    {
        if (!_questDataDict.ContainsKey(questId)) {
            Debug.LogWarning($"Quest with ID {questId} not found.");
            return false;
        }

        QuestState currentState = _questStates[questId];

        if (currentState != QuestState.Available) {
            return false;
        }

        QuestData quest = _questDataDict[questId];

        // Check multiple prerequisites
        // -1 means no prerequisites (skip check)
        if (HasRealPrerequisites(quest.previousQuestIds)) {
            foreach (int prerequisiteId in quest.previousQuestIds) {
                // Skip -1 entries
                if (prerequisiteId == -1) continue;

                if (!_completedQuestIds.Contains(prerequisiteId)) {
                    return false;
                }
            }
        }

        return true;
    }

    public bool StartQuest(int questId)
    {
        if (!CanStartQuest(questId)) {
            Debug.LogWarning($"Cannot start quest {questId}. Prerequisites may not be met or quest is not available.");
            return false;
        }

        _activeQuestIds.Add(questId);
        _questStates[questId] = QuestState.Active;

        OnQuestStateChanged?.Invoke(questId);
        SaveQuestProgress();

        if (_questDataDict.ContainsKey(questId)) {
            CheckSingleQuestCompletion(questId);
        }

        return true;
    }

    private void CheckSingleQuestCompletion(int questId)
    {
        if (!_questDataDict.ContainsKey(questId)) {
            return;
        }

        QuestData quest = _questDataDict[questId];
        QuestState currentState = _questStates[questId];

        if (currentState == QuestState.Completable) {
            // Completable quests already have all requirements met, but don't auto-complete
            // They wait for user to click the complete button
            return;
        }

        if (currentState != QuestState.Active) {
            return;
        }

        if (AreAllQuestRequirementsMet(quest)) {
            // Transition to Completable state instead of completing directly
            _questStates[questId] = QuestState.Completable;
            OnQuestStateChanged?.Invoke(questId);
            SaveQuestProgress();
        }
    }

    private bool AreAllQuestRequirementsMet(QuestData quest)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        Dictionary<ResourceType, int> inventoryResources = null;

        if (sceneName == "BaseScene") {
            if (_baseInventoryManager == null) {
                _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
            }

            if (_baseInventoryManager == null) {
                return false;
            }

            inventoryResources = _baseInventoryManager.GetAllResources();
        }
        else if (sceneName == "GameScene") {
            if (_inventorySystem == null) {
                _inventorySystem = FindFirstObjectByType<InventorySystem>();
            }

            if (_inventorySystem == null) {
                return false;
            }

            inventoryResources = _inventorySystem.GetAllResourcesFromInventory();
        }
        else {
            return false;
        }

        if (inventoryResources == null) {
            return false;
        }

        // Check resource requirements
        if (quest.requiredResources != null && quest.requiredResources.Length > 0) {
            foreach (ResourceCost cost in quest.requiredResources) {
                inventoryResources.TryGetValue(cost.resourceType, out int currentAmount);
                if (currentAmount < cost.amount) {
                    return false;
                }
            }
        }

        // Check quest tracking requirements
        if (quest.questCheckRequirements != null && quest.questCheckRequirements.Length > 0) {
            foreach (QuestCheckData checkData in quest.questCheckRequirements) {
                if (!checkData.IsRequirementMet()) {
                    return false;
                }
            }
        }

        return true;
    }

    private void CheckAllActiveQuests()
    {
        foreach (QuestData quest in _questDataDict.Values) {
            QuestState currentState = _questStates[quest.questId];

            if (currentState != QuestState.Active && currentState != QuestState.Completable) {
                continue;
            }

            if (AreAllQuestRequirementsMet(quest)) {
                if (currentState == QuestState.Active) {
                    _questStates[quest.questId] = QuestState.Completable;
                    OnQuestStateChanged?.Invoke(quest.questId);
                    SaveQuestProgress();
                }
            }
            else {
                if (currentState == QuestState.Completable) {
                    _questStates[quest.questId] = QuestState.Active;
                    OnQuestStateChanged?.Invoke(quest.questId);
                    SaveQuestProgress();
                }
            }
        }
    }

    public bool CheckQuestCompletion(int questId)
    {
        if (!_questDataDict.ContainsKey(questId)) {
            return false;
        }

        QuestData quest = _questDataDict[questId];
        QuestState currentState = _questStates[questId];

        // Completable state means requirements are already met
        if (currentState == QuestState.Completable) {
            return true;
        }

        if (currentState != QuestState.Active) {
            return false;
        }

        if (QuestTester.IsTestModeEnabled) {
            return true;
        }

        return AreAllQuestRequirementsMet(quest);
    }

    public void ResetAllQuestProgress()
    {
        _questStates.Clear();
        _completedQuestIds.Clear();
        _activeQuestIds.Clear();

        // Clear saved progress from PlayerPrefs
        ClearSavedQuestProgress();

        // Reset quest check data progress
        foreach (QuestData quest in _questDataDict.Values) {
            if (quest == null) continue;

            if (quest.questCheckRequirements != null) {
                foreach (QuestCheckData checkData in quest.questCheckRequirements) {
                    checkData.ResetProgress();
                }
            }
        }

        QuestTracker questTracker = FindFirstObjectByType<QuestTracker>();
        if (questTracker != null) {
            questTracker.ResetAllQuestProgress();
        }

        // Clear quest progress from all QuestUIHandler instances (there may be multiple - one for each UI provider)
        QuestUIHandler[] questUIHandlers = FindObjectsByType<QuestUIHandler>(FindObjectsSortMode.None);
        foreach (QuestUIHandler questUIHandler in questUIHandlers) {
            if (questUIHandler != null) {
                questUIHandler.ClearQuestProgress();
            }
        }

        // Re-initialize all quests to their default states
        foreach (QuestData quest in _questDataDict.Values) {
            if (quest == null) continue;

            // Check if quest has prerequisites
            // -1 means no prerequisites (first quest, always available)
            bool hasPrerequisites = HasRealPrerequisites(quest.previousQuestIds);

            if (!hasPrerequisites) {
                _questStates[quest.questId] = QuestState.Available;
            }
            else {
                _questStates[quest.questId] = QuestState.Locked;
            }
        }

        // Notify all quests that their state changed
        foreach (int questId in _questStates.Keys) {
            OnQuestStateChanged?.Invoke(questId);
        }
    }

    private void SaveQuestProgress()
    {
        foreach (KeyValuePair<int, QuestState> kvp in _questStates) {
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
        foreach (QuestData quest in _questDataDict.Values) {
            if (quest == null) continue;

            string stateKey = $"QuestState_{quest.questId}";
            if (PlayerPrefs.HasKey(stateKey)) {
                int savedState = PlayerPrefs.GetInt(stateKey);
                _questStates[quest.questId] = (QuestState)savedState;

                if (_questStates[quest.questId] == QuestState.Active ||
                    _questStates[quest.questId] == QuestState.Completable) {
                    _activeQuestIds.Add(quest.questId);
                }

                if (_questStates[quest.questId] == QuestState.Completed) {
                    _completedQuestIds.Add(quest.questId);
                }
            }
        }

        if (PlayerPrefs.HasKey("QuestActiveIds")) {
            string activeIds = PlayerPrefs.GetString("QuestActiveIds");
            if (!string.IsNullOrEmpty(activeIds)) {
                string[] ids = activeIds.Split(',');
                foreach (string idStr in ids) {
                    if (int.TryParse(idStr, out int questId)) {
                        _activeQuestIds.Add(questId);
                    }
                }
            }
        }

        if (PlayerPrefs.HasKey("QuestCompletedIds")) {
            string completedIds = PlayerPrefs.GetString("QuestCompletedIds");
            if (!string.IsNullOrEmpty(completedIds)) {
                string[] ids = completedIds.Split(',');
                foreach (string idStr in ids) {
                    if (int.TryParse(idStr, out int questId)) {
                        _completedQuestIds.Add(questId);
                    }
                }
            }
        }

        foreach (int completedId in _completedQuestIds) {
            UnlockDependentQuests(completedId);

            if (_questDataDict.ContainsKey(completedId)) {
                QuestData quest = _questDataDict[completedId];
                if (quest != null && quest.questFinishReward != null && quest.questFinishReward.unlockedBuildings != null && quest.questFinishReward.unlockedBuildings.Length > 0) {
                    if (BuildingUnlockManager.Instance == null) {
                        GameObject unlockManagerObj = new GameObject("BuildingUnlockManager");
                        unlockManagerObj.AddComponent<BuildingUnlockManager>();
                    }

                    if (BuildingUnlockManager.Instance != null) {
                        BuildingUnlockManager.Instance.UnlockBuildings(quest.questFinishReward.unlockedBuildings);
                    }
                }
            }
        }

        foreach (int questId in _questStates.Keys) {
            OnQuestStateChanged?.Invoke(questId);
        }
    }

    private void ClearSavedQuestProgress()
    {
        foreach (QuestData quest in _questDataDict.Values) {
            if (quest == null) continue;
            string stateKey = $"QuestState_{quest.questId}";
            if (PlayerPrefs.HasKey(stateKey)) {
                PlayerPrefs.DeleteKey(stateKey);
            }
        }

        if (PlayerPrefs.HasKey("QuestActiveIds")) {
            PlayerPrefs.DeleteKey("QuestActiveIds");
        }

        if (PlayerPrefs.HasKey("QuestCompletedIds")) {
            PlayerPrefs.DeleteKey("QuestCompletedIds");
        }

        PlayerPrefs.Save();
    }

    public bool CompleteQuest(int questId)
    {
        if (!_questDataDict.ContainsKey(questId)) {
            Debug.LogWarning($"Quest with ID {questId} not found.");
            return false;
        }

        QuestState currentState = _questStates[questId];

        if (currentState != QuestState.Active && currentState != QuestState.Completable) {
            Debug.LogWarning($"Quest {questId} is {currentState} and cannot be completed. Quest must be Active or Completable.");
            return false;
        }

        _questStates[questId] = QuestState.Completed;
        _completedQuestIds.Add(questId);
        _activeQuestIds.Remove(questId);
        OnQuestStateChanged?.Invoke(questId);
        SaveQuestProgress();

        QuestData quest = _questDataDict[questId];
        if (quest != null && quest.questFinishReward != null && quest.questFinishReward.unlockedBuildings != null && quest.questFinishReward.unlockedBuildings.Length > 0) {
            if (BuildingUnlockManager.Instance == null) {
                GameObject unlockManagerObj = new GameObject("BuildingUnlockManager");
                unlockManagerObj.AddComponent<BuildingUnlockManager>();
            }

            if (BuildingUnlockManager.Instance != null) {
                BuildingUnlockManager.Instance.UnlockBuildings(quest.questFinishReward.unlockedBuildings);
            }
        }

        return true;
    }

    public bool FinishQuest(int questId)
    {
        if (!_questDataDict.ContainsKey(questId)) {
            Debug.LogWarning($"Quest with ID {questId} not found.");
            return false;
        }

        QuestState currentState = _questStates[questId];

        if (currentState != QuestState.Completed) {
            Debug.LogWarning($"Quest {questId} is {currentState} and cannot be finished. Quest must be Completed.");
            return false;
        }

        UnlockDependentQuests(questId);

        return true;
    }

    private void UnlockDependentQuests(int completedQuestId)
    {
        foreach (QuestData quest in _questDataDict.Values) {
            if (_questStates[quest.questId] != QuestState.Locked) {
                continue;
            }

            // Check multiple prerequisites
            // -1 means no prerequisites (skip check)
            bool shouldUnlock = false;
            if (HasRealPrerequisites(quest.previousQuestIds)) {
                bool allPrerequisitesMet = true;
                foreach (int prerequisiteId in quest.previousQuestIds) {
                    // Skip -1 entries
                    if (prerequisiteId == -1) continue;

                    if (!_completedQuestIds.Contains(prerequisiteId)) {
                        allPrerequisitesMet = false;
                        break;
                    }
                }
                shouldUnlock = allPrerequisitesMet;
            }
            else {
                // No prerequisites means it should already be available (handled in InitializeQuests)
                shouldUnlock = false;
            }

            if (shouldUnlock) {
                _questStates[quest.questId] = QuestState.Available;
                OnQuestStateChanged?.Invoke(quest.questId);
                SaveQuestProgress();
            }
        }
    }

    public QuestState GetQuestState(int questId)
    {
        if (_questStates.ContainsKey(questId)) {
            return _questStates[questId];
        }
        return QuestState.Locked;
    }

    public QuestData GetQuestData(int questId)
    {
        if (_questDataDict.ContainsKey(questId)) {
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
        List<QuestData> matchingProviderQuests = _questDataDict.Values
            .Where(quest => quest.questProvider == provider)
            .ToList();

        List<QuestData> unlockedQuests = matchingProviderQuests
            .Where(quest =>
                _questStates.ContainsKey(quest.questId) &&
                _questStates[quest.questId] != QuestState.Locked)
            .ToList();

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
