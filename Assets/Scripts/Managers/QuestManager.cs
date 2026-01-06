using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Info Panel")]
    [SerializeField] private QuestInfoPanel questInfoPanel;
    [SerializeField] private GameObject questCellPrefab;
    [SerializeField] private Transform questCellContentParent;

    private QuestUIManager _questUIManager;

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
        // Find or create QuestUIManager
        _questUIManager = FindFirstObjectByType<QuestUIManager>();
        
        if (_questUIManager == null)
        {
            // Create QuestUIManager if it doesn't exist
            GameObject uiManagerObj = new GameObject("QuestUIManager");
            _questUIManager = uiManagerObj.AddComponent<QuestUIManager>();
        }
        
        // Set references
        if (_questUIManager != null)
        {
            if (questInfoPanel != null) _questUIManager.SetQuestInfoPanel(questInfoPanel);
            if (questCellPrefab != null) _questUIManager.SetQuestCellPrefab(questCellPrefab);
            if (questCellContentParent != null) _questUIManager.SetQuestCellContentParent(questCellContentParent);
        }
    }

    // Delegate all operations to QuestDataManager and QuestUIManager
    public bool StartQuest(int questId)
    {
        bool result = QuestDataManager.Instance != null && QuestDataManager.Instance.StartQuest(questId);
        if (result && _questUIManager != null)
        {
            _questUIManager.OnQuestStarted(questId);
        }
        return result;
    }

    public bool CompleteQuest(int questId)
    {
        bool result = QuestDataManager.Instance != null && QuestDataManager.Instance.CompleteQuest(questId);
        if (result && _questUIManager != null)
        {
            _questUIManager.OnQuestCompleted(questId);
        }
        return result;
    }

    public bool FinishQuest(int questId)
    {
        bool result = QuestDataManager.Instance != null && QuestDataManager.Instance.FinishQuest(questId);
        if (result && _questUIManager != null)
        {
            _questUIManager.OnQuestFinished(questId);
        }
        return result;
    }

    public QuestState GetQuestState(int questId)
    {
        return QuestDataManager.Instance != null ? QuestDataManager.Instance.GetQuestState(questId) : QuestState.Locked;
    }

    public QuestData GetQuestData(int questId)
    {
        return QuestDataManager.Instance?.GetQuestData(questId);
    }

    public HashSet<int> GetActiveQuestIds()
    {
        return QuestDataManager.Instance != null ? QuestDataManager.Instance.GetActiveQuestIds() : new HashSet<int>();
    }

    public bool IsQuestActive(int questId)
    {
        return QuestDataManager.Instance != null && QuestDataManager.Instance.IsQuestActive(questId);
    }

    public List<QuestData> GetAvailableQuests()
    {
        return QuestDataManager.Instance != null ? QuestDataManager.Instance.GetAvailableQuests() : new List<QuestData>();
    }

    public HashSet<int> GetCompletedQuestIds()
    {
        return QuestDataManager.Instance != null ? QuestDataManager.Instance.GetCompletedQuestIds() : new HashSet<int>();
    }

    public bool IsQuestCompleted(int questId)
    {
        return QuestDataManager.Instance != null && QuestDataManager.Instance.IsQuestCompleted(questId);
    }
}
