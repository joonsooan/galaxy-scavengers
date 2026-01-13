using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModuleStationUIManager : MonoBehaviour
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
    [SerializeField] private List<ModuleGridCell> recipeCells = new ();

    private readonly List<QuestCell> _questCells = new ();
    private ModuleData _currentData;
    private ModuleStation _currentStation;
    private bool _isQuestMode;

    private void Start()
    {
        moduleStationPanel.SetActive(false);
        moduleDetailPanel.Initialize(this);

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(OnCloseButtonClicked);
        questButton.onClick.RemoveAllListeners();
        questButton.onClick.AddListener(OnQuestButtonClicked);
        shopButton.onClick.RemoveAllListeners();
        shopButton.onClick.AddListener(OnShopButtonClicked);

        questGridPanel.SetActive(false);
        questDetailPanel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        ModuleStation.OnModuleStationClicked += ShowModuleStationUI;
        QuestDataManager.Instance.OnQuestStateChanged += OnQuestStateChanged;
    }

    private void OnDisable()
    {
        ModuleStation.OnModuleStationClicked -= ShowModuleStationUI;
        QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChanged;
    }
    
    private void OnQuestStateChanged(int questId)
    {
        if (_isQuestMode) {
            LoadQuestCells();
        }
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
        questDetailPanel.gameObject.SetActive(false);
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

    public void OnModuleCrafted()
    {
        // Module crafting is handled by BaseInventorySystem events
        // No additional action needed here
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
        _isQuestMode = true;

        recipeGridContainer.SetActive(false);
        moduleDetailPanel.HidePanel();
        questGridPanel.SetActive(true);
        questDetailPanel.gameObject.SetActive(true);
        
        LoadQuestCells();
    }
    
    private void ShowShopUI()
    {
        _isQuestMode = false;
        
        questGridPanel.SetActive(false);
        questDetailPanel.ClearQuestInfo();
        questDetailPanel.gameObject.SetActive(false);
        recipeGridContainer.SetActive(true);
        moduleDetailPanel.ShowPanel();
        LoadAllRecipes();
    }
    
    private void LoadQuestCells()
    {
        ClearQuestCells();
        
        List<QuestData> currentQuests = QuestDataManager.Instance.GetCurrentQuestsByProvider(questProvider);
        
        foreach (QuestData quest in currentQuests) {
            GameObject cellObject = Instantiate(questCellPrefab, questGridParent);
            QuestCell questCell = cellObject.GetComponent<QuestCell>();
            
            if (questCell != null) {
                questCell.Initialize(quest, questDetailPanel);
                _questCells.Add(questCell);
            }
            else {
                Debug.LogWarning($"QuestCell component not found on prefab {questCellPrefab.name}");
                Destroy(cellObject);
            }
        }
    }
    
    private void ClearQuestCells()
    {
        foreach (QuestCell cell in _questCells) {
            if (cell != null) {
                Destroy(cell.gameObject);
            }
        }
        _questCells.Clear();
        
        if (questGridParent != null) {
            foreach (Transform child in questGridParent) {
                Destroy(child.gameObject);
            }
        }
    }
}
