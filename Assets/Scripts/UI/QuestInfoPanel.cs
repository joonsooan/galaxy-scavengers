using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestInfoPanel : MonoBehaviour
{
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text questDescriptionText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button finishButton;

    private int _currentQuestId = -1;

    private void Awake()
    {
        ChangeInfo();
        
        if (acceptButton != null)
        {
            acceptButton.onClick.AddListener(OnAcceptButtonClicked);
        }

        if (finishButton != null)
        {
            finishButton.onClick.AddListener(OnFinishButtonClicked);
        }
    }

    public void DisplayQuestInfo(QuestData questData, int questId)
    {
        if (questData == null) return;

        _currentQuestId = questId;

        if (questNameText != null)
        {
            questNameText.text = questData.questName;
        }

        if (questDescriptionText != null)
        {
            questDescriptionText.text = questData.questInfo;
        }

        if (infoPanel != null)
        {
            infoPanel.SetActive(true);
        }

        // Update button visibility based on quest state
        UpdateButtonVisibility();

        ChangeInfo();
    }

    private void UpdateButtonVisibility()
    {
        if (_currentQuestId == -1)
        {
            if (acceptButton != null) acceptButton.gameObject.SetActive(false);
            if (finishButton != null) finishButton.gameObject.SetActive(false);
            return;
        }

        if (QuestManager.Instance != null)
        {
            QuestState questState = QuestManager.Instance.GetQuestState(_currentQuestId);
            
            // Show accept button only for Available quests
            if (acceptButton != null)
            {
                acceptButton.gameObject.SetActive(questState == QuestState.Available);
            }
            
            // Show finish button only for Completed quests
            if (finishButton != null)
            {
                finishButton.gameObject.SetActive(questState == QuestState.Completed);
            }
        }
        else
        {
            if (acceptButton != null) acceptButton.gameObject.SetActive(false);
            if (finishButton != null) finishButton.gameObject.SetActive(false);
        }
    }

    private void OnAcceptButtonClicked()
    {
        if (_currentQuestId == -1) return;

        if (QuestManager.Instance != null)
        {
            bool success = QuestManager.Instance.StartQuest(_currentQuestId);
            
            if (success)
            {
                // Update button visibility after quest is activated
                UpdateButtonVisibility();
            }
        }
    }

    private void OnFinishButtonClicked()
    {
        if (_currentQuestId == -1) return;

        if (QuestManager.Instance != null)
        {
            bool success = QuestManager.Instance.FinishQuest(_currentQuestId);
            
            if (success)
            {
                // Hide the info panel after finishing
                if (infoPanel != null)
                {
                    infoPanel.SetActive(false);
                }
                _currentQuestId = -1;
            }
        }
    }

    public void RefreshQuestState(int questId)
    {
        // If the panel is currently showing this quest, update the button visibility
        if (_currentQuestId == questId)
        {
            UpdateButtonVisibility();
        }
    }

    private void ChangeInfo()
    {
        if (infoPanel != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(infoPanel.GetComponent<RectTransform>());
        }
        if (questNameText != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(questNameText.rectTransform);
        }
        if (questDescriptionText != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(questDescriptionText.rectTransform);
        }
    }
}
