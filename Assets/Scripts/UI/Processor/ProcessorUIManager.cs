using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class ProcessorUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject recipeCellPrefab;
    public Transform contentParent;

    [Header("Resource picker")]
    [SerializeField] private Transform recipePickerParent;
    [SerializeField] private GameObject recipePickerIconPrefab;
    [SerializeField] private Button changeResourceButton;

    [Header("Display UI")]
    [SerializeField] private TMP_Text processorName;
    [SerializeField] private TMP_Text processorInfo;

    [Header("Unit Assignment")]
    public GameObject unitAssignCellPrefab;
    public Transform unitAssignContentParent;

    private ProcessorData _currentData;
    private Processor _currentProcessor;
    private bool _showResourcePicker;

    public static ProcessorUIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
        }
        else {
            Instance = this;
        }

        if (changeResourceButton != null) {
            changeResourceButton.onClick.AddListener(OnChangeResourceButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (changeResourceButton != null) {
            changeResourceButton.onClick.RemoveListener(OnChangeResourceButtonClicked);
        }
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        SetProcessorInfo(_currentData);
        if (_currentProcessor != null) {
            RefreshRecipeDisplay();
            InstantiateUnitAssignCells();
        }
    }

    private void OnChangeResourceButtonClicked()
    {
        _showResourcePicker = true;
        RefreshRecipeDisplay();
    }

    public void ShowProcessorUI(Processor processor)
    {
        _currentProcessor = processor;
        _currentData = processor != null ? processor.ProcessorData : null;
        _showResourcePicker = false;

        SetProcessorInfo(_currentData);
        RefreshRecipeDisplay();
        InstantiateUnitAssignCells();
    }

    private void SetProcessorInfo(ProcessorData data)
    {
        if (processorName != null) {
            processorName.text = data != null ? data.ProcessorName : string.Empty;
        }

        if (processorInfo != null) {
            processorInfo.text = data != null ? data.ProcessorInfo : string.Empty;
        }
    }

    private void RefreshRecipeDisplay()
    {
        Transform pickerRoot = recipePickerParent != null ? recipePickerParent : contentParent;
        Transform recipeRoot = contentParent;

        if (pickerRoot != recipeRoot) {
            ClearChildren(pickerRoot);
            ClearChildren(recipeRoot);
        }
        else {
            ClearChildren(recipeRoot);
        }

        if (changeResourceButton != null) {
            changeResourceButton.gameObject.SetActive(false);
        }

        if (_currentProcessor == null || _currentData == null) {
            return;
        }

        bool showGrid = !_currentProcessor.SelectedOutputResource.HasValue || _showResourcePicker;

        if (showGrid) {
            if (recipePickerParent != null) {
                EnsurePickerLayout(recipePickerParent);
            }

            BuildResourcePickerGrid(pickerRoot);
        }
        else {
            if (changeResourceButton != null) {
                changeResourceButton.gameObject.SetActive(true);
            }

            if (pickerRoot != recipeRoot) {
                ClearChildren(pickerRoot);
            }

            InstantiateSingleRecipeCell(recipeRoot);
        }
    }

    private static void EnsurePickerLayout(Transform pickerRoot)
    {
        if (pickerRoot == null) {
            return;
        }

        var rect = pickerRoot as RectTransform;
        if (rect == null) {
            return;
        }

        if (rect.GetComponent<GridLayoutGroup>() != null) {
            return;
        }

        var grid = rect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(64f, 64f);
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.Flexible;
    }

    private static Image GetRecipePickerIconImage(GameObject root)
    {
        if (root == null) {
            return null;
        }

        foreach (Transform child in root.transform) {
            Image nested = child.GetComponentInChildren<Image>(true);
            if (nested != null) {
                return nested;
            }
        }

        return root.GetComponent<Image>();
    }

    private void BuildResourcePickerGrid(Transform parent)
    {
        if (parent == null) {
            return;
        }

        if (recipePickerIconPrefab == null) {
            Debug.LogError("Recipe picker icon prefab not set");
            return;
        }

        List<ProcessorRecipe> recipes = _currentData.Recipes;
        if (recipes == null || recipes.Count == 0) {
            return;
        }

        HashSet<ResourceType> seen = new HashSet<ResourceType>();

        foreach (ProcessorRecipe recipe in recipes) {
            if (recipe == null) {
                continue;
            }

            if (!seen.Add(recipe.resourceType)) {
                continue;
            }

            ResourceType type = recipe.resourceType;
            GameObject iconObj = Instantiate(recipePickerIconPrefab, parent);
            Button btn = iconObj.GetComponentInChildren<Button>(true);

            if (btn == null) {
                Debug.LogError("Recipe picker icon prefab must include a Button (on root or child).");
                continue;
            }

            Image img = GetRecipePickerIconImage(iconObj);
            if (img == null) {
                continue;
            }

            Sprite recipeSprite = recipe.recipeIcon;
            if (recipeSprite == null && ResourceManager.Instance != null) {
                recipeSprite = ResourceManager.Instance.GetResourceIcon(type);
            }
            if (recipeSprite != null) {
                img.sprite = recipeSprite;
            }

            btn.onClick.RemoveAllListeners();
            ResourceType typeCopy = type;
            btn.onClick.AddListener(() => OnResourceTypePicked(typeCopy));
        }
    }

    private void OnResourceTypePicked(ResourceType type)
    {
        if (_currentProcessor == null) {
            return;
        }

        bool pickerMode = !_currentProcessor.SelectedOutputResource.HasValue || _showResourcePicker;

        if (pickerMode && _showResourcePicker && _currentProcessor.SelectedOutputResource.HasValue &&
            _currentProcessor.SelectedOutputResource.Value == type) {
            _showResourcePicker = false;
            RefreshRecipeDisplay();
            return;
        }

        if (pickerMode && !_showResourcePicker && _currentProcessor.SelectedOutputResource.HasValue &&
            _currentProcessor.SelectedOutputResource.Value == type) {
            _currentProcessor.SetSelectedOutputResource(null);
        }
        else {
            _currentProcessor.SetSelectedOutputResource(type);
            _showResourcePicker = false;
        }

        RefreshRecipeDisplay();
    }

    private void InstantiateSingleRecipeCell(Transform parent)
    {
        if (recipeCellPrefab == null || parent == null) {
            Debug.LogError("Recipe Cell Prefab and Content Parent not set");
            return;
        }

        if (!_currentProcessor.SelectedOutputResource.HasValue) {
            return;
        }

        ActiveRecipe activeRecipe = _currentProcessor.GetActiveRecipeForResource(_currentProcessor.SelectedOutputResource.Value);
        if (activeRecipe == null) {
            return;
        }

        GameObject newCellObject = Instantiate(recipeCellPrefab, parent);
        ProcessorRecipeCell newCell = newCellObject.GetComponent<ProcessorRecipeCell>();

        if (newCell != null) {
            newCell.Initialize(activeRecipe);
        }
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--) {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    private void InstantiateUnitAssignCells()
    {
        if (unitAssignContentParent == null) return;

        foreach (Transform child in unitAssignContentParent) {
            Destroy(child.gameObject);
        }

        if (unitAssignCellPrefab == null || _currentProcessor == null) return;

        int maxDrones = _currentProcessor.ProcessorData.MaxAssignedDrones;
        IReadOnlyList<Unit_Processor> assignedDrones = _currentProcessor.AssignedDrones;

        for (int i = 0; i < maxDrones; i++) {
            GameObject cellObj = Instantiate(unitAssignCellPrefab, unitAssignContentParent);
            UnitAssignCell cell = cellObj.GetComponent<UnitAssignCell>();

            if (cell != null) {
                Unit_Processor drone = i < assignedDrones.Count ? assignedDrones[i] : null;
                cell.Initialize(_currentProcessor, drone);
            }
        }
    }
}
