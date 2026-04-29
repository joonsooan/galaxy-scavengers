using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProceduralQuestPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProceduralQuestManager questManager;
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private GameObject choicesSection;
    [SerializeField] private Transform choiceContainer;
    [SerializeField] private QuestChoiceCellController questChoiceCellPrefab;
    [SerializeField] private GameObject progressSection;

    [Header("Active Quest UI")]
    [SerializeField] private TMP_Text activeResourceNameText;
    [SerializeField] private Image activeResourceIcon;
    [SerializeField] private TMP_Text activeRequiredAmountText;
    [SerializeField] private TMP_Text activeCurrentAmountText;
    [SerializeField] private TMP_Text activeStateText;
    [SerializeField] private Button completeButton;
    [SerializeField] private Button closeButton;

    private readonly List<QuestChoiceCellController> _spawnedChoices = new();

    private void Awake()
    {
        if (questManager == null)
        {
            questManager = ProceduralQuestManager.Instance;
            if (questManager == null)
            {
                questManager = FindFirstObjectByType<ProceduralQuestManager>();
            }
        }
    }

    private void OnEnable()
    {
        if (completeButton != null)
        {
            completeButton.onClick.RemoveListener(OnCompleteButtonClicked);
            completeButton.onClick.AddListener(OnCompleteButtonClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (questManager != null)
        {
            questManager.OnChoicesOffered += OnChoicesOffered;
            questManager.OnActiveQuestChanged += OnActiveQuestChanged;
            questManager.OnQuestStateChanged += OnQuestStateChanged;
            RefreshView();
        }
    }

    private void OnDisable()
    {
        if (questManager != null)
        {
            questManager.OnChoicesOffered -= OnChoicesOffered;
            questManager.OnActiveQuestChanged -= OnActiveQuestChanged;
            questManager.OnQuestStateChanged -= OnQuestStateChanged;
        }
    }

    public void TogglePanel()
    {
        if (rootPanel == null)
        {
            return;
        }

        bool next = !rootPanel.activeSelf;
        rootPanel.SetActive(next);
        if (next)
        {
            RefreshView();
        }
    }

    public void OpenPanel()
    {
        if (rootPanel == null)
        {
            return;
        }

        rootPanel.SetActive(true);
        RefreshView();
    }

    private void ClosePanel()
    {
        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
    }

    private void OnChoicesOffered(List<ProceduralQuestChoiceData> _)
    {
        RefreshView();
    }

    private void OnActiveQuestChanged(ProceduralQuestRuntimeData _)
    {
        RefreshView();
    }

    private void OnQuestStateChanged(ProceduralQuestState _)
    {
        RefreshView();
    }

    private void RefreshView()
    {
        if (questManager == null)
        {
            return;
        }

        ProceduralQuestState state = questManager.CurrentState;
        bool showChoices = state == ProceduralQuestState.ChoiceOffered;
        bool showProgress = state == ProceduralQuestState.InProgress || state == ProceduralQuestState.Completable;

        if (choicesSection != null)
        {
            choicesSection.SetActive(showChoices);
        }

        if (progressSection != null)
        {
            progressSection.SetActive(showProgress);
        }

        if (showChoices)
        {
            RebuildChoiceCells(questManager.CurrentChoices);
        }
        else
        {
            ClearChoiceCells();
        }

        if (showProgress)
        {
            RefreshActiveQuest(questManager.ActiveQuest, state);
        }
    }

    private void RebuildChoiceCells(IReadOnlyList<ProceduralQuestChoiceData> choices)
    {
        ClearChoiceCells();
        if (choiceContainer == null || questChoiceCellPrefab == null || choices == null)
        {
            return;
        }

        for (int i = 0; i < choices.Count; i++)
        {
            QuestChoiceCellController cell = Instantiate(questChoiceCellPrefab, choiceContainer);
            cell.Bind(choices[i], OnChoiceAccepted);
            _spawnedChoices.Add(cell);
        }
    }

    private void ClearChoiceCells()
    {
        for (int i = 0; i < _spawnedChoices.Count; i++)
        {
            if (_spawnedChoices[i] != null)
            {
                Destroy(_spawnedChoices[i].gameObject);
            }
        }

        _spawnedChoices.Clear();
    }

    private void RefreshActiveQuest(ProceduralQuestRuntimeData activeQuest, ProceduralQuestState state)
    {
        if (activeQuest == null)
        {
            return;
        }

        int currentAmount = ResourceManager.Instance != null
            ? ResourceManager.Instance.GetResourceAmount(activeQuest.targetResourceType)
            : 0;

        if (ResourceManager.Instance != null)
        {
            if (activeResourceNameText != null)
            {
                activeResourceNameText.text = ResourceManager.Instance.GetResourceDisplayName(activeQuest.targetResourceType);
            }

            if (activeResourceIcon != null)
            {
                activeResourceIcon.sprite = ResourceManager.Instance.GetResourceIcon(activeQuest.targetResourceType);
            }
        }
        else if (activeResourceNameText != null)
        {
            activeResourceNameText.text = activeQuest.targetResourceType.ToString();
        }

        if (activeRequiredAmountText != null)
        {
            activeRequiredAmountText.text = activeQuest.requiredAmount.ToString();
        }

        if (activeCurrentAmountText != null)
        {
            activeCurrentAmountText.text = currentAmount.ToString();
        }

        if (activeStateText != null)
        {
            activeStateText.text = state == ProceduralQuestState.Completable ? "완료 가능" : "진행 중";
        }

        if (completeButton != null)
        {
            completeButton.interactable = state == ProceduralQuestState.Completable;
        }
    }

    private void OnChoiceAccepted(int questId)
    {
        if (questManager == null)
        {
            return;
        }

        questManager.AcceptChoice(questId);
    }

    private void OnCompleteButtonClicked()
    {
        if (questManager == null)
        {
            return;
        }

        questManager.CompleteActiveQuest();
    }
}
