using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModuleStationUIManager : MonoBehaviour, IQuestUIProvider
{
    [Header("UI References")]
    [SerializeField] private GameObject moduleStationPanel;
    [SerializeField] private TMP_Text stationNameText;
    [SerializeField] private TMP_Text stationInfoText;
    [SerializeField] private TMP_Text moduleCraftingModeText;
    [SerializeField] private GameObject recipeGridContainer;
    [SerializeField] private GameObject moduleGridCellPrefab;
    [SerializeField] private ModuleDetailPanel moduleDetailPanel;
    [SerializeField] private Button closeButton;
    
    [Header("Quest UI")]
    [SerializeField] private Button questButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private GameObject questGridPanel;
    [SerializeField] private RectTransform questGridParent;
    [SerializeField] private GameObject questCellPrefab;
    [SerializeField] private QuestDetailPanel questDetailPanel;
    [SerializeField] private QuestProvider questProvider = QuestProvider.NPC_1;
    [SerializeField] private QuestUIHandler questUIHandler;
    [SerializeField] private GameObject newQuestIndicator;
    [SerializeField] private GameObject questBlockPanel;
    [SerializeField] private int unlockQuestId = -1;
    [SerializeField] private List<ModuleGridCell> recipeCells = new ();

    private ModuleData _currentData;
    private ModuleStation _currentStation;

    private void Start()
    {
        moduleStationPanel.SetActive(false);
        moduleDetailPanel.Initialize();

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(OnCloseButtonClicked);
        
        if (questUIHandler == null)
        {
            questUIHandler = gameObject.AddComponent<QuestUIHandler>();
        }
        questUIHandler.Initialize(this);
    }

    private void OnEnable()
    {
        ModuleStation.OnModuleStationClicked += ShowModuleStationUI;
        SubscribeToQuestEvents();
    }

    private void OnDisable()
    {
        ModuleStation.OnModuleStationClicked -= ShowModuleStationUI;
        UnsubscribeFromQuestEvents();
    }

    private void ShowModuleStationUI(ModuleStation station)
    {
        _currentStation = station;
        _currentData = station.ModuleData;

        moduleStationPanel.SetActive(true);
        moduleDetailPanel.gameObject.SetActive(true);
        moduleDetailPanel.ClearInfo();

        UpdateStationInfo();
        
        if (newQuestIndicator != null && newQuestIndicator.activeSelf && questUIHandler != null)
        {
            questUIHandler.ShowQuestUI();
        }
        else if (questUIHandler != null)
        {
            questUIHandler.ShowShopUI();
        }
        else
        {
            ShowShopUI();
        }
    }

    private void HidePanel()
    {
        moduleStationPanel.SetActive(false);
        moduleDetailPanel.HidePanel();
        if (questUIHandler != null)
        {
            questUIHandler.HideQuestUI();
        }
    }

    private void OnCloseButtonClicked()
    {
        HidePanel();
    }

    private void UpdateStationInfo()
    {
        if (_currentData == null) return;
        
        stationNameText.text = _currentData.StationName;
        stationInfoText.text = _currentData.StationInfo;
    }

    private void LoadAllRecipes()
    {
        ClearAllRecipes();

        if (_currentData == null || recipeGridContainer == null || moduleGridCellPrefab == null) {
            return;
        }
        
        foreach (ModuleRecipe recipe in _currentData.Recipes) {
            GameObject cellObj = Instantiate(moduleGridCellPrefab, recipeGridContainer.transform);
            ModuleGridCell cell = cellObj.GetComponent<ModuleGridCell>();

            if (cell != null) {
                cell.Initialize(recipe, this);
                recipeCells.Add(cell);
            }
        }
    }

    private void ClearAllRecipes()
    {
        if (recipeGridContainer == null) return;

        foreach (Transform child in recipeGridContainer.transform) {
            Destroy(child.gameObject);
        }

        recipeCells.Clear();
    }

    public void ShowModuleDetail(ModuleRecipe recipe)
    {
        if (moduleDetailPanel != null && _currentStation != null) {
            moduleDetailPanel.ShowInfo(recipe, _currentStation);
        }
    }
    
    public Button GetQuestButton() => questButton;
    public Button GetShopButton() => shopButton;
    public GameObject GetQuestGridPanel() => questGridPanel;
    public RectTransform GetQuestGridParent() => questGridParent;
    public GameObject GetQuestCellPrefab() => questCellPrefab;
    public QuestDetailPanel GetQuestDetailPanel() => questDetailPanel;
    public QuestProvider GetQuestProvider() => questProvider;
    public GameObject GetShopUIContainer() => recipeGridContainer;
    
    public void ShowShopUI()
    {
        recipeGridContainer.SetActive(true);
        moduleDetailPanel.ShowPanel();
        if (moduleCraftingModeText != null)
        {
            moduleCraftingModeText.gameObject.SetActive(true);
        }
        LoadAllRecipes();
    }
    
    public void HideShopUI()
    {
        recipeGridContainer.SetActive(false);
        moduleDetailPanel.HidePanel();
        if (moduleCraftingModeText != null)
        {
            moduleCraftingModeText.gameObject.SetActive(false);
        }
    }
    
    public void ClearDetailPanel()
    {
        if (moduleDetailPanel != null)
        {
            moduleDetailPanel.ClearInfo();
        }
    }
    
    public GameObject GetNewQuestIndicator() => newQuestIndicator;
    
    public string GetUIName() => "Module Station UI";

    private void SubscribeToQuestEvents()
    {
        if (QuestDataManager.Instance != null && unlockQuestId >= 0)
        {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChangedForBlocker;
            QuestDataManager.Instance.OnQuestStateChanged += OnQuestStateChangedForBlocker;
            UpdateQuestBlockPanel();
        }
        else if (questBlockPanel != null && unlockQuestId >= 0)
        {
            questBlockPanel.SetActive(true);
        }
    }

    private void UnsubscribeFromQuestEvents()
    {
        if (QuestDataManager.Instance != null && unlockQuestId >= 0)
        {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChangedForBlocker;
        }
    }

    private void OnQuestStateChangedForBlocker(int questId)
    {
        if (questId == unlockQuestId)
        {
            UpdateQuestBlockPanel();
        }
    }

    private void UpdateQuestBlockPanel()
    {
        if (questBlockPanel == null || unlockQuestId < 0)
        {
            return;
        }

        if (QuestDataManager.Instance == null)
        {
            questBlockPanel.SetActive(true);
            return;
        }

        bool isCompleted = QuestDataManager.Instance.IsQuestCompleted(unlockQuestId);
        questBlockPanel.SetActive(!isCompleted);
    }
}
