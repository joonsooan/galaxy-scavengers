using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

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

    // Delegate all operations to QuestDataManager
    // QuestUIManager listens to QuestDataManager events directly
    public bool StartQuest(int questId)
    {
        return QuestDataManager.Instance != null && QuestDataManager.Instance.StartQuest(questId);
    }

    public bool CompleteQuest(int questId)
    {
        return QuestDataManager.Instance != null && QuestDataManager.Instance.CompleteQuest(questId);
    }

    public bool FinishQuest(int questId)
    {
        return QuestDataManager.Instance != null && QuestDataManager.Instance.FinishQuest(questId);
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
    
    public bool CheckQuestCompletion(int questId)
    {
        return QuestDataManager.Instance != null && QuestDataManager.Instance.CheckQuestCompletion(questId);
    }
}
