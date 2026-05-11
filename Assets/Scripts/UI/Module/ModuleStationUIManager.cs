using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class ModuleStationUIManager : MonoBehaviour
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
    [SerializeField] private List<ModuleGridCell> recipeCells = new ();

    private ModuleData _currentData;
    private ModuleStation _currentStation;

    private void Start()
    {
        moduleStationPanel.SetActive(false);
        moduleDetailPanel.Initialize();

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(OnCloseButtonClicked);
        ApplyLocalizedModuleStationChrome();
    }

    private void OnEnable()
    {
        ModuleStation.OnModuleStationClicked += ShowModuleStationUI;
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        ApplyLocalizedModuleStationChrome();
    }

    private void OnDisable()
    {
        ModuleStation.OnModuleStationClicked -= ShowModuleStationUI;
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyLocalizedModuleStationChrome();
        if (moduleStationPanel != null && moduleStationPanel.activeSelf && _currentData != null)
        {
            UpdateStationInfo();
        }

        if (moduleDetailPanel != null)
        {
            moduleDetailPanel.ApplyLocalizationRefresh();
        }
    }

    private void ApplyLocalizedModuleStationChrome()
    {
        if (moduleCraftingModeText != null)
        {
            moduleCraftingModeText.text = GameLocalization.GetOrDefault("UI_Common", "base.craftableModules",
                "\uC81C\uC791 \uAC00\uB2A5\uD55C \uBAA8\uB4C8");
        }

        if (stationInfoText != null && (moduleStationPanel == null || !moduleStationPanel.activeSelf))
        {
            stationInfoText.text = GameLocalization.GetOrDefault("UI_Common", "base.moduleStationDescription",
                "\uC2DC\uB4DC\uCF54\uC5B4\uC5D0 \uC124\uCE58\uD560 \uC218 \uC788\uB294 \uBAA8\uB4C8\uC744 \uC81C\uC791\uD558\uB294 \uC2A4\uD14C\uC774\uC158\uC785\uB2C8\uB2E4.");
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
}
