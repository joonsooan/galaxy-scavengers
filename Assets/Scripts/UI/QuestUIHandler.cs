using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class QuestUIHandler : MonoBehaviour
{
    private IQuestUIProvider _uiProvider;
    private readonly List<QuestCell> _questCells = new ();
    private bool _isQuestMode;
    
    private readonly HashSet<int> _viewedQuestIds = new ();
    private readonly HashSet<int> _finishedQuestIds = new ();

    public void Initialize(IQuestUIProvider uiProvider)
    {
        _uiProvider = uiProvider;
        
        Button questButton = _uiProvider.GetQuestButton();
        Button shopButton = _uiProvider.GetShopButton();
        
        questButton.onClick.RemoveAllListeners();
        questButton.onClick.AddListener(OnQuestButtonClicked);
        
        shopButton.onClick.RemoveAllListeners();
        shopButton.onClick.AddListener(OnShopButtonClicked);
        
        GameObject questGridPanel = _uiProvider.GetQuestGridPanel();
        questGridPanel.SetActive(false);
        
        QuestDetailPanel questDetailPanel = _uiProvider.GetQuestDetailPanel();
        questDetailPanel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        StartCoroutine(SubscribeToQuestDataManagerWhenReady());
    }
    
    private IEnumerator SubscribeToQuestDataManagerWhenReady()
    {
        while (QuestDataManager.Instance == null)
        {
            yield return null;
        }
        
        QuestDataManager.Instance.OnQuestStateChanged += OnQuestStateChanged;
    }

    private void OnDisable()
    {
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChanged;
        }
    }
    
    private void OnQuestStateChanged(int questId)
    {
        if (_isQuestMode)
        {
            LoadQuestCells();
        }
        
        UpdateNewQuestIndicator();
    }
    
    public void OnQuestViewed(int questId)
    {
        _viewedQuestIds.Add(questId);
        UpdateNewQuestIndicator();
    }
    
    public void OnQuestFinished(int questId)
    {
        _finishedQuestIds.Add(questId);
        if (_isQuestMode)
        {
            LoadQuestCells();
        }
        UpdateNewQuestIndicator();
    }
    
    private void OnQuestButtonClicked()
    {
        ShowQuestUI();
    }
    
    private void OnShopButtonClicked()
    {
        ShowShopUI();
    }

    private void ShowQuestUI()
    {
        if (_uiProvider == null) return;
        
        _isQuestMode = true;
        
        // Clear shop detail panel when switching to quest UI
        _uiProvider.ClearDetailPanel();
        
        GameObject shopContainer = _uiProvider.GetShopUIContainer();
        shopContainer.SetActive(false);
        _uiProvider.HideShopUI();
        
        GameObject questGridPanel = _uiProvider.GetQuestGridPanel();
        questGridPanel.SetActive(true);
        
        QuestDetailPanel questDetailPanel = _uiProvider.GetQuestDetailPanel();
        questDetailPanel.ClearQuestInfo();
        questDetailPanel.gameObject.SetActive(true);
        
        StartCoroutine(LoadQuestCellsWhenReady());
        UpdateNewQuestIndicator();
    }
    
    private IEnumerator LoadQuestCellsWhenReady()
    {
        while (QuestDataManager.Instance == null)
        {
            yield return null;
        }
        
        yield return null;
        
        LoadQuestCells();
    }
    
    public void ShowShopUI()
    {
        if (_uiProvider == null) return;
        
        _isQuestMode = false;
        
        GameObject questGridPanel = _uiProvider.GetQuestGridPanel();
        questGridPanel.SetActive(false);
        
        QuestDetailPanel questDetailPanel = _uiProvider.GetQuestDetailPanel();
        questDetailPanel.ClearQuestInfo();
        questDetailPanel.gameObject.SetActive(false);
        
        GameObject shopContainer = _uiProvider.GetShopUIContainer();
        shopContainer.SetActive(true);
        _uiProvider.ShowShopUI();
        
        // Clear detail panel when switching to shop UI
        _uiProvider.ClearDetailPanel();
    }
    
    private void LoadQuestCells()
    {
        if (_uiProvider == null) return;
        
        if (QuestDataManager.Instance == null)
        {
            Debug.LogWarning("QuestUIHandler: QuestDataManager.Instance is null. Cannot load quest cells.");
            return;
        }
        
        ClearQuestCells();
        
        RectTransform questGridParent = _uiProvider.GetQuestGridParent();
        GameObject questCellPrefab = _uiProvider.GetQuestCellPrefab();
        QuestDetailPanel questDetailPanel = _uiProvider.GetQuestDetailPanel();
        QuestProvider questProvider = _uiProvider.GetQuestProvider();
        
        if (questGridParent == null)
        {
            Debug.LogError("QuestUIHandler: Quest grid parent is null. Cannot instantiate quest cells.");
            return;
        }
        
        if (questCellPrefab == null)
        {
            Debug.LogError("QuestUIHandler: Quest cell prefab is null. Cannot instantiate quest cells.");
            return;
        }
        
        if (questDetailPanel == null)
        {
            Debug.LogError("QuestUIHandler: Quest detail panel is null. Cannot initialize quest cells.");
            return;
        }
        
        List<QuestData> currentQuests = QuestDataManager.Instance.GetCurrentQuestsByProvider(questProvider);
        
        if (currentQuests != null && currentQuests.Count > 0)
        {
            currentQuests = currentQuests.Where(quest => !_finishedQuestIds.Contains(quest.questId)).ToList();
        }
        
        if (currentQuests == null || currentQuests.Count == 0)
        {
            List<QuestData> allQuests = QuestDataManager.Instance.GetAllQuests();
            // Debug.LogWarning($"QuestUIHandler: No quests found for provider {questProvider}. Total quests in system: {allQuests.Count}");
            
            foreach (QuestData quest in allQuests)
            {
                QuestState state = QuestDataManager.Instance.GetQuestState(quest.questId);
                // Debug.Log($"  Quest {quest.questId} ({quest.questName}): Provider={quest.questProvider}, State={state}");
            }
            
            return;
        }
        
        // Debug.Log($"QuestUIHandler: Loading {currentQuests.Count} quest(s) for provider {questProvider}");
        
        foreach (QuestData quest in currentQuests)
        {
            GameObject cellObject = Instantiate(questCellPrefab, questGridParent);
            QuestCell questCell = cellObject.GetComponent<QuestCell>();
            
            if (questCell != null)
            {
                bool isNew = !_viewedQuestIds.Contains(quest.questId);
                
                questCell.Initialize(quest, questDetailPanel, isNew);
                _questCells.Add(questCell);
            }
            else
            {
                Debug.LogWarning($"QuestCell component not found on prefab {questCellPrefab.name}");
                Destroy(cellObject);
            }
        }
        
        UpdateNewQuestIndicator();
    }
    
    private void ClearQuestCells()
    {
        foreach (QuestCell cell in _questCells)
        {
            if (cell != null)
            {
                Destroy(cell.gameObject);
            }
        }
        _questCells.Clear();
        
        if (_uiProvider == null) return;
        
        RectTransform questGridParent = _uiProvider.GetQuestGridParent();
        if (questGridParent != null)
        {
            foreach (Transform child in questGridParent)
            {
                Destroy(child.gameObject);
            }
        }
    }
    
    public void HideQuestUI()
    {
        if (_uiProvider == null) return;
        
        GameObject questGridPanel = _uiProvider.GetQuestGridPanel();
        questGridPanel.SetActive(false);
        
        QuestDetailPanel questDetailPanel = _uiProvider.GetQuestDetailPanel();
        questDetailPanel.gameObject.SetActive(false);
    }
    
    private void UpdateNewQuestIndicator()
    {
        if (_uiProvider == null || QuestDataManager.Instance == null) return;
        
        QuestProvider questProvider = _uiProvider.GetQuestProvider();
        List<QuestData> currentQuests = QuestDataManager.Instance.GetCurrentQuestsByProvider(questProvider);
        
        if (currentQuests != null)
        {
            currentQuests = currentQuests.Where(quest =>
            {
                if (_finishedQuestIds.Contains(quest.questId)) return false;
                
                QuestState state = QuestDataManager.Instance.GetQuestState(quest.questId);
                if (state == QuestState.Locked) return false;
                
                if (state == QuestState.Active)
                {
                    bool isCompletable = QuestDataManager.Instance.CheckQuestCompletion(quest.questId);
                    bool isUnchecked = !_viewedQuestIds.Contains(quest.questId);
                    return isCompletable && isUnchecked;
                }
                
                return false;
            }).ToList();
        }
        
        bool hasNewQuests = currentQuests != null && currentQuests.Count > 0;
        
        GameObject newQuestIndicator = _uiProvider.GetNewQuestIndicator();
        if (newQuestIndicator != null)
        {
            newQuestIndicator.SetActive(hasNewQuests);
        }
    }
    
    private void Start()
    {
        if (QuestDataManager.Instance != null)
        {
            StartCoroutine(UpdateIndicatorAfterInit());
        }
    }
    
    private IEnumerator UpdateIndicatorAfterInit()
    {
        yield return new WaitForSeconds(0.1f);
        UpdateNewQuestIndicator();
    }
}
