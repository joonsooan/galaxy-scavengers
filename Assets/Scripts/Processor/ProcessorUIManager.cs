using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ProcessorUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject recipeCellPrefab;
    public Transform contentParent;
    
    [Header("Display UI")]
    [SerializeField] private TMP_Text processorName;
    [SerializeField] private TMP_Text processorInfo;
    
    // [Header("Test")]
    // [SerializeField] private ResourceProcessorData testData;

    private List<ProcessorRecipe> _allRecipes;
    private ResourceProcessorData _currentData;

    // public void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.P))
    //         OnClick(testData);
    //     else if (Input.GetKeyDown(KeyCode.R))
    //         ClearAllRecipes();
    // }

    public void OnClick(ResourceProcessorData data)
    {
        SetProcessorInfo(data);
        LoadAllRecipes(data);
    }

    private void SetProcessorInfo(ResourceProcessorData data)
    {
        processorName.text = data.ProcessorName;
        processorInfo.text = data.ProcessorInfo;
    }

    private void LoadAllRecipes(ResourceProcessorData data)
    {
        if (data == _currentData) return;
        ClearAllRecipes();
        _allRecipes = data.Recipes;
        InstantiateRecipeCells();
    }
    
    private void ClearAllRecipes()
    {
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
    }
    
    private void InstantiateRecipeCells()
    {
        if (recipeCellPrefab == null || contentParent == null)
        {
            Debug.LogError("Recipe Cell Prefab and Content Parent not set");
            return;
        }

        foreach (var recipeData in _allRecipes)
        {
            GameObject newCellObject = Instantiate(recipeCellPrefab, contentParent);
            ProcessorRecipeCell newCell = newCellObject.GetComponent<ProcessorRecipeCell>();
            
            if (newCell != null)
            {
                newCell.Initialize(recipeData);
            }
        }
    }
}

