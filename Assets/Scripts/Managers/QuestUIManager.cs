using System.Collections.Generic;
using UnityEngine;

public class QuestUIManager : MonoBehaviour
{
    [Header("Quest Info Panel")]
    [SerializeField] private QuestDetailPanel questDetailPanel;
    [SerializeField] private GameObject questCellPrefab;
    [SerializeField] private Transform questCellContentParent;

    public void SetQuestInfoPanel(QuestDetailPanel panel) => questDetailPanel = panel;
    public void SetQuestCellPrefab(GameObject prefab) => questCellPrefab = prefab;
    public void SetQuestCellContentParent(Transform parent) => questCellContentParent = parent;

    private readonly List<QuestCell> _instantiatedQuestCells = new ();

    private void Start()
    {
        StartCoroutine(InitializeAfterQuestDataManager());
    }

    private System.Collections.IEnumerator InitializeAfterQuestDataManager()
    {
        while (QuestDataManager.Instance == null)
        {
            yield return null;
        }

        InstantiateAvailableQuestCells();
        
        QuestDataManager.Instance.OnQuestStateChanged += OnQuestStateChanged;
    }

    private void OnDestroy()
    {
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChanged;
        }
    }

    private void OnQuestStateChanged(int questId)
    {
        if (QuestDataManager.Instance == null) return;
        
        QuestState state = QuestDataManager.Instance.GetQuestState(questId);
        
        if (state == QuestState.Active)
        {
            OnQuestStarted(questId);
        }
        else if (state == QuestState.Completed)
        {
            OnQuestCompleted(questId);
        }
        else if (state == QuestState.Available)
        {
            RefreshQuestCells();
        }
    }

    private void InstantiateAvailableQuestCells()
    {
        if (questCellPrefab == null || questCellContentParent == null || questDetailPanel == null)
        {
            return;
        }

        ClearQuestCells();
        
        if (QuestDataManager.Instance == null) return;
        
        // Get all quests that are not locked (Available, Active, or Completed)
        List<QuestData> currentQuests = new List<QuestData>();
        foreach (QuestData quest in QuestDataManager.Instance.GetAllQuests())
        {
            QuestState state = QuestDataManager.Instance.GetQuestState(quest.questId);
            if (state != QuestState.Locked)
            {
                currentQuests.Add(quest);
            }
        }

        foreach (QuestData quest in currentQuests)
        {
            GameObject cellObject = Instantiate(questCellPrefab, questCellContentParent);
            QuestCell questCell = cellObject.GetComponent<QuestCell>();

            if (questCell != null)
            {
                questCell.Initialize(quest, questDetailPanel, false);
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

    public void RefreshQuestCells()
    {
        InstantiateAvailableQuestCells();
    }

    private void UpdateQuestCellBadge(int questId)
    {
        foreach (QuestCell cell in _instantiatedQuestCells)
        {
            if (cell != null && cell.GetQuestData() != null && cell.GetQuestData().questId == questId)
            {
                // cell.UpdateBadge();
            }
        }
    }

    private void UpdateAllQuestCellBadges()
    {
        foreach (QuestCell cell in _instantiatedQuestCells)
        {
            if (cell != null)
            {
                // cell.UpdateBadge();
            }
        }
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

    public void OnQuestStarted(int questId)
    {
        UpdateQuestCellBadge(questId);
        
        if (questDetailPanel != null)
        {
            questDetailPanel.RefreshQuestState(questId);
        }
    }

    public void OnQuestCompleted(int questId)
    {
        UpdateQuestCellBadge(questId);
        UpdateAllQuestCellBadges();
        
        if (questDetailPanel != null)
        {
            questDetailPanel.RefreshQuestState(questId);
        }
    }

    public void OnQuestFinished(int questId)
    {
        RemoveQuestCell(questId);
        InstantiateAvailableQuestCells();
    }
}

