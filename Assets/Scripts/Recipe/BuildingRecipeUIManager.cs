using System.Collections.Generic;
using UnityEngine;

public class BuildingRecipeUIManager : MonoBehaviour
{
    public RecipeInfo recipeInfo;
    
    [Header("UI References")]
    public GameObject recipeCellPrefab;
    public Transform contentParent;
    
    private List<ComboCardData> _allRecipes;

    private void Awake()
    {
        LoadAllRecipes();
    }

    private void LoadAllRecipes()
    {
        _allRecipes = new List<ComboCardData>();
        ComboCardData[] loadedData = Resources.LoadAll<ComboCardData>("Combo Cards");
        _allRecipes.AddRange(loadedData);
        
        InstantiateRecipeCells();
    }
    
    private void InstantiateRecipeCells()
    {
        if (recipeCellPrefab == null || contentParent == null)
        {
            return;
        }

        foreach (var recipeData in _allRecipes)
        {
            GameObject newCellObject = Instantiate(recipeCellPrefab, contentParent);
            
            RecipeCell newCell = newCellObject.GetComponent<RecipeCell>();
            if (newCell != null)
            {
                newCell.Initialize(recipeData);
            }
        }
    }

    public ComboCardData GetRecipeByName(string comboName)
    {
        return _allRecipes.Find(recipe => recipe.displayName == comboName);
    }

    public List<ComboCardData> GetAllRecipes()
    {
        return _allRecipes;
    }
}