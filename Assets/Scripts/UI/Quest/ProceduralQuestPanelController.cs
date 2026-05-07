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
    [SerializeField] private TMP_Text progressSectionStatusText;
    [SerializeField] private string progressInProgressLabel = "진행 중인 퀘스트";
    [SerializeField] private string progressCompletableLabel = "퀘스트 완료 가능";
    [SerializeField] private TMP_Text progressActionButtonText;
    [SerializeField] private string progressActionInProgressLabel = "진행 중";
    [SerializeField] private string progressActionCompletableLabel = "완료";

    [Header("Quest entry")]
    [SerializeField] private Button questButton;
    [SerializeField] private GameObject notifierIcon;

    [Header("Active Quest UI")]
    [SerializeField] private Image activeResourceIcon;
    [SerializeField] private TMP_Text activeAmountPairText;
    [SerializeField] private TMP_Text activeTokenAmountText;
    [SerializeField] private TMP_Text activeStateText;
    [SerializeField] private Button completeButton;

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
        ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;

        if (questButton != null)
        {
            questButton.onClick.RemoveListener(OnQuestButtonClicked);
            questButton.onClick.AddListener(OnQuestButtonClicked);
        }

        if (completeButton != null)
        {
            completeButton.onClick.RemoveListener(OnCompleteButtonClicked);
            completeButton.onClick.AddListener(OnCompleteButtonClicked);
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
        ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;

        if (questButton != null)
        {
            questButton.onClick.RemoveListener(OnQuestButtonClicked);
        }

        if (completeButton != null)
        {
            completeButton.onClick.RemoveListener(OnCompleteButtonClicked);
        }

        if (questManager != null)
        {
            questManager.OnChoicesOffered -= OnChoicesOffered;
            questManager.OnActiveQuestChanged -= OnActiveQuestChanged;
            questManager.OnQuestStateChanged -= OnQuestStateChanged;
        }
    }

    private void Update()
    {
        if (rootPanel == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (rootPanel.activeSelf || CanOpenPanelByHotkey())
            {
                TogglePanel();
            }
            return;
        }

        if (rootPanel.activeSelf && Input.GetMouseButtonDown(1))
        {
            ClosePanel();
        }
    }

    private void OnResourceAmountChanged(ResourceType _, int __)
    {
        if (questManager == null)
        {
            return;
        }

        ProceduralQuestState state = questManager.CurrentState;
        bool showProgress = state == ProceduralQuestState.InProgress || state == ProceduralQuestState.Completable;
        if (!showProgress)
        {
            return;
        }

        RefreshActiveQuest(questManager.ActiveQuest, state);
    }

    private bool CanOpenPanelByHotkey()
    {
        if (questManager == null)
        {
            return false;
        }

        bool hasActiveQuest = questManager.ActiveQuest != null;
        bool hasAcceptableChoices = questManager.CurrentState == ProceduralQuestState.ChoiceOffered;
        return hasActiveQuest || hasAcceptableChoices;
    }

    private void OnQuestButtonClicked()
    {
        TogglePanel();
    }

    public void TogglePanel()
    {
        if (rootPanel == null)
        {
            return;
        }

        if (rootPanel.activeSelf)
        {
            ClosePanel();
        }
        else
        {
            rootPanel.SetActive(true);
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
        if (rootPanel == null || !rootPanel.activeSelf)
        {
            return;
        }

        rootPanel.SetActive(false);
        RefreshNotifier();
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

        RefreshNotifier();
    }

    private void RefreshNotifier()
    {
        if (notifierIcon == null || questManager == null)
        {
            return;
        }

        ProceduralQuestState state = questManager.CurrentState;
        bool needsAttention = state == ProceduralQuestState.ChoiceOffered || state == ProceduralQuestState.Completable;
        bool panelOpen = rootPanel != null && rootPanel.activeSelf;
        notifierIcon.SetActive(needsAttention && !panelOpen);
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
            if (activeTokenAmountText != null)
            {
                activeTokenAmountText.text = string.Empty;
            }

            return;
        }

        int currentAmount = ResourceManager.Instance != null
            ? ResourceManager.Instance.GetResourceAmount(activeQuest.targetResourceType)
            : 0;

        if (ResourceManager.Instance != null)
        {
            if (activeResourceIcon != null)
            {
                activeResourceIcon.sprite = ResourceManager.Instance.GetResourceIcon(activeQuest.targetResourceType);
            }
        }

        if (activeAmountPairText != null)
        {
            activeAmountPairText.text = $"{currentAmount} / {activeQuest.requiredAmount}";
        }

        if (activeTokenAmountText != null)
        {
            int tokenReward = SumTokenRewardAmount(activeQuest.rewardSpecs);
            activeTokenAmountText.text = tokenReward > 0 ? $"+{tokenReward}" : string.Empty;
        }

        string progressLine = state == ProceduralQuestState.Completable
            ? progressCompletableLabel
            : progressInProgressLabel;
        if (progressSectionStatusText != null)
        {
            progressSectionStatusText.text = progressLine;
        }

        if (activeStateText != null)
        {
            activeStateText.text = progressLine;
        }

        if (completeButton != null)
        {
            completeButton.interactable = state == ProceduralQuestState.Completable;
        }

        if (progressActionButtonText != null)
        {
            progressActionButtonText.text = state == ProceduralQuestState.Completable
                ? progressActionCompletableLabel
                : progressActionInProgressLabel;
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

    public void OnCompleteButtonClicked()
    {
        if (questManager == null)
        {
            return;
        }

        questManager.CompleteActiveQuest();
        RefreshView();
    }

    private static int SumTokenRewardAmount(List<ProceduralQuestRewardSpec> rewardSpecs)
    {
        if (rewardSpecs == null)
        {
            return 0;
        }

        int sum = 0;
        for (int i = 0; i < rewardSpecs.Count; i++)
        {
            ProceduralQuestRewardSpec spec = rewardSpecs[i];
            if (spec != null && spec.kind == ProceduralQuestRewardKind.Token && spec.amount > 0)
            {
                sum += spec.amount;
            }
        }

        return sum;
    }
}
