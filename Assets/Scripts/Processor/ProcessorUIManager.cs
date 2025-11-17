using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ProcessorUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject recipeCellPrefab;
    public Transform contentParent;
    public UnitAssignPanel unitAssignPanel;

    [Header("Display UI")]
    [SerializeField] private TMP_Text processorName;
    [SerializeField] private TMP_Text processorInfo;

    [Header("Unit Assignment")]
    public GameObject unitAssignCellPrefab;
    public Transform unitAssignContentParent;

    // [Header("Test")]
    // [SerializeField] private ResourceProcessorData testData;

    private List<ProcessorRecipe> _allRecipes;
    private ProcessorData _currentData;
    private Processor _currentProcessor;

    public static ProcessorUIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
        }
        else {
            Instance = this;
        }
    }

    // private void OnEnable()
    // {
    //     ResourceProcessor.OnProcessorClicked += ShowProcessorUI;
    // }
    //
    // private void OnDisable()
    // {
    //     ResourceProcessor.OnProcessorClicked -= ShowProcessorUI;
    // }

    // public void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.P))
    //         OnClick(testData);
    //     else if (Input.GetKeyDown(KeyCode.R))
    //         ClearAllRecipes();
    // }

    public void ShowProcessorUI(Processor processor)
    {
        _currentProcessor = processor;
        _currentData = processor.ProcessorData;

        SetProcessorInfo(_currentData);
        LoadAllRecipes(_currentData);

        InstantiateUnitAssignCells();
    }

    private void SetProcessorInfo(ProcessorData data)
    {
        processorName.text = data.ProcessorName;
        processorInfo.text = data.ProcessorInfo;
    }

    private void LoadAllRecipes(ProcessorData data)
    {
        ClearAllRecipes();
        _allRecipes = data.Recipes;
        InstantiateRecipeCells();
    }

    private void ClearAllRecipes()
    {
        foreach (Transform child in contentParent) {
            Destroy(child.gameObject);
        }
    }

    private void InstantiateRecipeCells()
    {
        if (recipeCellPrefab == null || contentParent == null) {
            Debug.LogError("Recipe Cell Prefab and Content Parent not set");
            return;
        }

        foreach (var activeRecipe in _currentProcessor.ActiveRecipes) {
            GameObject newCellObject = Instantiate(recipeCellPrefab, contentParent);
            ProcessorRecipeCell newCell = newCellObject.GetComponent<ProcessorRecipeCell>();

            if (newCell != null) {
                newCell.Initialize(activeRecipe);
            }
        }
    }

    private void InstantiateUnitAssignCells()
    {
        foreach (Transform child in unitAssignContentParent) {
            Destroy(child.gameObject);
        }

        if (unitAssignCellPrefab == null || _currentProcessor == null) return;

        int maxDrones = _currentProcessor.ProcessorData.MaxAssignedDrones;
        IReadOnlyList<Unit_Drone> assignedDrones = _currentProcessor.AssignedDrones;

        for (int i = 0; i < maxDrones; i++) {
            GameObject cellObj = Instantiate(unitAssignCellPrefab, unitAssignContentParent);
            UnitAssignCell cell = cellObj.GetComponent<UnitAssignCell>();

            if (cell != null) {
                Unit_Drone drone = i < assignedDrones.Count ? assignedDrones[i] : null;
                cell.Initialize(_currentProcessor, drone);
            }
        }
    }
}
