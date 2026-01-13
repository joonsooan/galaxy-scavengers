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
    [SerializeField] private QuestInfoPanel questDetailPanel;
    [SerializeField] private QuestProvider questProvider = QuestProvider.NPC_1;

    private readonly List<ModuleGridCell> _recipeCells = new List<ModuleGridCell>();
    private readonly List<QuestCell> _questCells = new List<QuestCell>();
    private ModuleData _currentData;
    private ModuleStation _currentStation;
    private bool _isQuestMode = false;

    private void Start()
    {
        if (moduleStationPanel != null) {
            moduleStationPanel.SetActive(false);
        }

        if (moduleDetailPanel != null) {
            moduleDetailPanel.Initialize(this);
        }

        if (closeButton != null) {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        if (questButton != null) {
            questButton.onClick.RemoveAllListeners();
            questButton.onClick.AddListener(OnQuestButtonClicked);
        }
        
        if (shopButton != null) {
            shopButton.onClick.RemoveAllListeners();
            shopButton.onClick.AddListener(OnShopButtonClicked);
        }
        
        if (questGridPanel != null) {
            questGridPanel.SetActive(false);
        }
        
        if (questDetailPanel != null) {
            questDetailPanel.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        ModuleStation.OnModuleStationClicked += ShowModuleStationUI;
        
        if (QuestDataManager.Instance != null) {
            QuestDataManager.Instance.OnQuestStateChanged += OnQuestStateChanged;
        }
    }

    private void OnDisable()
    {
        ModuleStation.OnModuleStationClicked -= ShowModuleStationUI;
        
        if (QuestDataManager.Instance != null) {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChanged;
        }
    }
    
    private void OnQuestStateChanged(int questId)
    {
        // Refresh quest cells if we're in quest mode
        if (_isQuestMode) {
            LoadQuestCells();
        }
    }

    private void ShowModuleStationUI(ModuleStation station)
    {
        _currentStation = station;
        _currentData = station.ModuleData;

        if (moduleStationPanel != null) {
            moduleStationPanel.SetActive(true);
        }

        if (moduleDetailPanel != null) {
            moduleDetailPanel.gameObject.SetActive(true);
            moduleDetailPanel.ClearInfo();
        }

        UpdateStationInfo();
        ShowShopUI();
    }

    private void HidePanel()
    {
        if (moduleStationPanel != null) {
            moduleStationPanel.SetActive(false);
        }

        if (moduleDetailPanel != null) {
            moduleDetailPanel.HidePanel();
        }
        
        if (questDetailPanel != null) {
            questDetailPanel.gameObject.SetActive(false);
        }
    }

    private void OnCloseButtonClicked()
    {
        HidePanel();
    }

    private void UpdateStationInfo()
    {
        if (_currentData == null) return;

        if (stationNameText != null) {
            stationNameText.text = _currentData.StationName;
        }

        if (stationInfoText != null) {
            stationInfoText.text = _currentData.StationInfo;
        }
    }

    private void LoadAllRecipes()
    {
        ClearAllRecipes();

        if (_currentData == null || recipeGridContainer == null || moduleGridCellPrefab == null) {
            return;
        }

        GridLayoutGroup gridLayout = recipeGridContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout == null) {
            gridLayout = recipeGridContainer.AddComponent<GridLayoutGroup>();
        }

        foreach (ModuleRecipe recipe in _currentData.Recipes) {
            GameObject cellObj = Instantiate(moduleGridCellPrefab, recipeGridContainer.transform);
            ModuleGridCell cell = cellObj.GetComponent<ModuleGridCell>();

            if (cell != null) {
                cell.Initialize(recipe, this);
                _recipeCells.Add(cell);
            }
        }
    }

    private void ClearAllRecipes()
    {
        if (recipeGridContainer == null) return;

        foreach (Transform child in recipeGridContainer.transform) {
            Destroy(child.gameObject);
        }

        _recipeCells.Clear();
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
        
        // Hide shop UI
        if (recipeGridContainer != null) {
            recipeGridContainer.SetActive(false);
        }
        
        if (moduleDetailPanel != null) {
            moduleDetailPanel.HidePanel();
        }
        
        // Show quest UI
        if (questGridPanel != null) {
            questGridPanel.SetActive(true);
        }
        
        // Hide quest detail panel initially (will show when quest cell is clicked)
        if (questDetailPanel != null) {
            questDetailPanel.gameObject.SetActive(false);
        }
        
        LoadQuestCells();
    }
    
    private void ShowShopUI()
    {
        _isQuestMode = false;
        
        // Hide quest UI
        if (questGridPanel != null) {
            questGridPanel.SetActive(false);
        }
        
        if (questDetailPanel != null) {
            questDetailPanel.gameObject.SetActive(false);
        }
        
        // Show shop UI
        if (recipeGridContainer != null) {
            recipeGridContainer.SetActive(true);
        }
        
        LoadAllRecipes();
    }
    
    private void LoadQuestCells()
    {
        ClearQuestCells();
        
        if (questGridParent == null || questCellPrefab == null || questDetailPanel == null) {
            return;
        }
        
        if (QuestDataManager.Instance == null) {
            return;
        }
        
        // Get current quests for this provider (Available, Active, or Completed)
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
