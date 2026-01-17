using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private Button cellButton;
    [SerializeField] private GameObject newQuestIcon;

    private QuestData _questData;
    private QuestDetailPanel _questDetailPanel;

    private void Awake()
    {
        if (cellButton != null)
        {
            cellButton.onClick.AddListener(OnCellClicked);
        }
    }

    public void Initialize(QuestData questData, QuestDetailPanel questDetailPanel, bool isNew)
    {
        _questData = questData;
        _questDetailPanel = questDetailPanel;

        if (questNameText != null && questData != null)
        {
            questNameText.text = questData.questName;
        }
        
        UpdateNewQuestIcon(isNew, questData);
    }
    
    private void UpdateNewQuestIcon(bool isNew, QuestData questData)
    {
        if (newQuestIcon == null || questData == null) return;
        
        if (QuestDataManager.Instance == null)
        {
            newQuestIcon.SetActive(false);
            return;
        }
        
        QuestState state = QuestDataManager.Instance.GetQuestState(questData.questId);
        
        bool shouldShow = false;
        
        if (isNew)
        {
            shouldShow = true;
        }
        else
        {
            if (state == QuestState.Active)
            {
                bool isCompletable = QuestDataManager.Instance.CheckQuestCompletion(questData.questId);
                shouldShow = isCompletable;
            }
        }
        
        newQuestIcon.SetActive(shouldShow);
    }
    
    public void MarkAsViewed()
    {
        if (_questData != null)
        {
            UpdateNewQuestIcon(false, _questData);
        }
    }

    private void OnCellClicked()
    {
        if (_questData != null && _questDetailPanel != null)
        {
            _questDetailPanel.DisplayQuestInfo(_questData, _questData.questId);
            
            MarkAsViewed();
            
            QuestUIHandler handler = FindFirstObjectByType<QuestUIHandler>();
            if (handler != null)
            {
                handler.OnQuestViewed(_questData.questId);
            }
        }
    }

    public QuestData GetQuestData()
    {
        return _questData;
    }
}

