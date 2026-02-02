using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneQuestUIManager : MonoBehaviour
{
    [Header("Quest UI References")]
    [SerializeField] private GameObject questPanel;
    [SerializeField] private GameObject questCellPrefab;
    [SerializeField] private Transform questCellGridParent;
    [SerializeField] private QuestDetailPanel questDetailPanel;
    [SerializeField] private QuestDetailPanel requestQuestAcceptPanel;
    [SerializeField] private Button toggleQuestPanelButton;
    
    [Header("Notification Settings")]
    [SerializeField] private GameObject notifierIcon;
    [SerializeField] private Material shaderMaterial;
    
    private readonly List<QuestCell> _questCells = new List<QuestCell>();
    private readonly HashSet<int> _viewedQuestIds = new HashSet<int>();
    private readonly HashSet<int> _acceptedRequestQuests = new HashSet<int>();
    private bool _isPanelOpen = false;
    private float _savedTimeScale = 1f;
    
    private void Awake()
    {
        if (questDetailPanel != null)
        {
            questDetailPanel.SetGameSceneMode(true);
        }
    }
    
    private void OnEnable()
    {
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged += OnQuestStateChanged;
        }
        
        if (RequestQuestManager.Instance != null)
        {
            RequestQuestManager.OnRequestQuestSpawned += OnRequestQuestSpawned;
        }
        
        if (toggleQuestPanelButton != null)
        {
            toggleQuestPanelButton.onClick.RemoveAllListeners();
            toggleQuestPanelButton.onClick.AddListener(ToggleQuestPanel);
        }
        
        if (questDetailPanel != null)
        {
            questDetailPanel.SetGameSceneMode(true);
        }
        
        if (questCellGridParent != null)
        {
            questCellGridParent.gameObject.SetActive(false);
        }
        
        if (questDetailPanel != null)
        {
            questDetailPanel.gameObject.SetActive(false);
        }
        
        if (notifierIcon != null)
        {
            notifierIcon.SetActive(false);
        }
        
        if (shaderMaterial != null)
        {
            shaderMaterial.SetFloat("_Enabled", 0f);
        }
    }
    
    private void OnDisable()
    {
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChanged;
        }
        
        RequestQuestManager.OnRequestQuestSpawned -= OnRequestQuestSpawned;
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            ToggleQuestPanel();
        }
    }
    
    private void Start()
    {
        StartCoroutine(InitializeWhenReady());
    }
    
    private IEnumerator InitializeWhenReady()
    {
        while (QuestDataManager.Instance == null)
        {
            yield return null;
        }
        
        yield return null;
        
        LoadActiveQuests();
    }
    
    private void OnQuestStateChanged(int questId)
    {
        QuestData questData = QuestDataManager.Instance?.GetQuestData(questId);
        if (questData != null && questData.questType == QuestType.RequestQuest && _acceptedRequestQuests.Contains(questId))
        {
            QuestState state = QuestDataManager.Instance.GetQuestState(questId);
            foreach (QuestCell cell in _questCells)
            {
                if (cell != null && cell.GetQuestData() != null && cell.GetQuestData().questId == questId)
                {
                    cell.CheckAndUpdateCompletability();
                }
            }
        }
        
        LoadActiveQuests();
        
        if (questDetailPanel != null)
        {
            questDetailPanel.RefreshQuestState(questId);
        }
        
        UpdateNotifierIcons();
    }
    
    private void OnRequestQuestSpawned(QuestData questData)
    {
        if (questData != null)
        {
            ShowNotifierForNewQuest(questData.questId);
            StartCoroutine(RefreshQuestListAfterSpawn());
        }
    }
    
    private IEnumerator RefreshQuestListAfterSpawn()
    {
        yield return null;
        yield return null;
        LoadActiveQuests();
    }
    
    public void LoadActiveQuests()
    {
        if (questCellPrefab == null || questCellGridParent == null || questDetailPanel == null)
        {
            return;
        }
        
        if (QuestDataManager.Instance == null)
        {
            return;
        }
        
        ClearQuestCells();
        
        List<QuestData> allQuests = QuestDataManager.Instance.GetAllQuests();
        
        List<QuestData> activeQuests = allQuests.Where(quest =>
        {
            if (quest == null) return false;
            
            QuestState state = QuestDataManager.Instance.GetQuestState(quest.questId);
            bool isActive = state == QuestState.Active || state == QuestState.Completable;
            bool isRequestQuestAvailable = quest.questType == QuestType.RequestQuest && state == QuestState.Available;
            bool isRequestQuestAccepted = quest.questType == QuestType.RequestQuest && _acceptedRequestQuests.Contains(quest.questId);
            bool shouldInclude = isActive || isRequestQuestAvailable || isRequestQuestAccepted;
            
            return shouldInclude;
        }).ToList();
        
        foreach (QuestData quest in activeQuests)
        {
            GameObject cellObject = Instantiate(questCellPrefab, questCellGridParent);
            QuestCell questCell = cellObject.GetComponent<QuestCell>();
            
            if (questCell != null)
            {
                bool isNew = !_viewedQuestIds.Contains(quest.questId) && quest.questType == QuestType.RequestQuest && !_acceptedRequestQuests.Contains(quest.questId);
                questCell.Initialize(quest, questDetailPanel, isNew, null, this);
                _questCells.Add(questCell);
            }
            else
            {
                Destroy(cellObject);
            }
        }
        
        UpdateNotifierIcons();
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
        
        if (questCellGridParent != null)
        {
            foreach (Transform child in questCellGridParent)
            {
                Destroy(child.gameObject);
            }
        }
    }
    
    private void ToggleQuestPanel()
    {
        if (_isPanelOpen)
        {
            HideQuestPanel();
        }
        else
        {
            ShowQuestPanel();
        }
    }

    private void ShowQuestPanel()
    {
        _isPanelOpen = true;
        
        if (questCellGridParent != null)
        {
            questCellGridParent.gameObject.SetActive(true);
        }
        
        if (questDetailPanel != null)
        {
            questDetailPanel.gameObject.SetActive(true);
        }
        
        if (shaderMaterial != null)
        {
            if (shaderMaterial.HasProperty("_Enabled"))
            {
                shaderMaterial.SetFloat("_Enabled", 0f);
            }
            else if (shaderMaterial.HasProperty("_Intensity"))
            {
                shaderMaterial.SetFloat("_Intensity", 0f);
            }
        }
    }

    private void HideQuestPanel()
    {
        _isPanelOpen = false;
        
        if (questDetailPanel != null)
        {
            questDetailPanel.ClearQuestInfo();
            questDetailPanel.gameObject.SetActive(false);
        }
        
        if (questCellGridParent != null)
        {
            questCellGridParent.gameObject.SetActive(false);
        }
    }
    
    private void ShowNotifierForNewQuest(int questId)
    {
        if (notifierIcon != null)
        {
            notifierIcon.SetActive(true);
        }
        
        if (shaderMaterial != null)
        {
            if (shaderMaterial.HasProperty("_Enabled"))
            {
                shaderMaterial.SetFloat("_Enabled", 1f);
            }
            else if (shaderMaterial.HasProperty("_Intensity"))
            {
                shaderMaterial.SetFloat("_Intensity", 1f);
            }
        }
    }
    
    private void UpdateNotifierIcons()
    {
        bool hasUnreadQuests = false;
        bool hasCompletableRequestQuests = false;
        
        foreach (QuestCell cell in _questCells)
        {
            if (cell != null)
            {
                QuestData questData = cell.GetQuestData();
                if (questData != null)
                {
                    bool isUnread = !_viewedQuestIds.Contains(questData.questId) && 
                                   questData.questType == QuestType.RequestQuest && 
                                   !_acceptedRequestQuests.Contains(questData.questId);
                    
                    if (questData.questType == QuestType.RequestQuest && _acceptedRequestQuests.Contains(questData.questId))
                    {
                        QuestState state = QuestDataManager.Instance.GetQuestState(questData.questId);
                        if (state == QuestState.Completable)
                        {
                            hasCompletableRequestQuests = true;
                        }
                    }
                    
                    if (isUnread)
                    {
                        hasUnreadQuests = true;
                    }
                }
            }
        }
        
        if (notifierIcon != null)
        {
            notifierIcon.SetActive(hasUnreadQuests || hasCompletableRequestQuests);
        }
        
        if (shaderMaterial != null && !hasUnreadQuests && !hasCompletableRequestQuests)
        {
            if (shaderMaterial.HasProperty("_Enabled"))
            {
                shaderMaterial.SetFloat("_Enabled", 0f);
            }
            else if (shaderMaterial.HasProperty("_Intensity"))
            {
                shaderMaterial.SetFloat("_Intensity", 0f);
            }
        }
    }
    
    public void MarkQuestAsViewed(int questId)
    {
        _viewedQuestIds.Add(questId);
        UpdateNotifierIcons();
    }
    
    public void ShowRequestQuestAcceptPanel(QuestData questData)
    {
        if (questData == null)
        {
            return;
        }
        
        if (questData.questType != QuestType.RequestQuest)
        {
            return;
        }
        
        if (requestQuestAcceptPanel == null)
        {
            return;
        }
        
        if (QuestDataManager.Instance == null)
        {
            return;
        }
        
        QuestState state = QuestDataManager.Instance.GetQuestState(questData.questId);
        if (state != QuestState.Available || _acceptedRequestQuests.Contains(questData.questId))
        {
            return;
        }
        
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        
        requestQuestAcceptPanel.DisplayQuestInfo(questData, questData.questId);
        requestQuestAcceptPanel.gameObject.SetActive(true);
    }
    
    public void OnRequestQuestAccepted(int questId)
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StartQuest(questId);
        }
        
        _acceptedRequestQuests.Add(questId);
        _viewedQuestIds.Add(questId);
        
        CloseRequestQuestAcceptPanel();
        LoadActiveQuests();
        UpdateNotifierIcons();
    }
    
    public void OnRequestQuestRejected(int questId)
    {
        if (RequestQuestManager.Instance != null)
        {
            QuestData questData = QuestDataManager.Instance?.GetQuestData(questId);
            if (questData != null)
            {
                RequestQuestManager.Instance.RemoveRequestQuest(questData);
            }
        }
        
        CloseRequestQuestAcceptPanel();
        LoadActiveQuests();
        UpdateNotifierIcons();
    }
    
    private void CloseRequestQuestAcceptPanel()
    {
        Time.timeScale = _savedTimeScale;
        
        if (requestQuestAcceptPanel != null)
        {
            requestQuestAcceptPanel.ClearQuestInfo();
            requestQuestAcceptPanel.gameObject.SetActive(false);
        }
    }
    
    public bool IsRequestQuestAccepted(int questId)
    {
        return _acceptedRequestQuests.Contains(questId);
    }
}
