using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private Button cellButton;
    [SerializeField] private GameObject configureIcon;

    private QuestData _questData;
    private QuestDetailPanel _questDetailPanel;
    private QuestUIHandler _questUIHandler;
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

    public void Initialize(QuestData questData, QuestDetailPanel questDetailPanel, bool isNew, QuestUIHandler questUIHandler = null)
    {
        _questData = questData;
        _questDetailPanel = questDetailPanel;
        _questUIHandler = questUIHandler;
        _isNew = isNew;

        if (questNameText != null && questData != null)
        {
            questNameText.text = questData.questName;
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
        if (_questData != null && _questDetailPanel != null)
        {
            _questDetailPanel.DisplayQuestInfo(_questData, _questData.questId);
            
            MarkAsViewed();
            
            if (_questUIHandler != null)
            {
                _questUIHandler.OnQuestViewed(_questData.questId);
            }
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

