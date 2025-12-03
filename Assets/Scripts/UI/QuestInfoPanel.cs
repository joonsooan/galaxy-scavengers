using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestInfoPanel : MonoBehaviour
{
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text questDescriptionText;
    [SerializeField] private GameObject questResourcePanel;
    [SerializeField] private GameObject resourceInfoCellPrefab;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button finishButton;

    private int _currentQuestId = -1;

    private void Awake()
    {
        if (acceptButton != null)
        {
            acceptButton.onClick.AddListener(OnAcceptButtonClicked);
        }

        if (finishButton != null)
        {
            finishButton.onClick.AddListener(OnFinishButtonClicked);
        }
    }

    private void Update()
    {
        // Close panel on right-click if it's active
        if (Input.GetMouseButtonDown(1))
        {
            if (infoPanel != null && infoPanel.activeSelf)
            {
                // Don't close if dragging a building
                if (GameManager.Instance != null && GameManager.Instance.IsDragging())
                {
                    return;
                }

                // Close the panel on right-click (whether clicking on UI or not)
                ClosePanel();
            }
        }
    }

    private void ClosePanel()
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
        _currentQuestId = -1;
    }

    public void DisplayQuestInfo(QuestData questData, int questId)
    {
        if (questData == null) return;

        if (_currentQuestId == questId && infoPanel != null && infoPanel.activeSelf)
        {
            RebuildAllLayouts();
            infoPanel.SetActive(false);
            _currentQuestId = -1;
            return;
        }

        bool wasInactive = infoPanel != null && !infoPanel.activeSelf;
        _currentQuestId = questId;

        if (infoPanel != null && !infoPanel.activeSelf)
        {
            infoPanel.SetActive(true);
        }

        if (questNameText != null)
        {
            questNameText.text = questData.questName;
        }

        if (questDescriptionText != null)
        {
            questDescriptionText.text = questData.questInfo;
        }

        DisplayRequiredResources(questData);
        UpdateButtonVisibility();
        
        // If panel was just reactivated, rebuild layouts in next frame to ensure Unity has initialized layout components
        if (wasInactive)
        {
            StartCoroutine(RebuildLayoutsNextFrame());
        }
        else
        {
            RebuildAllLayouts();
        }
    }

    private void DisplayRequiredResources(QuestData questData)
    {
        if (questResourcePanel == null || resourceInfoCellPrefab == null) return;

        foreach (Transform child in questResourcePanel.transform)
        {
            Destroy(child.gameObject);
        }

        if (questData.requiredResources != null && questData.requiredResources.Length > 0)
        {
            foreach (ResourceCost cost in questData.requiredResources)
            {
                GameObject cell = Instantiate(resourceInfoCellPrefab, questResourcePanel.transform);
                ResourceInfoCell cellComponent = cell.GetComponent<ResourceInfoCell>();
                if (cellComponent != null)
                {
                    cellComponent.SetInfo(cost.resourceType, cost.amount, false);
                }
            }

            foreach (Transform child in questResourcePanel.transform)
            {
                ResourceInfoCell cell = child.GetComponent<ResourceInfoCell>();
                if (cell != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
                }
            }

            if (questResourcePanel != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(questResourcePanel.GetComponent<RectTransform>());
            }
        }
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
            
            if (acceptButton != null)
            {
                acceptButton.gameObject.SetActive(questState == QuestState.Available);
            }
            
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
        if (_currentQuestId == questId)
        {
            UpdateButtonVisibility();
        }
    }

    private void RebuildAllLayouts()
    {
        if (questNameText != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(questNameText.rectTransform);
        }
        if (questDescriptionText != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(questDescriptionText.rectTransform);
        }

        if (questResourcePanel != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(questResourcePanel.GetComponent<RectTransform>());
        }

        if (infoPanel != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(infoPanel.GetComponent<RectTransform>());
        }

        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator RebuildLayoutsNextFrame()
    {
        yield return null; // Wait one frame for Unity to initialize layout components
        RebuildAllLayouts();
    }
}
