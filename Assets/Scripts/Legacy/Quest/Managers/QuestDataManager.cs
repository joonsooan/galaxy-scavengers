using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum QuestState
{
    Locked,
    Available,
    Active,
    Completable,
    Completed,
    Finished
}

public class QuestDataManager : MonoBehaviour
{
    private static readonly WaitForSeconds _checkInventoryWait = CoroutineCache.GetWaitForSeconds(0.5f);
    [Header("Quest List")]
    [SerializeField] private List<QuestData> allQuests = new List<QuestData>();
    private readonly HashSet<int> _activeQuestIds = new HashSet<int>();
    private readonly HashSet<int> _completedQuestIds = new HashSet<int>();

    private readonly Dictionary<int, QuestData> _questDataDict = new Dictionary<int, QuestData>();
    private readonly Dictionary<int, QuestState> _questStates = new Dictionary<int, QuestState>();
    private readonly HashSet<int> _questsVisitedGameScene = new HashSet<int>();
    private readonly HashSet<int> _questsReturnedSuccessfully = new HashSet<int>();
    private readonly HashSet<int> _questsReturnedWithFailure = new HashSet<int>();

    private BaseInventoryManager _baseInventoryManager;
    private InventorySystem _inventorySystem;
    public static QuestDataManager Instance { get; private set; }
    public bool IsInitialized { get; private set; }
    public event Action OnInitialized;

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
        IsInitialized = true;
        OnInitialized?.Invoke();

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
        
        if (scene.name == "GameScene")
        {
            foreach (int questId in _activeQuestIds)
            {
                if (_questStates.ContainsKey(questId) && _questStates[questId] == QuestState.Active)
                {
                    _questsVisitedGameScene.Add(questId);
                }
            }
        }
        else if (scene.name == "BaseScene")
        {
            StartCoroutine(CheckDefaultQuestsAfterSceneLoad());
        }
    }
    
    private IEnumerator CheckDefaultQuestsAfterSceneLoad()
    {
        yield return null;
        yield return null;
        
        foreach (QuestData quest in _questDataDict.Values)
        {
            QuestState currentState = GetQuestState(quest.questId);
            
            if (currentState != QuestState.Active)
            {
                continue;
            }
            
            bool visitedGameScene = _questsVisitedGameScene.Contains(quest.questId);
            
            if (!visitedGameScene)
            {
                continue;
            }
            
            if (quest.questCheckRequirements != null && quest.questCheckRequirements.Length > 0)
            {
                bool hasSuccessCheck = false;
                bool hasFailureCheck = false;
                foreach (QuestCheckData checkData in quest.questCheckRequirements)
                {
                    if (checkData.checkType == QuestCheckType.ReturnFromGameSceneSuccess)
                    {
                        hasSuccessCheck = true;
                    }
                    else if (checkData.checkType == QuestCheckType.ReturnFromGameSceneFailure)
                    {
                        hasFailureCheck = true;
                    }
                }
                
                if (hasSuccessCheck && _questsReturnedSuccessfully.Contains(quest.questId))
                {
                    CheckSingleQuestCompletion(quest.questId);
                }
                else if (hasFailureCheck && _questsReturnedWithFailure.Contains(quest.questId))
                {
                    CheckSingleQuestCompletion(quest.questId);
                }
            }
        }
    }

    public void MarkQuestReturnedSuccessfully(int questId)
    {
        if (_questsVisitedGameScene.Contains(questId))
        {
            _questsReturnedSuccessfully.Add(questId);
        }
    }

    public void MarkQuestReturnedWithFailure(int questId)
    {
        if (_questsVisitedGameScene.Contains(questId))
        {
            _questsReturnedWithFailure.Add(questId);
        }
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
            
            while (ResourceDataManager.Instance == null) {
                yield return null;
            }
            ResourceDataManager.OnResourceAmountChanged += OnResourceDataManagerResourceChanged;
        }
    }

    private void UnsubscribeFromInventoryEvents()
    {
        if (_baseInventoryManager != null) {
            _baseInventoryManager.OnResourceChanged -= OnInventoryResourceChanged;
        }
        
        ResourceDataManager.OnResourceAmountChanged -= OnResourceDataManagerResourceChanged;
    }
    
    private void OnResourceDataManagerResourceChanged(ResourceType resourceType, int amount)
    {
        foreach (QuestData quest in _questDataDict.Values) {
            QuestState currentState = _questStates[quest.questId];

            if (currentState != QuestState.Active && currentState != QuestState.Completable) {
                continue;
            }

            if (quest.questType != QuestType.RequestQuest) {
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

            bool hasQuestCheckRequirements = quest.questCheckRequirements != null && quest.questCheckRequirements.Length > 0;

            if (!requiresThisResource && !hasQuestCheckRequirements) {
                continue;
            }

            bool requirementsMet = AreAllQuestRequirementsMet(quest);

            if (currentState == QuestState.Active && requirementsMet) {
                _questStates[quest.questId] = QuestState.Completable;
                OnQuestStateChanged?.Invoke(quest.questId);
                SaveQuestProgress();
            }
            else if (currentState == QuestState.Completable && !requirementsMet) {
                _questStates[quest.questId] = QuestState.Active;
                OnQuestStateChanged?.Invoke(quest.questId);
                SaveQuestProgress();
            }
        }
    }

    private IEnumerator PeriodicallyCheckInventoryResources()
    {
        while (_inventorySystem != null && SceneManager.GetActiveScene().name == "GameScene") {
            yield return _checkInventoryWait;
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
        QuestData[] loadedQuests = Resources.LoadAll<QuestData>("Legacy/QuestRuntime/Quest Data");

        if (loadedQuests != null && loadedQuests.Length > 0) {
            allQuests.Clear();
            allQuests.AddRange(loadedQuests);
        }
        else {
            Debug.LogWarning("No quests found in Resources/Quest Data folder. Make sure QuestData assets are placed in Resources/Quest Data/");
        }
    }

    public void RegisterRuntimeQuest(QuestData questData)
    {
        if (questData == null) return;

        ResetQuestCheckRuntimeState(questData);

        if (!allQuests.Contains(questData)) {
            allQuests.Add(questData);
        }

        bool isNewQuest = !_questDataDict.ContainsKey(questData.questId);
        if (isNewQuest) {
            _questDataDict[questData.questId] = questData;
            _questStates[questData.questId] = QuestState.Available;
            OnQuestStateChanged?.Invoke(questData.questId);
            SaveQuestProgress();
        }
    }

    public void UnregisterRuntimeQuest(int questId)
    {
        if (!_questDataDict.ContainsKey(questId)) {
            return;
        }

        QuestData quest = _questDataDict[questId];

        _questDataDict.Remove(questId);
        _questStates.Remove(questId);
        _activeQuestIds.Remove(questId);
        _completedQuestIds.Remove(questId);
        _questsVisitedGameScene.Remove(questId);
        _questsReturnedSuccessfully.Remove(questId);
        _questsReturnedWithFailure.Remove(questId);

        if (quest != null && allQuests.Contains(quest)) {
            allQuests.Remove(quest);
        }
        else {
            allQuests.RemoveAll(q => q != null && q.questId == questId);
        }

        string stateKey = $"QuestState_{questId}";
        if (PlayerPrefs.HasKey(stateKey)) {
            PlayerPrefs.DeleteKey(stateKey);
        }

        SaveQuestProgress();
        OnQuestStateChanged?.Invoke(questId);
    }

    private void InitializeQuests()
    {
        _questDataDict.Clear();
        _questStates.Clear();
        _completedQuestIds.Clear();
        _activeQuestIds.Clear();
        _questsVisitedGameScene.Clear();

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

        if (_questDataDict.TryGetValue(questId, out QuestData questToStart))
        {
            ResetQuestCheckRuntimeState(questToStart);
        }

        QuestTracker questTracker = FindFirstObjectByType<QuestTracker>();
        if (questTracker != null)
        {
            questTracker.ResetQuestProgressForQuest(questId);
        }

        string currentScene = SceneManager.GetActiveScene().name;
        _activeQuestIds.Add(questId);
        _questStates[questId] = QuestState.Active;

        if (_questDataDict.ContainsKey(questId))
        {
            QuestData quest = _questDataDict[questId];
        }

        OnQuestStateChanged?.Invoke(questId);
        SaveQuestProgress();

        if (_questDataDict.ContainsKey(questId)) {
            QuestData quest = _questDataDict[questId];
            if (quest.questType != QuestType.BaseQuest)
            {
                CheckSingleQuestCompletion(questId);
            }
        }

        return true;
    }

    private static void ResetQuestCheckRuntimeState(QuestData quest)
    {
        if (quest == null || quest.questCheckRequirements == null)
        {
            return;
        }

        foreach (QuestCheckData checkData in quest.questCheckRequirements)
        {
            if (checkData == null) continue;
            checkData.ResetProgress();
        }
    }

    private void CheckSingleQuestCompletion(int questId)
    {
        if (!_questDataDict.ContainsKey(questId)) {
            return;
        }

        QuestData quest = _questDataDict[questId];
        QuestState currentState = _questStates[questId];

        if (currentState == QuestState.Completable) {
            return;
        }

        if (currentState != QuestState.Active) {
            return;
        }

        if (AreAllQuestRequirementsMet(quest)) {
            _questStates[questId] = QuestState.Completable;
            OnQuestStateChanged?.Invoke(questId);
            SaveQuestProgress();
        }
    }

    private bool AreAllQuestRequirementsMet(QuestData quest)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        Dictionary<ResourceType, int> inventoryResources = null;
        

        if (quest.questType == QuestType.BaseQuest) {
            if (sceneName == "BaseScene") {
                if (_baseInventoryManager == null) {
                    _baseInventoryManager = FindFirstObjectByType<BaseInventoryManager>();
                }

                if (_baseInventoryManager == null) {
                    return false;
                }

                inventoryResources = _baseInventoryManager.GetAllResources();
            }
            else {
                return false;
            }
        }
        else if (quest.questType == QuestType.RequestQuest) {
            if (sceneName == "GameScene") {
                if (ResourceDataManager.Instance == null) {
                    return false;
                }

                inventoryResources = new Dictionary<ResourceType, int>();
                foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType))) {
                    inventoryResources[type] = ResourceDataManager.Instance.GetResourceAmount(type);
                }
            }
            else {
                return false;
            }
        }
        else {
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
        }

        if (inventoryResources == null) {
            return false;
        }

        if (quest.requiredResources != null && quest.requiredResources.Length > 0) {
            foreach (ResourceCost cost in quest.requiredResources) {
                inventoryResources.TryGetValue(cost.resourceType, out int currentAmount);
                if (currentAmount < cost.amount) {
                    return false;
                }
            }
        }

        if (quest.questCheckRequirements != null && quest.questCheckRequirements.Length > 0) {
            foreach (QuestCheckData checkData in quest.questCheckRequirements) {
                if (checkData.checkType == QuestCheckType.ReturnFromGameSceneSuccess)
                {
                    bool isMet = _questsReturnedSuccessfully.Contains(quest.questId);
                    if (!isMet)
                    {
                        return false;
                    }
                }
                else if (checkData.checkType == QuestCheckType.ReturnFromGameSceneFailure)
                {
                    bool isMet = _questsReturnedWithFailure.Contains(quest.questId);
                    if (!isMet)
                    {
                        return false;
                    }
                }
                else
                {
                    bool isMet = checkData.IsRequirementMet();
                    if (!isMet) {
                        return false;
                    }
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
        _questsVisitedGameScene.Clear();
        _questsReturnedSuccessfully.Clear();
        _questsReturnedWithFailure.Clear();

        ClearSavedQuestProgress();

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

        QuestUIHandler[] questUIHandlers = FindObjectsByType<QuestUIHandler>(FindObjectsSortMode.None);
        foreach (QuestUIHandler questUIHandler in questUIHandlers) {
            if (questUIHandler != null) {
                questUIHandler.ClearQuestProgress();
                questUIHandler.RefreshQuestUI();
            }
        }

        foreach (QuestData quest in _questDataDict.Values) {
            if (quest == null) continue;

            bool hasPrerequisites = HasRealPrerequisites(quest.previousQuestIds);

            if (!hasPrerequisites) {
                _questStates[quest.questId] = QuestState.Available;
            }
            else {
                _questStates[quest.questId] = QuestState.Locked;
            }
        }

        foreach (int questId in _questStates.Keys) {
            OnQuestStateChanged?.Invoke(questId);
        }

        SaveQuestProgress();

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
                QuestState loadedState = (QuestState)savedState;

                _questStates[quest.questId] = loadedState;

                if (_questStates[quest.questId] == QuestState.Active ||
                    _questStates[quest.questId] == QuestState.Completable) {
                    _activeQuestIds.Add(quest.questId);
                }

                if (_questStates[quest.questId] == QuestState.Completed ||
                    _questStates[quest.questId] == QuestState.Finished) {
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

            if (quest.questCheckRequirements != null) {
                for (int i = 0; i < quest.questCheckRequirements.Length; i++) {
                    string progressKey = $"QuestCheckProgress_{quest.questId}_{i}";
                    string completedKey = $"QuestCheckCompleted_{quest.questId}_{i}";
                    if (PlayerPrefs.HasKey(progressKey)) {
                        PlayerPrefs.DeleteKey(progressKey);
                    }
                    if (PlayerPrefs.HasKey(completedKey)) {
                        PlayerPrefs.DeleteKey(completedKey);
                    }
                }
            }
        }

        if (PlayerPrefs.HasKey("QuestActiveIds")) {
            PlayerPrefs.DeleteKey("QuestActiveIds");
        }

        if (PlayerPrefs.HasKey("QuestCompletedIds")) {
            PlayerPrefs.DeleteKey("QuestCompletedIds");
        }

        if (PlayerPrefs.HasKey("QuestViewedIds")) {
            PlayerPrefs.DeleteKey("QuestViewedIds");
        }

        if (PlayerPrefs.HasKey("QuestFinishedIds")) {
            PlayerPrefs.DeleteKey("QuestFinishedIds");
        }

        PlayerPrefs.Save();
    }

    public bool CompleteQuest(int questId)
    {
        if (!_questDataDict.ContainsKey(questId)) {
            Debug.LogWarning($"Quest with ID {questId} not found.");
            return false;
        }

        QuestState currentState = GetQuestState(questId);

        if (currentState != QuestState.Active && currentState != QuestState.Completable) {
            Debug.LogWarning($"Quest {questId} is {currentState} and cannot be completed. Quest must be Active or Completable.");
            return false;
        }

        _questStates[questId] = QuestState.Completed;
        _completedQuestIds.Add(questId);
        _activeQuestIds.Remove(questId);
        _questsVisitedGameScene.Remove(questId);
        _questsReturnedSuccessfully.Remove(questId);
        _questsReturnedWithFailure.Remove(questId);
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

        QuestState currentState = GetQuestState(questId);

        if (currentState != QuestState.Completed) {
            Debug.LogWarning($"Quest {questId} is {currentState} and cannot be finished. Quest must be Completed.");
            return false;
        }

        _questStates[questId] = QuestState.Finished;
        _completedQuestIds.Add(questId);
        OnQuestStateChanged?.Invoke(questId);
        SaveQuestProgress();

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
