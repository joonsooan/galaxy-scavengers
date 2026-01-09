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
    private readonly List<ModuleGridCell> _recipeCells = new List<ModuleGridCell>();
    private ModuleData _currentData;

    private ModuleStation _currentStation;

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

        if (moduleStationPanel != null) {
            moduleStationPanel.SetActive(true);
        }

        if (moduleDetailPanel != null) {
            moduleDetailPanel.gameObject.SetActive(true);
            moduleDetailPanel.ClearInfo();
        }

        UpdateStationInfo();
        LoadAllRecipes();
    }

    private void HidePanel()
    {
        if (moduleStationPanel != null) {
            moduleStationPanel.SetActive(false);
        }

        if (moduleDetailPanel != null) {
            moduleDetailPanel.HidePanel();
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
}
