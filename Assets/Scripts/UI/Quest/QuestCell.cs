using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text questIdText;
    [SerializeField] private Button cellButton;
    [SerializeField] private GameObject configureIcon;
    [SerializeField] private GameObject notifierIcon;
    [Header("Title Color")]
    [SerializeField] private Color coreRepairQuestTitleColor = Color.cyan;

    private QuestData _questData;
    private QuestDetailPanel _questDetailPanel;
    private QuestBriefPanel _questBriefPanel;
    private QuestUIHandler _questUIHandler;
    private GameSceneQuestUIManager _gameSceneQuestUIManager;
    private bool _isNew;
    private Color _defaultQuestTitleColor = Color.white;

    private void Awake()
    {
        if (questNameText != null)
        {
            _defaultQuestTitleColor = questNameText.color;
        }

        if (cellButton != null)
        {
            cellButton.onClick.AddListener(OnCellClicked);
        }
    }
    
    private void OnEnable()
    {
        SubscribeToResourceChanges();
        RefreshIcon();
    }
    
    private void OnDisable()
    {
        UnsubscribeFromResourceChanges();
    }
    
    private void SubscribeToResourceChanges()
    {
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager != null)
        {
            inventoryManager.OnResourceChanged += OnBaseInventoryResourceChanged;
        }
    }
    
    private void UnsubscribeFromResourceChanges()
    {
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager != null)
        {
            inventoryManager.OnResourceChanged -= OnBaseInventoryResourceChanged;
        }
    }
    
    private void OnBaseInventoryResourceChanged(ResourceType resourceType, int amount)
    {
        if (_questData != null)
        {
            CheckAndUpdateCompletability();
            
            if (_questUIHandler != null)
            {
                _questUIHandler.OnResourceChangedForIndicator();
            }
        }
    }
    
    public void CheckAndUpdateCompletability()
    {
        if (_questData == null || QuestDataManager.Instance == null) return;
        
        bool isNew = _isNew;
        if (_gameSceneQuestUIManager != null && _questData.questType == QuestType.CoreRepairQuest)
        {
            isNew = !_gameSceneQuestUIManager.IsQuestViewed(_questData.questId);
        }
        
        UpdateConfigureIcon(isNew, _questData);
    }

    public void Initialize(QuestData questData, QuestBriefPanel questBriefPanel, bool isNew, QuestUIHandler questUIHandler = null, GameSceneQuestUIManager gameSceneQuestUIManager = null)
    {
        _questData = questData;
        _questDetailPanel = null;
        _questBriefPanel = questBriefPanel;
        _questUIHandler = questUIHandler;
        _gameSceneQuestUIManager = gameSceneQuestUIManager;
        _isNew = isNew;


        if (questNameText != null && questData != null)
        {
            questNameText.text = questData.questName;
        }
        ApplyQuestTitleColor(questData);

        if (questIdText != null && questData != null)
        {
            questIdText.text = $"#{questData.questId:D4}";
        }

        UpdateConfigureIcon(isNew, questData);

        SubscribeToResourceChanges();
    }

    public void Initialize(QuestData questData, QuestDetailPanel questDetailPanel, bool isNew, QuestUIHandler questUIHandler = null, GameSceneQuestUIManager gameSceneQuestUIManager = null)
    {
        _questData = questData;
        _questDetailPanel = questDetailPanel;
        _questBriefPanel = null;
        _questUIHandler = questUIHandler;
        _gameSceneQuestUIManager = gameSceneQuestUIManager;
        _isNew = isNew;


        if (questNameText != null && questData != null)
        {
            questNameText.text = questData.questName;
        }
        ApplyQuestTitleColor(questData);
        
        if (questIdText != null && questData != null)
        {
            questIdText.text = $"#{questData.questId:D4}";
        }
        
        UpdateConfigureIcon(isNew, questData);
        
        SubscribeToResourceChanges();
    }
    
    private void UpdateConfigureIcon(bool isNew, QuestData questData)
    {
        if (configureIcon == null || questData == null) return;
        
        if (QuestDataManager.Instance == null)
        {
            configureIcon.SetActive(false);
            return;
        }
        
        QuestState questState = QuestDataManager.Instance.GetQuestState(questData.questId);
        bool shouldShow;
        
        bool isBaseQuestReadyToComplete = questData.questType == QuestType.BaseQuest && IsQuestReadyToComplete(questData.questId, questState);

        if (questState == QuestState.Available || questState == QuestState.Completable || isBaseQuestReadyToComplete)
        {
            shouldShow = true;
        }
        else
        {
            shouldShow = false;
        }
        
        configureIcon.SetActive(shouldShow);
        
        if (notifierIcon != null)
        {
            bool shouldShowNotifier = false;
            if (questData.questType == QuestType.BaseQuest)
            {
                QuestState state = QuestDataManager.Instance.GetQuestState(questData.questId);
                shouldShowNotifier = IsQuestReadyToComplete(questData.questId, state);
            }
            else if (questData.questType == QuestType.CoreRepairQuest)
            {
                QuestState state = QuestDataManager.Instance.GetQuestState(questData.questId);
                if (_gameSceneQuestUIManager != null)
                {
                    bool isViewed = _gameSceneQuestUIManager.IsQuestViewed(questData.questId);
                    shouldShowNotifier = state == QuestState.Completable || (state == QuestState.Active && !isViewed);
                }
                else
                {
                    shouldShowNotifier = state == QuestState.Completable || (state == QuestState.Active && isNew);
                }
            }
            else if (questData.questType == QuestType.RequestQuest)
            {
                if (_gameSceneQuestUIManager != null)
                {
                    bool isAccepted = _gameSceneQuestUIManager.IsRequestQuestAccepted(questData.questId);
                    if (isAccepted)
                    {
                        QuestState state = QuestDataManager.Instance.GetQuestState(questData.questId);
                        shouldShowNotifier = state == QuestState.Completable;
                    }
                    else
                    {
                        shouldShowNotifier = !_gameSceneQuestUIManager.IsQuestViewed(questData.questId);
                    }
                }
                else
                {
                    shouldShowNotifier = isNew;
                }
            }
            notifierIcon.SetActive(shouldShowNotifier);
        }
    }

    private bool IsQuestReadyToComplete(int questId, QuestState state)
    {
        if (state == QuestState.Completable)
        {
            return true;
        }

        if (state != QuestState.Active || QuestDataManager.Instance == null)
        {
            return false;
        }

        return QuestDataManager.Instance.CheckQuestCompletion(questId);
    }

    private void MarkAsViewed()
    {
        if (_questData != null)
        {
            _isNew = false;
            UpdateConfigureIcon(false, _questData);
        }
    }

    private void OnCellClicked()
    {
        if (_questData == null)
        {
            return;
        }
        
        if (_questData.questType == QuestType.RequestQuest)
        {
            if (_gameSceneQuestUIManager == null)
            {
                _gameSceneQuestUIManager = FindFirstObjectByType<GameSceneQuestUIManager>();
            }
            
            if (QuestDataManager.Instance != null)
            {
                QuestState state = QuestDataManager.Instance.GetQuestState(_questData.questId);
                bool isAccepted = _gameSceneQuestUIManager != null && _gameSceneQuestUIManager.IsRequestQuestAccepted(_questData.questId);
                
                if (_gameSceneQuestUIManager != null && state == QuestState.Available && !isAccepted)
                {
                    _gameSceneQuestUIManager.ShowRequestQuestAcceptPanel(_questData);
                    return;
                }
            }
        }

        if (_questBriefPanel != null)
        {
            if (_gameSceneQuestUIManager == null)
            {
                _gameSceneQuestUIManager = FindFirstObjectByType<GameSceneQuestUIManager>();
            }

            if (_gameSceneQuestUIManager != null)
            {
                _gameSceneQuestUIManager.ShowQuestDetailPanel();
            }

            if (_questBriefPanel.gameObject != null)
            {
                _questBriefPanel.gameObject.SetActive(true);
            }

            _questBriefPanel.DisplayQuestInfo(_questData, _questData.questId);

            if (_gameSceneQuestUIManager != null)
            {
                _gameSceneQuestUIManager.MarkQuestAsViewed(_questData.questId);
            }

            MarkAsViewed();

            if (_questUIHandler != null)
            {
                _questUIHandler.OnQuestViewed(_questData.questId);
            }
        }
        else if (_questDetailPanel != null)
        {
            if (_gameSceneQuestUIManager == null)
            {
                _gameSceneQuestUIManager = FindFirstObjectByType<GameSceneQuestUIManager>();
            }

            if (_gameSceneQuestUIManager != null)
            {
                _gameSceneQuestUIManager.ShowQuestDetailPanel();
            }

            if (_questDetailPanel.gameObject != null)
            {
                _questDetailPanel.gameObject.SetActive(true);
            }

            _questDetailPanel.DisplayQuestInfo(_questData, _questData.questId);

            if (_gameSceneQuestUIManager != null)
            {
                _gameSceneQuestUIManager.MarkQuestAsViewed(_questData.questId);
            }

            MarkAsViewed();

            if (_questUIHandler != null)
            {
                _questUIHandler.OnQuestViewed(_questData.questId);
            }
        }
        
        if (cellButton != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(SelectButtonAfterFrame());
            }
            else if (_gameSceneQuestUIManager != null)
            {
                _gameSceneQuestUIManager.SelectButtonAfterFrame(cellButton.gameObject);
            }
        }
    }
    
    private System.Collections.IEnumerator SelectButtonAfterFrame()
    {
        yield return null;
        if (cellButton != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(cellButton.gameObject);
        }
    }

    public QuestData GetQuestData()
    {
        return _questData;
    }

    private void RefreshIcon()
    {
        CheckAndUpdateCompletability();
    }

    private void ApplyQuestTitleColor(QuestData questData)
    {
        if (questNameText == null)
        {
            return;
        }

        if (questData != null && questData.questType == QuestType.CoreRepairQuest)
        {
            questNameText.color = GetCoreRepairQuestTitleColor(questData);
        }
        else
        {
            questNameText.color = _defaultQuestTitleColor;
        }
    }

    private Color GetCoreRepairQuestTitleColor(QuestData questData)
    {
        if (questData == null)
        {
            return coreRepairQuestTitleColor;
        }

        if (CoreRepairManager.Instance == null)
        {
            return coreRepairQuestTitleColor;
        }

        CorePart part = CoreRepairManager.Instance.GetCorePartFromQuestId(questData.questId);
        CorePartData partData = CoreRepairManager.Instance.GetPartData(part);
        if (partData != null)
        {
            return partData.questTitleColor;
        }

        return coreRepairQuestTitleColor;
    }
}

