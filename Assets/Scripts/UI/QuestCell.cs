using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private Button cellButton;
    [SerializeField] private Image availableBadge;
    [SerializeField] private Image finishedBadge;

    private QuestData _questData;
    private QuestInfoPanel _questInfoPanel;

    private void Awake()
    {
        if (cellButton != null)
        {
            cellButton.onClick.AddListener(OnCellClicked);
        }
    }

    public void Initialize(QuestData questData, QuestInfoPanel questInfoPanel)
    {
        _questData = questData;
        _questInfoPanel = questInfoPanel;

        if (questNameText != null && questData != null)
        {
            questNameText.text = questData.questName;
        }

        UpdateBadge();
    }

    public void UpdateBadge()
    {
        if (_questData == null || QuestManager.Instance == null) return;

        QuestState questState = QuestManager.Instance.GetQuestState(_questData.questId);

        // Show available badge for Available quests
        if (availableBadge != null)
        {
            availableBadge.gameObject.SetActive(questState == QuestState.Available);
        }

        // Show finished badge for Completed quests
        if (finishedBadge != null)
        {
            finishedBadge.gameObject.SetActive(questState == QuestState.Completed);
        }
    }

    private void OnCellClicked()
    {
        if (_questData != null && _questInfoPanel != null)
        {
            _questInfoPanel.DisplayQuestInfo(_questData, _questData.questId);
        }
    }

    public QuestData GetQuestData()
    {
        return _questData;
    }
}

