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

    private QuestData _questData;
    private QuestDetailPanel _questDetailPanel;
    private QuestUIHandler _questUIHandler;
    private GameSceneQuestUIManager _gameSceneQuestUIManager;
    private bool _isNew;

    private void Awake()
    {
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
        
        UpdateConfigureIcon(_isNew, _questData);
    }

    public void Initialize(QuestData questData, QuestDetailPanel questDetailPanel, bool isNew, QuestUIHandler questUIHandler = null, GameSceneQuestUIManager gameSceneQuestUIManager = null)
    {
        _questData = questData;
        _questDetailPanel = questDetailPanel;
        _questUIHandler = questUIHandler;
        _gameSceneQuestUIManager = gameSceneQuestUIManager;
        _isNew = isNew;

        if (questNameText != null && questData != null)
        {
            questNameText.text = questData.questName;
        }
        
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
        
        if (questState == QuestState.Available || questState == QuestState.Completable)
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
            if (questData.questType == QuestType.CoreRepairQuest)
            {
                shouldShowNotifier = isNew;
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
                        shouldShowNotifier = isNew;
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
        if (_questData == null) return;
        
        if (_questData.questType == QuestType.RequestQuest)
        {
            if (_gameSceneQuestUIManager == null)
            {
                _gameSceneQuestUIManager = FindFirstObjectByType<GameSceneQuestUIManager>();
            }
            
            if (_gameSceneQuestUIManager != null && QuestDataManager.Instance != null)
            {
                QuestState state = QuestDataManager.Instance.GetQuestState(_questData.questId);
                bool isAccepted = _gameSceneQuestUIManager.IsRequestQuestAccepted(_questData.questId);
                
                if (state == QuestState.Available && !isAccepted)
                {
                    _gameSceneQuestUIManager.ShowRequestQuestAcceptPanel(_questData);
                    return;
                }
            }
        }
        
        if (_questDetailPanel != null)
        {
            _questDetailPanel.DisplayQuestInfo(_questData, _questData.questId);
            
            MarkAsViewed();
            
            if (_gameSceneQuestUIManager == null)
            {
                _gameSceneQuestUIManager = FindFirstObjectByType<GameSceneQuestUIManager>();
            }
            
            if (_gameSceneQuestUIManager != null)
            {
                _gameSceneQuestUIManager.MarkQuestAsViewed(_questData.questId);
            }
            
            if (_questUIHandler != null)
            {
                _questUIHandler.OnQuestViewed(_questData.questId);
            }
        }
        
        if (cellButton != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            StartCoroutine(SelectButtonAfterFrame());
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
}

