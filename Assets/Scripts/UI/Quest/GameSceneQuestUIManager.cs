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
    [SerializeField] private GameObject questDetailPanelObject;
    [SerializeField] private GameObject requestQuestAcceptPanelObject;
    [SerializeField] private Button toggleQuestPanelButton;
    
    [Header("Notification Settings")]
    [SerializeField] private GameObject notifierIcon;
    [SerializeField] private Material shaderMaterial;
    [SerializeField] private string shaderMaterialResourcePath;
    
    private readonly List<QuestCell> _questCells = new ();
    private readonly HashSet<int> _viewedQuestIds = new ();
    private readonly HashSet<int> _acceptedRequestQuests = new ();
    private bool _isPanelOpen;

    private QuestDetailPanel _questDetailPanel;
    private RequestQuestAcceptPanel _requestQuestAcceptPanel;
    
    private void Awake()
    {
        if (questDetailPanelObject != null)
        {
            _questDetailPanel = questDetailPanelObject.GetComponent<QuestDetailPanel>();
            if (_questDetailPanel != null)
            {
                _questDetailPanel.SetGameSceneMode(true);
            }
        }

        if (requestQuestAcceptPanelObject != null)
        {
            _requestQuestAcceptPanel = requestQuestAcceptPanelObject.GetComponent<RequestQuestAcceptPanel>();
        }
        
        if (questPanel != null)
        {
            Button panelButton = questPanel.GetComponent<Button>();
            if (panelButton == null)
            {
                panelButton = questPanel.GetComponentInChildren<Button>();
            }
            if (panelButton != null)
            {
                panelButton.onClick.RemoveAllListeners();
                panelButton.onClick.AddListener(ToggleQuestPanel);
            }
        }
        
        if (shaderMaterial == null && !string.IsNullOrEmpty(shaderMaterialResourcePath))
        {
            shaderMaterial = Resources.Load<Material>(shaderMaterialResourcePath);
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
        
        if (questPanel != null)
        {
            Button panelButton = questPanel.GetComponent<Button>();
            if (panelButton == null)
            {
                panelButton = questPanel.GetComponentInChildren<Button>();
            }
            if (panelButton != null)
            {
                panelButton.onClick.RemoveAllListeners();
                panelButton.onClick.AddListener(ToggleQuestPanel);
            }
        }
        
        if (_questDetailPanel != null)
        {
            _questDetailPanel.SetGameSceneMode(true);
        }
        
        if (questCellGridParent != null)
        {
            questCellGridParent.gameObject.SetActive(false);
        }
        
        if (questDetailPanelObject != null)
        {
            questDetailPanelObject.SetActive(false);
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
        
        List<QuestData> coreRepairQuests = QuestDataManager.Instance.GetAllQuests()
            .Where(q => q != null && q.questType == QuestType.CoreRepairQuest)
            .ToList();
        
        foreach (QuestData quest in coreRepairQuests)
        {
            _viewedQuestIds.Remove(quest.questId);
        }
        
        yield return null;
        
        LoadActiveQuests();
    }
    
    private void OnQuestStateChanged(int questId)
    {
        QuestData questData = QuestDataManager.Instance?.GetQuestData(questId);
        if (questData != null)
        {
            if (questData.questType == QuestType.RequestQuest && _acceptedRequestQuests.Contains(questId))
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
            
            if (questData.questType == QuestType.CoreRepairQuest)
            {
                QuestState state = QuestDataManager.Instance.GetQuestState(questId);
                if ((state == QuestState.Active || state == QuestState.Completable) && !_viewedQuestIds.Contains(questId))
                {
                    ShowNotifierForNewQuest(questId);
                }
            }
            
            if (questData.questType == QuestType.RequestQuest)
            {
                QuestState state = QuestDataManager.Instance.GetQuestState(questId);
                if (state == QuestState.Completed || state == QuestState.Finished)
                {
                    LoadActiveQuests();
                    return;
                }
                if (state == QuestState.Available && !_viewedQuestIds.Contains(questId) && !_acceptedRequestQuests.Contains(questId))
                {
                    ShowNotifierForNewQuest(questId);
                }
            }
        }
        
        LoadActiveQuests();
        
        if (_questDetailPanel != null)
        {
            _questDetailPanel.RefreshQuestState(questId);
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
    
    private void DisableShaderMaterial()
    {
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
    
    private IEnumerator RefreshQuestListAfterSpawn()
    {
        yield return null;
        yield return null;
        LoadActiveQuests();
    }
    
    public void LoadActiveQuests()
    {
        if (questCellPrefab == null || questCellGridParent == null || _questDetailPanel == null)
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
            bool isRequestQuestFinished = quest.questType == QuestType.RequestQuest && (state == QuestState.Completed || state == QuestState.Finished);
            bool shouldInclude = (isActive || isRequestQuestAvailable || isRequestQuestAccepted) && !isRequestQuestFinished;
            
            return shouldInclude;
        }).ToList();
        
        foreach (QuestData quest in activeQuests)
        {
            GameObject cellObject = Instantiate(questCellPrefab, questCellGridParent);
            QuestCell questCell = cellObject.GetComponent<QuestCell>();
            
            if (questCell != null)
            {
                bool isNew = false;
                if (quest.questType == QuestType.RequestQuest)
                {
                    isNew = !_viewedQuestIds.Contains(quest.questId) && !_acceptedRequestQuests.Contains(quest.questId);
                }
                else if (quest.questType == QuestType.CoreRepairQuest)
                {
                    isNew = !_viewedQuestIds.Contains(quest.questId);
                    if (isNew)
                    {
                        ShowNotifierForNewQuest(quest.questId);
                    }
                }
                questCell.Initialize(quest, _questDetailPanel, isNew, null, this);
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
        
        if (_questDetailPanel != null && questDetailPanelObject != null && questDetailPanelObject.activeSelf)
        {
            _questDetailPanel.ClearQuestInfo();
            questDetailPanelObject.SetActive(false);
        }
        
        if (questCellGridParent != null)
        {
            questCellGridParent.gameObject.SetActive(true);
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
        
        if (questCellGridParent != null)
        {
            questCellGridParent.gameObject.SetActive(false);
        }
        
        if (questDetailPanelObject != null)
        {
            if (_questDetailPanel != null)
            {
                _questDetailPanel.ClearQuestInfo();
            }
            questDetailPanelObject.SetActive(false);
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
        bool hasActiveNotifier = false;
        
        foreach (QuestCell cell in _questCells)
        {
            if (cell != null)
            {
                QuestData questData = cell.GetQuestData();
                if (questData != null)
                {
                    bool shouldShowNotifier = false;
                    
                    if (questData.questType == QuestType.BaseQuest)
                    {
                        QuestState state = QuestDataManager.Instance.GetQuestState(questData.questId);
                        shouldShowNotifier = state == QuestState.Completable;
                    }
                    else if (questData.questType == QuestType.CoreRepairQuest)
                    {
                        shouldShowNotifier = !_viewedQuestIds.Contains(questData.questId);
                    }
                    else if (questData.questType == QuestType.RequestQuest)
                    {
                        bool isAccepted = _acceptedRequestQuests.Contains(questData.questId);
                        if (isAccepted)
                        {
                            QuestState state = QuestDataManager.Instance.GetQuestState(questData.questId);
                            shouldShowNotifier = state == QuestState.Completable;
                        }
                        else
                        {
                            shouldShowNotifier = !_viewedQuestIds.Contains(questData.questId);
                        }
                    }
                    
                    if (shouldShowNotifier)
                    {
                        hasActiveNotifier = true;
                        break;
                    }
                }
            }
        }
        
        if (notifierIcon != null)
        {
            notifierIcon.SetActive(hasActiveNotifier);
        }
        
        if (shaderMaterial != null)
        {
            if (hasActiveNotifier)
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
            else
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
        
        if (_requestQuestAcceptPanel == null || requestQuestAcceptPanelObject == null)
        {
            return;
        }
        
        if (QuestDataManager.Instance == null)
        {
            return;
        }
        
        QuestState state = QuestDataManager.Instance.GetQuestState(questData.questId);
        bool isAccepted = _acceptedRequestQuests.Contains(questData.questId);
        
        if (state != QuestState.Available || isAccepted)
        {
            return;
        }
        
        if (questDetailPanelObject != null && questDetailPanelObject.activeSelf && _questDetailPanel != null)
        {
            _questDetailPanel.ClearQuestInfo();
            questDetailPanelObject.SetActive(false);
        }
        
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null && !gameManager.IsPaused)
        {
            gameManager.TogglePause();
        }
        
        _requestQuestAcceptPanel.DisplayQuestInfo(questData);
        requestQuestAcceptPanelObject.SetActive(true);
    }
    
    public void OnRequestQuestAccepted(int questId)
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StartQuest(questId);
        }
        
        _acceptedRequestQuests.Add(questId);
        _viewedQuestIds.Add(questId);
        
        DisableShaderMaterial();
        
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
        
        _viewedQuestIds.Add(questId);
        
        DisableShaderMaterial();
        
        CloseRequestQuestAcceptPanel();
        LoadActiveQuests();
        UpdateNotifierIcons();
    }
    
    private void CloseRequestQuestAcceptPanel()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null && gameManager.IsPaused)
        {
            gameManager.TogglePause();
        }
        
        if (_requestQuestAcceptPanel != null && requestQuestAcceptPanelObject != null)
        {
            _requestQuestAcceptPanel.ClearQuestInfo();
            requestQuestAcceptPanelObject.SetActive(false);
        }
    }
    
    public bool IsRequestQuestAccepted(int questId)
    {
        return _acceptedRequestQuests.Contains(questId);
    }
    
    public bool IsQuestViewed(int questId)
    {
        return _viewedQuestIds.Contains(questId);
    }
    
    public void ShowQuestDetailPanel()
    {
        if (questDetailPanelObject != null)
        {
            questDetailPanelObject.SetActive(true);
        }
    }
    
    public void SelectButtonAfterFrame(GameObject buttonObject)
    {
        if (buttonObject != null && gameObject.activeInHierarchy)
        {
            StartCoroutine(SelectButtonAfterFrameCoroutine(buttonObject));
        }
    }
    
    private IEnumerator SelectButtonAfterFrameCoroutine(GameObject buttonObject)
    {
        yield return null;
        if (buttonObject != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(buttonObject);
        }
    }
}
