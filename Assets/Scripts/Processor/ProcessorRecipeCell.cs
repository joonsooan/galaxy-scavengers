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
    private ActiveRecipe _activeRecipe;
    
    private ResourceCost product;
    private int currentStorageAmount;
    private int produceMaxAmount;
    
    public void Initialize(ActiveRecipe activeRecipe)
    {
        _activeRecipe = activeRecipe;
        recipeData = _activeRecipe.recipeData;
        
        recipeName.text = $"{recipeData.resourceType}";
        recipeIcon.sprite = recipeData.recipeIcon;
        // recipeProcessTime.text = recipeData.processingTime.ToString();
        ingredients = recipeData.ingredients;
        
        produceMaxAmount = _activeRecipe.maxProductionLimit;
        
        UpdateCurrentAmount();
        UpdateUI();

        foreach (ResourceCost ingredient in ingredients)
        {
            GameObject newCellObject = Instantiate(recipeCellPrefab, contentParent);
            ResourceInfoCell newCell = newCellObject.GetComponent<ResourceInfoCell>();

            if (newCell != null)
            {
                // Set info without rebuilding immediately - we'll rebuild the parent after all cells are set
                newCell.SetInfo(ingredient.resourceType, ingredient.amount, false);
            }
        }
        
        // Rebuild all resource info cells first, then rebuild the ingredient panel
        // This ensures the ingredient panel updates its size after all cells have updated
        foreach (Transform child in contentParent)
        {
            ResourceInfoCell cell = child.GetComponent<ResourceInfoCell>();
            if (cell != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
            }
        }
        
        // Now rebuild the ingredient panel after all resource info cells have updated their sizes
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
    }
    
    private void OnEnable()
    {
        ResourceManager.OnResourceAmountChanged += HandleResourceChange;
    }

    private void OnDisable()
    {
        ResourceManager.OnResourceAmountChanged -= HandleResourceChange;
    }
    
    private void HandleResourceChange(ResourceType type, int newAmount)
    {
        if (recipeData != null && type == recipeData.resourceType)
        {
            currentStorageAmount = newAmount;
            UpdateUI();
            
            _activeRecipe.SetProductionLimit(produceMaxAmount);
        }
    }

    private void UpdateCurrentAmount()
    {
        if (recipeData == null) return;
        currentStorageAmount = ResourceManager.Instance.GetResourceAmount(recipeData.resourceType);
        Debug.Log(recipeData .resourceType + " Current Storage Amount: " + currentStorageAmount);
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
            
            _activeRecipe.SetProductionLimit(produceMaxAmount);
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
            
            _activeRecipe.SetProductionLimit(produceMaxAmount);
        }
    }
}