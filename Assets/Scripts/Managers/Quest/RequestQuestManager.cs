using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RequestQuestManager : MonoBehaviour
{
    public static RequestQuestManager Instance { get; private set; }
    
    [Header("Request Quest Settings")]
    [Tooltip("튜토리얼 종료 후 첫 퀘스트가 생성되기까지의 실제 시간(초)")]
    [SerializeField] private float initialQuestDelay = 10f;
    [Tooltip("퀘스트 간 간격의 실제 시간(초)")]
    [SerializeField] private float timeBetweenQuests = 30f;
    
    private List<QuestData> _availableRequestQuests = new List<QuestData>();
    private List<QuestData> _spawnedRequestQuests = new List<QuestData>();
    private int _currentQuestIndex = 0;
    private Coroutine _questSpawnCoroutine;
    private bool _isInitialized = false;
    
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
        TutorialManager.OnTutorialEnded += OnTutorialEnded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnEnable()
    {
        TutorialManager.OnTutorialEnded += OnTutorialEnded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDisable()
    {
        TutorialManager.OnTutorialEnded -= OnTutorialEnded;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnDestroy()
    {
        TutorialManager.OnTutorialEnded -= OnTutorialEnded;
        SceneManager.sceneLoaded -= OnSceneLoaded;
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
            StartCoroutine(SpawnFirstRequestQuestWithDelay());
        }
    }
    
    private void LoadRequestQuestsFromResources()
    {
        QuestData[] loadedQuests = Resources.LoadAll<QuestData>("Request Quest Data");
        
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
        StartCoroutine(SpawnFirstRequestQuestWithDelay());
    }

    private IEnumerator SpawnFirstRequestQuestWithDelay()
    {
        if (initialQuestDelay > 0f)
        {
            yield return new WaitForSeconds(initialQuestDelay);
        }

        if (IsLaunchCountdownActive())
        {
            yield break;
        }

        SpawnFirstRequestQuest();
    }
    
    private void SpawnFirstRequestQuest()
    {
        if (_availableRequestQuests.Count == 0)
        {
            return;
        }
        
        QuestData firstQuest = _availableRequestQuests[0];
        SpawnRequestQuest(firstQuest);
        _currentQuestIndex = 1;
        
        if (_currentQuestIndex < _availableRequestQuests.Count)
        {
            _questSpawnCoroutine = StartCoroutine(SpawnNextRequestQuestCoroutine());
        }
    }
    
    private IEnumerator SpawnNextRequestQuestCoroutine()
    {
        while (_currentQuestIndex < _availableRequestQuests.Count)
        {
            yield return new WaitForSeconds(timeBetweenQuests);

            if (IsLaunchCountdownActive())
            {
                yield break;
            }
            
            if (SceneManager.GetActiveScene().name == "GameScene")
            {
                bool isTutorialActive = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive();
                
                if (isTutorialActive)
                {
                    continue;
                }
                
                QuestData nextQuest = _availableRequestQuests[_currentQuestIndex];
                SpawnRequestQuest(nextQuest);
                _currentQuestIndex++;
            }
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
        OnRequestQuestSpawned?.Invoke(questData);
    }
    
    public List<QuestData> GetAvailableRequestQuests()
    {
        return _availableRequestQuests.Where(quest => 
            !_spawnedRequestQuests.Contains(quest)
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

    private static bool IsLaunchCountdownActive()
    {
        LaunchUIController launchUIController = FindFirstObjectByType<LaunchUIController>(FindObjectsInactive.Include);
        return launchUIController != null && launchUIController.IsCountdownSequenceActive();
    }
}
