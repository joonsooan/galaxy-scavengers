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
    
    private bool _indicatorUpdatePending = false;

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
        if (questDetailPanel != null)
        {
            questDetailPanel.SetQuestUIHandler(this);
            questDetailPanel.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        LoadQuestProgress();
        StartCoroutine(SubscribeToQuestDataManagerWhenReady());
        StartCoroutine(SubscribeToBaseInventoryWhenReady());
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveQuestProgress();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveQuestProgress();
        }
    }
    
    private void OnDestroy()
    {
        SaveQuestProgress();
    }
    
    private IEnumerator SubscribeToQuestDataManagerWhenReady()
    {
        while (QuestDataManager.Instance == null)
        {
            yield return null;
        }
        
        QuestDataManager.Instance.OnQuestStateChanged += OnQuestStateChanged;
    }
    
    private IEnumerator SubscribeToBaseInventoryWhenReady()
    {
        BaseInventoryManager inventoryManager = null;
        while (inventoryManager == null)
        {
            inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
            yield return null;
        }
        
        inventoryManager.OnResourceChanged += OnBaseInventoryResourceChanged;
    }

    private void OnDisable()
    {
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChanged;
        }
        
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager != null)
        {
            inventoryManager.OnResourceChanged -= OnBaseInventoryResourceChanged;
        }
    }
    
    private void OnQuestStateChanged(int questId)
    {
        if (_isQuestMode)
        {
            if (QuestDataManager.Instance != null)
            {
                QuestData questData = QuestDataManager.Instance.GetQuestData(questId);
                if (questData != null && questData.questType == QuestType.BaseQuest)
                {
                    foreach (QuestCell cell in _questCells)
                    {
                        if (cell != null && cell.GetQuestData() != null && cell.GetQuestData().questId == questId)
                        {
                            cell.CheckAndUpdateCompletability();
                        }
                    }
                }
                else
                {
                    LoadQuestCells();
                }
            }
            else
            {
                LoadQuestCells();
            }
        }
        
        RequestIndicatorUpdate();
    }
    
    private void OnBaseInventoryResourceChanged(ResourceType resourceType, int amount)
    {
        if (_isQuestMode)
        {
            foreach (QuestCell cell in _questCells)
            {
                if (cell != null && cell.GetQuestData() != null)
                {
                    cell.CheckAndUpdateCompletability();
                }
            }
        }
        
        RequestIndicatorUpdate();
    }
    
    public void OnResourceChangedForIndicator()
    {
        RequestIndicatorUpdate();
    }
    
    public void OnQuestViewed(int questId)
    {
        _viewedQuestIds.Add(questId);
        SaveQuestProgress();
        RequestIndicatorUpdate();
    }
    
    public void OnQuestFinished(int questId)
    {
        _finishedQuestIds.Add(questId);
        SaveQuestProgress();
        
        if (_isQuestMode)
        {
            for (int i = _questCells.Count - 1; i >= 0; i--)
            {
                QuestCell cell = _questCells[i];
                if (cell != null && cell.GetQuestData() != null && cell.GetQuestData().questId == questId)
                {
                    Destroy(cell.gameObject);
                    _questCells.RemoveAt(i);
                }
            }
            
            LoadQuestCells();
        }
        
        RequestIndicatorUpdate();
    }
    
    private void RequestIndicatorUpdate()
    {
        if (!_indicatorUpdatePending)
        {
            _indicatorUpdatePending = true;
            StartCoroutine(UpdateIndicatorDelayed());
        }
    }
    
    private IEnumerator UpdateIndicatorDelayed()
    {
        yield return null;
        _indicatorUpdatePending = false;
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

    public void ShowQuestUI()
    {
        if (_uiProvider == null) return;
        
        _isQuestMode = true;
        
        _uiProvider.ClearDetailPanel();
        
        GameObject shopContainer = _uiProvider.GetShopUIContainer();
        shopContainer.SetActive(false);
        _uiProvider.HideShopUI();
        
        GameObject questGridPanel = _uiProvider.GetQuestGridPanel();
        questGridPanel.SetActive(true);
        
        QuestDetailPanel questDetailPanel = _uiProvider.GetQuestDetailPanel();
        questDetailPanel.ClearQuestInfo();
        questDetailPanel.gameObject.SetActive(true);
        
        UpdateButtonStates(showQuest: true);
        
        StartCoroutine(LoadQuestCellsWhenReady());
        RequestIndicatorUpdate();
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
        
        _uiProvider.ClearDetailPanel();
        
        UpdateButtonStates(showQuest: false);
    }
    
    private void UpdateButtonStates(bool showQuest)
    {
        if (_uiProvider == null) return;
        
        Button questButton = _uiProvider.GetQuestButton();
        Button shopButton = _uiProvider.GetShopButton();
        
        if (questButton != null && shopButton != null)
        {
            if (showQuest)
            {
                questButton.Select();
            }
            else
            {
                shopButton.Select();
            }
        }
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
        
        if (currentQuests != null)
        {
            currentQuests = currentQuests.Where(quest => !_finishedQuestIds.Contains(quest.questId) && quest.questType == QuestType.BaseQuest).ToList();
        }
        
        if (currentQuests == null || currentQuests.Count == 0)
        {
            return;
        }
        
        foreach (QuestData quest in currentQuests)
        {
            if (_finishedQuestIds.Contains(quest.questId))
            {
                continue;
            }
            
            GameObject cellObject = Instantiate(questCellPrefab, questGridParent);
            QuestCell questCell = cellObject.GetComponent<QuestCell>();
            
            if (questCell != null)
            {
                bool isNew = !_viewedQuestIds.Contains(quest.questId);
                
                questCell.Initialize(quest, questDetailPanel, isNew, this);
                _questCells.Add(questCell);
            }
            else
            {
                Debug.LogWarning($"QuestCell component not found on prefab {questCellPrefab.name}");
                Destroy(cellObject);
            }
        }
        
        StartCoroutine(RefreshQuestCellsAfterFrame());
        RequestIndicatorUpdate();
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
                if (quest.questType != QuestType.BaseQuest) return false;
                
                QuestState state = QuestDataManager.Instance.GetQuestState(quest.questId);
                
                return state == QuestState.Available || state == QuestState.Completable;
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
    
    private static readonly WaitForSeconds _wait01 = CoroutineCache.GetWaitForSeconds(0.1f);

    private IEnumerator UpdateIndicatorAfterInit()
    {
        yield return _wait01;
        RequestIndicatorUpdate();
    }
    
    private IEnumerator RefreshQuestCellsAfterFrame()
    {
        yield return null;
        foreach (QuestCell cell in _questCells)
        {
            if (cell != null && cell.GetQuestData() != null)
            {
                cell.CheckAndUpdateCompletability();
            }
        }
    }
    
    private void SaveQuestProgress()
    {
        string viewedIds = string.Join(",", _viewedQuestIds);
        PlayerPrefs.SetString("QuestViewedIds", viewedIds);
        
        string finishedIds = string.Join(",", _finishedQuestIds);
        PlayerPrefs.SetString("QuestFinishedIds", finishedIds);
        
        PlayerPrefs.Save();
    }
    
    private void LoadQuestProgress()
    {
        if (PlayerPrefs.HasKey("QuestViewedIds"))
        {
            string viewedIds = PlayerPrefs.GetString("QuestViewedIds");
            if (!string.IsNullOrEmpty(viewedIds))
            {
                string[] ids = viewedIds.Split(',');
                foreach (string idStr in ids)
                {
                    if (int.TryParse(idStr, out int questId))
                    {
                        _viewedQuestIds.Add(questId);
                    }
                }
            }
        }
        
        if (PlayerPrefs.HasKey("QuestFinishedIds"))
        {
            string finishedIds = PlayerPrefs.GetString("QuestFinishedIds");
            if (!string.IsNullOrEmpty(finishedIds))
            {
                string[] ids = finishedIds.Split(',');
                foreach (string idStr in ids)
                {
                    if (int.TryParse(idStr, out int questId))
                    {
                        _finishedQuestIds.Add(questId);
                    }
                }
            }
        }
    }
    
    public void ClearQuestProgress()
    {
        _viewedQuestIds.Clear();
        _finishedQuestIds.Clear();
        
        if (PlayerPrefs.HasKey("QuestViewedIds"))
        {
            PlayerPrefs.DeleteKey("QuestViewedIds");
        }
        
        if (PlayerPrefs.HasKey("QuestFinishedIds"))
        {
            PlayerPrefs.DeleteKey("QuestFinishedIds");
        }
        
        PlayerPrefs.Save();
        
        if (_isQuestMode)
        {
            LoadQuestCells();
        }
    }
    
    public void RefreshQuestUI()
    {
        if (_isQuestMode)
        {
            LoadQuestCells();
        }
        
        RequestIndicatorUpdate();
    }
}
