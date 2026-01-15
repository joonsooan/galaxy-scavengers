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
    }

    private void OnDisable()
    {
        ModuleStation.OnModuleStationClicked -= ShowModuleStationUI;
    }

    private void ShowModuleStationUI(ModuleStation station)
    {
        _currentStation = station;
        _currentData = station.ModuleData;

        moduleStationPanel.SetActive(true);
        moduleDetailPanel.gameObject.SetActive(true);
        moduleDetailPanel.ClearInfo();

        UpdateStationInfo();
        ShowShopUI();
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
        LoadAllRecipes();
    }
    
    public void HideShopUI()
    {
        recipeGridContainer.SetActive(false);
        moduleDetailPanel.HidePanel();
    }
    
    public void ClearDetailPanel()
    {
        if (moduleDetailPanel != null)
        {
            moduleDetailPanel.ClearInfo();
        }
    }
}
