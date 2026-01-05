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

    private static ModuleStationUIManager Instance { get; set; }
    
    private ModuleStation _currentStation;
    private ModuleData _currentData;
    private readonly List<ModuleGridCell> _recipeCells = new();
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    
    private void Start()
    {
        if (moduleStationPanel != null)
        {
            moduleStationPanel.SetActive(false);
        }
        
        if (moduleDetailPanel != null)
        {
            moduleDetailPanel.Initialize(this);
        }
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
        
        // Refresh module grid in inventory system to show current modules
        BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        if (inventorySystem != null)
        {
            inventorySystem.RefreshModuleGrid();
        }
        
        if (moduleStationPanel != null)
        {
            moduleStationPanel.SetActive(true);
        }
        
        UpdateStationInfo();
        LoadAllRecipes();
    }

    private void HidePanel()
    {
        if (moduleStationPanel != null)
        {
            moduleStationPanel.SetActive(false);
        }
    }
    
    private void UpdateStationInfo()
    {
        if (_currentData == null) return;
        
        if (stationNameText != null)
        {
            stationNameText.text = _currentData.StationName;
        }
        
        if (stationInfoText != null)
        {
            stationInfoText.text = _currentData.StationInfo;
        }
    }
    
    private void LoadAllRecipes()
    {
        ClearAllRecipes();
        
        if (_currentData == null || recipeGridContainer == null || moduleGridCellPrefab == null)
        {
            return;
        }
        
        // Ensure grid layout group exists
        GridLayoutGroup gridLayout = recipeGridContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            gridLayout = recipeGridContainer.AddComponent<GridLayoutGroup>();
        }
        
        foreach (ModuleRecipe recipe in _currentData.Recipes)
        {
            GameObject cellObj = Instantiate(moduleGridCellPrefab, recipeGridContainer.transform);
            ModuleGridCell cell = cellObj.GetComponent<ModuleGridCell>();
            
            if (cell != null)
            {
                cell.Initialize(recipe, this);
                _recipeCells.Add(cell);
            }
        }
    }
    
    private void ClearAllRecipes()
    {
        if (recipeGridContainer == null) return;
        
        foreach (Transform child in recipeGridContainer.transform)
        {
            Destroy(child.gameObject);
        }
        
        _recipeCells.Clear();
    }
    
    public void ShowModuleDetail(ModuleRecipe recipe)
    {
        if (moduleDetailPanel != null && _currentStation != null)
        {
            moduleDetailPanel.ShowModuleDetail(recipe, _currentStation);
        }
    }
    
    public void OnModuleCrafted()
    {
        // Refresh recipe cells to update availability
        foreach (ModuleGridCell cell in _recipeCells)
        {
            if (cell != null && cell.Recipe != null && _currentStation != null)
            {
                // Could update visual state here if needed
            }
        }
    }
}

