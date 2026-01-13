using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QuestUIHandler : MonoBehaviour
{
    private IQuestUIProvider _uiProvider;
    private readonly List<QuestCell> _questCells = new ();
    private bool _isQuestMode;

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
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged += OnQuestStateChanged;
        }
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
        
        ClearQuestCells();
        
        RectTransform questGridParent = _uiProvider.GetQuestGridParent();
        GameObject questCellPrefab = _uiProvider.GetQuestCellPrefab();
        QuestDetailPanel questDetailPanel = _uiProvider.GetQuestDetailPanel();
        QuestProvider questProvider = _uiProvider.GetQuestProvider();
        
        List<QuestData> currentQuests = QuestDataManager.Instance.GetCurrentQuestsByProvider(questProvider);
        
        foreach (QuestData quest in currentQuests)
        {
            GameObject cellObject = Instantiate(questCellPrefab, questGridParent);
            QuestCell questCell = cellObject.GetComponent<QuestCell>();
            
            if (questCell != null)
            {
                questCell.Initialize(quest, questDetailPanel);
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
        
       RectTransform questGridParent = _uiProvider.GetQuestGridParent();
       foreach (Transform child in questGridParent)
       {
           Destroy(child.gameObject);
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
}
