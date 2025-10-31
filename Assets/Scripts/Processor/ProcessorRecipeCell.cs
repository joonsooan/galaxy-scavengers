using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProcessorRecipeCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image recipeIcon;
    [SerializeField] private TMP_Text recipeName;
    [SerializeField] private TMP_Text recipeProcessTime;
    [SerializeField] private GameObject recipeCellPrefab;
    [SerializeField] private RectTransform contentParent;
    
    private ProcessorRecipe recipeData;
    private ResourceCost[] ingredients;
    private ResourceCost product;
    
    public void Initialize(ProcessorRecipe data)
    {
        recipeData = data;
        recipeName.text = recipeData.recipeName;
        recipeIcon.sprite = recipeData.recipeIcon;
        // recipeProcessTime.text = recipeData.processingTime.ToString();
        ingredients = recipeData.ingredients;

        foreach (ResourceCost ingredient in ingredients)
        {
            GameObject newCellObject = Instantiate(recipeCellPrefab, contentParent);
            ResourceInfoCell newCell = newCellObject.GetComponent<ResourceInfoCell>();

            if (newCell != null)
            {
                newCell.resourceImage.sprite = GetResourceImage(ingredient.resourceType);
                newCell.resourceAmount.text = ingredient.amount.ToString();
            }
        }
    }

    private Sprite GetResourceImage(ResourceType type)
    {
        return ResourceManager.Instance.GetResourceIcon(type);
    }

    public void OnClickCell()
    {
        // TODO : 옆에 각 셀의 정보를 보여주는 UI 띄우기
    }

    // TODO : 정보 보여주는 함수 수정
    // protected override void ShowInfo() => GameManager.Instance?.uiManager.DisplayRecipeInfo(recipeData);
    // protected override void HideInfo() => GameManager.Instance?.uiManager.HideRecipeInfo();
}