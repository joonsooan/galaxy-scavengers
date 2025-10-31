using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProcessorRecipeCell : MonoBehaviour
{
    private ProcessorRecipe recipeData;
    private TMP_Text recipeName;
    private Image recipeIcon;
    private ResourceCost[] ingredients;
    private ResourceCost product;
    private float processingTime;
    
    public void Initialize(ProcessorRecipe data)
    {
        recipeData = data;
        recipeName.text = recipeData.recipeName;
        recipeIcon.sprite = recipeData.recipeIcon;
        ingredients = recipeData.ingredients;
        processingTime = recipeData.processingTime;
    }

    public void OnClickCell()
    {
        // TODO : 옆에 각 셀의 정보를 보여주는 UI 띄우기
    }

    // TODO : 정보 보여주는 함수 수정
    // protected override void ShowInfo() => GameManager.Instance?.uiManager.DisplayRecipeInfo(recipeData);
    // protected override void HideInfo() => GameManager.Instance?.uiManager.HideRecipeInfo();
}