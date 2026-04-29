using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProceduralQuestManager : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private float firstOfferDelaySeconds = 60f;
    [SerializeField] private float nextOfferDelaySeconds = 5f;
    [SerializeField] private float fallbackCheckIntervalSeconds = 0.5f;

    [Header("Generation")]
    [SerializeField] private int switchToLateRuleQuestIndex = 4;
    [SerializeField] private int choicesPerCycle = 3;
    [SerializeField] private float lateBasicWeight = 0.65f;
    [SerializeField] private bool useDeterministicSeed;
    [SerializeField] private int deterministicSeed = 1234;

    [Header("Early Rule")]
    [SerializeField] private List<ResourceType> earlyBasicResourcePool = new();
    [SerializeField] private Vector2Int earlyBasicAmountRange = new(20, 50);

    [Header("Late Rule")]
    [SerializeField] private List<ResourceType> lateBasicResourcePool = new();
    [SerializeField] private Vector2Int lateBasicAmountRange = new(80, 140);
    [SerializeField] private List<ResourceType> lateProcessedResourcePool = new();
    [SerializeField] private Vector2Int lateProcessedAmountRange = new(10, 30);

    [Header("Reward")]
    [SerializeField] private Vector2Int rewardAmountRange = new(5, 15);

    [Header("Notifier")]
    [SerializeField] private GameObject notifierIcon;

    public static ProceduralQuestManager Instance { get; private set; }

    public event Action<List<ProceduralQuestChoiceData>> OnChoicesOffered;
    public event Action<ProceduralQuestRuntimeData> OnActiveQuestChanged;
    public event Action<ProceduralQuestState> OnQuestStateChanged;
    public event Action<ProceduralQuestRuntimeData> OnQuestCompleted;

    public ProceduralQuestState CurrentState => _state;
    public IReadOnlyList<ProceduralQuestChoiceData> CurrentChoices => _currentChoices;
    public ProceduralQuestRuntimeData ActiveQuest => _activeQuest;

    private readonly List<ProceduralQuestChoiceData> _currentChoices = new();
    private ProceduralQuestRuntimeData _activeQuest;
    private ProceduralQuestState _state = ProceduralQuestState.Waiting;
    private ProceduralQuestGenerator _generator;
    private int _nextQuestId = 1;
    private int _completedQuestCount;
    private Coroutine _flowCoroutine;
    private Coroutine _fallbackCheckCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildGenerator();
        SetState(ProceduralQuestState.Waiting);
        SetNotifier(false);
    }

    private void OnEnable()
    {
        ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;
        StartFlow();
        _fallbackCheckCoroutine = StartCoroutine(FallbackCheckLoop());
    }

    private void OnDisable()
    {
        ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;
        if (_flowCoroutine != null)
        {
            StopCoroutine(_flowCoroutine);
            _flowCoroutine = null;
        }

        if (_fallbackCheckCoroutine != null)
        {
            StopCoroutine(_fallbackCheckCoroutine);
            _fallbackCheckCoroutine = null;
        }
    }

    private void BuildGenerator()
    {
        ProceduralQuestGenerator.GenerationSettings settings = new()
        {
            earlyBasicResourcePool = new List<ResourceType>(earlyBasicResourcePool),
            lateBasicResourcePool = new List<ResourceType>(lateBasicResourcePool),
            lateProcessedResourcePool = new List<ResourceType>(lateProcessedResourcePool),
            earlyBasicAmountRange = earlyBasicAmountRange,
            lateBasicAmountRange = lateBasicAmountRange,
            lateProcessedAmountRange = lateProcessedAmountRange,
            switchToLateRuleQuestIndex = Mathf.Max(1, switchToLateRuleQuestIndex),
            lateBasicWeight = lateBasicWeight,
            choicesPerCycle = Mathf.Max(1, choicesPerCycle),
            rewardAmountMin = Mathf.Min(rewardAmountRange.x, rewardAmountRange.y),
            rewardAmountMax = Mathf.Max(rewardAmountRange.x, rewardAmountRange.y),
            useDeterministicSeed = useDeterministicSeed,
            deterministicSeed = deterministicSeed
        };
        _generator = new ProceduralQuestGenerator(settings);
    }

    public void StartFlow()
    {
        if (_flowCoroutine != null)
        {
            StopCoroutine(_flowCoroutine);
        }

        _flowCoroutine = StartCoroutine(QuestFlowCoroutine());
    }

    public void OfferChoicesNow()
    {
        if (_activeQuest != null)
        {
            return;
        }

        GenerateAndPublishChoices();
    }

    public bool AcceptChoice(int questId)
    {
        if (_state != ProceduralQuestState.ChoiceOffered)
        {
            return false;
        }

        ProceduralQuestChoiceData selected = null;
        for (int i = 0; i < _currentChoices.Count; i++)
        {
            if (_currentChoices[i].questId == questId)
            {
                selected = _currentChoices[i];
                break;
            }
        }

        if (selected == null)
        {
            return false;
        }

        _activeQuest = new ProceduralQuestRuntimeData
        {
            questId = selected.questId,
            targetResourceType = selected.targetResourceType,
            requiredAmount = selected.requiredAmount,
            rewardSpecs = new List<ProceduralQuestRewardSpec>(selected.rewardSpecs),
            createdAtQuestIndex = selected.createdAtQuestIndex,
            state = ProceduralQuestState.InProgress
        };

        _currentChoices.Clear();
        SetState(ProceduralQuestState.InProgress);
        OnChoicesOffered?.Invoke(_currentChoices);
        OnActiveQuestChanged?.Invoke(_activeQuest);
        EvaluateActiveQuest();
        return true;
    }

    public bool CompleteActiveQuest()
    {
        if (_activeQuest == null || _state != ProceduralQuestState.Completable)
        {
            return false;
        }

        bool spent = ResourceManager.Instance != null &&
                     ResourceManager.Instance.RemoveResource(_activeQuest.targetResourceType, _activeQuest.requiredAmount);
        if (!spent)
        {
            EvaluateActiveQuest();
            return false;
        }

        if (_activeQuest.rewardSpecs != null)
        {
            for (int i = 0; i < _activeQuest.rewardSpecs.Count; i++)
            {
                ProceduralQuestRewardSpec reward = _activeQuest.rewardSpecs[i];
                if (reward == null || reward.resourceType == ResourceType.None || reward.amount <= 0)
                {
                    continue;
                }

                ResourceManager.Instance?.AddResource(reward.resourceType, reward.amount);
            }
        }

        _activeQuest.state = ProceduralQuestState.Completed;
        SetState(ProceduralQuestState.Completed);
        OnQuestCompleted?.Invoke(_activeQuest);
        OnActiveQuestChanged?.Invoke(_activeQuest);
        _completedQuestCount++;
        _activeQuest = null;
        StartFlow();
        return true;
    }

    private IEnumerator QuestFlowCoroutine()
    {
        float delay = _completedQuestCount == 0 ? firstOfferDelaySeconds : nextOfferDelaySeconds;
        yield return new WaitForSeconds(Mathf.Max(0f, delay));
        if (_activeQuest == null)
        {
            GenerateAndPublishChoices();
        }
    }

    private IEnumerator FallbackCheckLoop()
    {
        while (enabled)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, fallbackCheckIntervalSeconds));
            EvaluateActiveQuest();
        }
    }

    private void GenerateAndPublishChoices()
    {
        if (_generator == null)
        {
            BuildGenerator();
        }

        _currentChoices.Clear();
        int questIndexForCycle = _completedQuestCount + 1;
        List<ProceduralQuestChoiceData> generated = _generator.GenerateChoices(questIndexForCycle, ref _nextQuestId);
        _currentChoices.AddRange(generated);

        SetState(ProceduralQuestState.ChoiceOffered);
        OnChoicesOffered?.Invoke(_currentChoices);
        OnActiveQuestChanged?.Invoke(null);
    }

    private void EvaluateActiveQuest()
    {
        if (_activeQuest == null || ResourceManager.Instance == null)
        {
            return;
        }

        int currentAmount = ResourceManager.Instance.GetResourceAmount(_activeQuest.targetResourceType);
        bool isCompletable = currentAmount >= _activeQuest.requiredAmount;
        ProceduralQuestState nextState = isCompletable ? ProceduralQuestState.Completable : ProceduralQuestState.InProgress;
        if (_state == nextState)
        {
            return;
        }

        _activeQuest.state = nextState;
        SetState(nextState);
        OnActiveQuestChanged?.Invoke(_activeQuest);
    }

    private void OnResourceAmountChanged(ResourceType _, int __)
    {
        EvaluateActiveQuest();
    }

    private void SetState(ProceduralQuestState nextState)
    {
        _state = nextState;
        OnQuestStateChanged?.Invoke(_state);
        SetNotifier(_state == ProceduralQuestState.ChoiceOffered || _state == ProceduralQuestState.Completable);
    }

    private void SetNotifier(bool isOn)
    {
        if (notifierIcon != null)
        {
            notifierIcon.SetActive(isOn);
        }
    }
}
