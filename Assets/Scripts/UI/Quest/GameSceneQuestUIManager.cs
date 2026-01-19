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
    [SerializeField] private Button toggleQuestPanelButton;
    [SerializeField] private TMP_Text toggleButtonText;
    
    private readonly List<QuestCell> _questCells = new List<QuestCell>();
    private bool _isPanelOpen = false;
    
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
        
        if (toggleQuestPanelButton != null)
        {
            toggleQuestPanelButton.onClick.RemoveAllListeners();
            toggleQuestPanelButton.onClick.AddListener(ToggleQuestPanel);
        }
        
        if (questDetailPanel != null)
        {
            questDetailPanel.SetGameSceneMode(true);
        }
        
        if (questPanel != null)
        {
            questPanel.SetActive(false);
        }
        
        if (questCellGridParent != null)
        {
            questCellGridParent.gameObject.SetActive(false);
        }
        
        if (questDetailPanel != null)
        {
            questDetailPanel.gameObject.SetActive(false);
        }
    }
    
    private void OnDisable()
    {
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChanged;
        }
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
        LoadActiveQuests();
        
        if (questDetailPanel != null)
        {
            questDetailPanel.RefreshQuestState(questId);
        }
    }
    
    private void LoadActiveQuests()
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
            QuestState state = QuestDataManager.Instance.GetQuestState(quest.questId);
            return state == QuestState.Active || state == QuestState.Completable;
        }).ToList();
        
        foreach (QuestData quest in activeQuests)
        {
            GameObject cellObject = Instantiate(questCellPrefab, questCellGridParent);
            QuestCell questCell = cellObject.GetComponent<QuestCell>();
            
            if (questCell != null)
            {
                questCell.Initialize(quest, questDetailPanel, false, null);
                _questCells.Add(questCell);
            }
            else
            {
                Debug.LogWarning($"QuestCell component not found on prefab {questCellPrefab.name}");
                Destroy(cellObject);
            }
        }
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
        if (questPanel == null) return;
        
        _isPanelOpen = true;
        questPanel.SetActive(true);
        
        if (questCellGridParent != null)
        {
            questCellGridParent.gameObject.SetActive(true);
        }
        
        if (questDetailPanel != null)
        {
            questDetailPanel.gameObject.SetActive(true);
        }
    }

    private void HideQuestPanel()
    {
        if (questPanel == null) return;
        
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
        
        questPanel.SetActive(false);
    }
}
