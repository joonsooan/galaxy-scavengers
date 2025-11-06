using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProcessorRecipeCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image recipeIcon;
    [SerializeField] private TMP_Text recipeName;
    [SerializeField] private TMP_Text recipeProcessTime;
    [SerializeField] private TMP_Text produceInfoText;
    [SerializeField] private GameObject recipeCellPrefab;
    [SerializeField] private RectTransform contentParent;

    private const int MaxProduceAmount = 999;
    
    private ProcessorRecipe recipeData;
    private ResourceCost[] ingredients;
    private ResourceCost product;
    private int currentStorageAmount;
    private int produceMaxAmount;
    
    public void Initialize(ProcessorRecipe data)
    {
        recipeData = data;
        recipeName.text = $"{recipeData.resourceType}";
        recipeIcon.sprite = recipeData.recipeIcon;
        // recipeProcessTime.text = recipeData.processingTime.ToString();
        ingredients = recipeData.ingredients;
        
        currentStorageAmount = ResourceManager.Instance.GetResourceAmount(recipeData.resourceType);
        UpdateUI();

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

    private void UpdateUI()
    {
        produceInfoText.text = $"{currentStorageAmount} / {produceMaxAmount}";
    }

    private Sprite GetResourceImage(ResourceType type)
    {
        return ResourceManager.Instance.GetResourceIcon(type);
    }
    
    private int GetAmountChange()
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            return 100;
        }
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            return 10;
        }
        return 1;
    }

    public void OnPlusBtnClick()
    {
        int amountToAdd = GetAmountChange();
        int newAmount = Mathf.Min(produceMaxAmount + amountToAdd, MaxProduceAmount);

        if (produceMaxAmount != newAmount)
        {
            produceMaxAmount = newAmount;
            UpdateUI();
        }
    }

    public void OnMinusBtnClick()
    {
        int amountToSubtract = GetAmountChange();
        int newAmount = Mathf.Max(produceMaxAmount - amountToSubtract, 0);

        if (produceMaxAmount != newAmount)
        {
            produceMaxAmount = newAmount;
            UpdateUI();
        }
    }
}