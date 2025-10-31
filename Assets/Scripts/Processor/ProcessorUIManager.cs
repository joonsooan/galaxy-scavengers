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

    private List<ProcessorRecipe> _allRecipes;
    
    public void OnClick(ResourceProcessorData data)
    {
        LoadAllRecipes(data);
        // DisplayProcessorInfo(data);
    }

    private void LoadAllRecipes(ResourceProcessorData data)
    {
        _allRecipes = data.Recipes;
        InstantiateRecipeCells();
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

    // public ComboCardData GetRecipeByName(string comboName)
    // {
    //     return _allRecipes.Find(recipe => recipe.displayName == comboName);
    // }
    //
    // public List<ComboCardData> GetAllRecipes()
    // {
    //     return _allRecipes;
    // }
}

