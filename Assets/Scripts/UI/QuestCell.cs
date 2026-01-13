using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private Button cellButton;

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

