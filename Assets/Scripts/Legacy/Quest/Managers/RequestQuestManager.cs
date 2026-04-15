using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RequestQuestManager : MonoBehaviour
{
    public static RequestQuestManager Instance { get; private set; }
    private const float QuestSpawnCheckInterval = 0.5f;
    
    [Header("Request Quest Settings")]
    [Tooltip("튜토리얼 종료 후 첫 퀘스트가 생성되기까지의 실제 시간(초)")]
    [SerializeField] private float initialQuestDelay = 10f;
    [Tooltip("퀘스트 간 간격의 실제 시간(초)")]
    [SerializeField] private float timeBetweenQuests = 30f;
    
    private List<QuestData> _availableRequestQuests = new List<QuestData>();
    private List<QuestData> _spawnedRequestQuests = new List<QuestData>();
    private readonly HashSet<int> _processedRequestQuestIds = new ();
    private readonly Dictionary<int, float> _prerequisiteSatisfiedAt = new ();
    private Coroutine _questSpawnCoroutine;
    private bool _isInitialized = false;
    private bool _eventsSubscribed;
    
    public static event Action<QuestData> OnRequestQuestSpawned;
    
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
        LoadRequestQuestsFromResources();
    }
    
    private void OnEnable()
    {
        if (_eventsSubscribed)
        {
            return;
        }

        TutorialManager.OnTutorialEnded += OnTutorialEnded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        _eventsSubscribed = true;
    }
    
    private void OnDisable()
    {
        if (!_eventsSubscribed)
        {
            return;
        }

        TutorialManager.OnTutorialEnded -= OnTutorialEnded;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _eventsSubscribed = false;
    }
    
    private void OnDestroy()
    {
        if (_eventsSubscribed)
        {
            TutorialManager.OnTutorialEnded -= OnTutorialEnded;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _eventsSubscribed = false;
        }
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene")
        {
            StartCoroutine(CheckTutorialStatusInGameScene());
        }
    }
    
    private IEnumerator CheckTutorialStatusInGameScene()
    {
        yield return null;
        
        while (TutorialManager.Instance == null)
        {
            yield return null;
        }
        
        bool isTutorialActive = TutorialManager.Instance.IsTutorialActive();
        bool shouldStartTutorial = TutorialManager.Instance.ShouldStartTutorial();
        
        if (!isTutorialActive && !shouldStartTutorial && !_isInitialized)
        {
            _isInitialized = true;
            StartQuestSpawnLoop();
        }
    }
    
    private void LoadRequestQuestsFromResources()
    {
        QuestData[] loadedQuests = Resources.LoadAll<QuestData>("Legacy/QuestRuntime/Request Quest Data");
        
        if (loadedQuests != null && loadedQuests.Length > 0)
        {
            _availableRequestQuests = loadedQuests
                .Where(quest => quest != null && quest.questType == QuestType.RequestQuest)
                .OrderBy(quest => quest.questId)
                .ToList();
        }
    }
    
    private void OnTutorialEnded()
    {
        if (_isInitialized)
        {
            return;
        }
        
        _isInitialized = true;
        StartQuestSpawnLoop();
    }

    private void StartQuestSpawnLoop()
    {
        if (_questSpawnCoroutine != null)
        {
            StopCoroutine(_questSpawnCoroutine);
        }
        _questSpawnCoroutine = StartCoroutine(SpawnRequestQuestCoroutine());
    }

    private IEnumerator SpawnRequestQuestCoroutine()
    {
        while (true)
        {
            if (SceneManager.GetActiveScene().name != "GameScene")
            {
                yield return CoroutineCache.GetWaitForSeconds(QuestSpawnCheckInterval);
                continue;
            }

            if (IsLaunchCountdownActive())
            {
                yield return CoroutineCache.GetWaitForSeconds(QuestSpawnCheckInterval);
                continue;
            }

            bool isTutorialActive = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive();
            if (isTutorialActive)
            {
                yield return CoroutineCache.GetWaitForSeconds(QuestSpawnCheckInterval);
                continue;
            }

            QuestData nextQuest = GetNextQuestToSpawn();
            if (nextQuest == null)
            {
                _questSpawnCoroutine = null;
                yield break;
            }

            if (!ArePrerequisitesSatisfied(nextQuest))
            {
                _prerequisiteSatisfiedAt.Remove(nextQuest.questId);
                yield return CoroutineCache.GetWaitForSeconds(QuestSpawnCheckInterval);
                continue;
            }

            if (!_prerequisiteSatisfiedAt.TryGetValue(nextQuest.questId, out float satisfiedAt))
            {
                _prerequisiteSatisfiedAt[nextQuest.questId] = Time.time;
                yield return CoroutineCache.GetWaitForSeconds(QuestSpawnCheckInterval);
                continue;
            }

            float requiredDelay = GetSpawnDelay(nextQuest);
            if (Time.time - satisfiedAt < requiredDelay)
            {
                yield return CoroutineCache.GetWaitForSeconds(QuestSpawnCheckInterval);
                continue;
            }

            SpawnRequestQuest(nextQuest);
            _prerequisiteSatisfiedAt.Remove(nextQuest.questId);
            yield return CoroutineCache.GetWaitForSeconds(QuestSpawnCheckInterval);
        }
    }
    
    private void SpawnRequestQuest(QuestData questData)
    {
        if (questData == null || QuestDataManager.Instance == null)
        {
            return;
        }

        if (IsLaunchCountdownActive())
        {
            return;
        }
        
        bool isTutorialActive = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive();
        if (isTutorialActive)
        {
            return;
        }
        
        QuestDataManager.Instance.RegisterRuntimeQuest(questData);
        _spawnedRequestQuests.Add(questData);
        _processedRequestQuestIds.Add(questData.questId);
        OnRequestQuestSpawned?.Invoke(questData);
    }
    
    public List<QuestData> GetAvailableRequestQuests()
    {
        return _availableRequestQuests.Where(quest => 
            !_spawnedRequestQuests.Contains(quest) &&
            !_processedRequestQuestIds.Contains(quest.questId)
        ).ToList();
    }
    
    public void RemoveRequestQuest(QuestData questData)
    {
        if (questData != null && _spawnedRequestQuests.Contains(questData))
        {
            _spawnedRequestQuests.Remove(questData);
            
            if (QuestDataManager.Instance != null)
            {
                QuestDataManager.Instance.UnregisterRuntimeQuest(questData.questId);
            }
        }
    }

    private QuestData GetNextQuestToSpawn()
    {
        if (_availableRequestQuests == null || _availableRequestQuests.Count == 0)
        {
            return null;
        }

        QuestDataManager questDataManager = QuestDataManager.Instance;
        foreach (QuestData quest in _availableRequestQuests)
        {
            if (quest == null) continue;
            if (_processedRequestQuestIds.Contains(quest.questId)) continue;
            if (questDataManager != null && questDataManager.IsQuestCompleted(quest.questId))
            {
                _processedRequestQuestIds.Add(quest.questId);
                continue;
            }
            return quest;
        }
        return null;
    }

    private float GetSpawnDelay(QuestData quest)
    {
        return HasRealPrerequisites(quest) ? timeBetweenQuests : initialQuestDelay;
    }

    private static bool HasRealPrerequisites(QuestData quest)
    {
        if (quest == null || quest.previousQuestIds == null || quest.previousQuestIds.Length == 0)
        {
            return false;
        }
        return !quest.previousQuestIds.Contains(-1);
    }

    private static bool ArePrerequisitesSatisfied(QuestData quest)
    {
        if (!HasRealPrerequisites(quest))
        {
            return true;
        }

        if (QuestDataManager.Instance == null)
        {
            return false;
        }

        foreach (int prerequisiteId in quest.previousQuestIds)
        {
            if (prerequisiteId < 0) continue;
            if (!QuestDataManager.Instance.IsQuestCompleted(prerequisiteId))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsLaunchCountdownActive()
    {
        LaunchUIController launchUIController = FindFirstObjectByType<LaunchUIController>(FindObjectsInactive.Include);
        return launchUIController != null && launchUIController.IsCountdownSequenceActive();
    }
}
