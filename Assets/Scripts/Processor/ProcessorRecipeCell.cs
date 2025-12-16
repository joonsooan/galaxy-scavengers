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
    
    private ProcessorRecipe _recipeData;
    private ResourceCost[] _ingredients;
    private ActiveRecipe _activeRecipe;
    
    private ResourceCost _product;
    private int _currentStorageAmount;
    private int _produceMaxAmount;
    
    public void Initialize(ActiveRecipe activeRecipe)
    {
        _activeRecipe = activeRecipe;
        _recipeData = _activeRecipe.recipeData;
        
        recipeName.text = $"{_recipeData.resourceType}";
        recipeIcon.sprite = _recipeData.recipeIcon;
        _ingredients = _recipeData.ingredients;
        
        _produceMaxAmount = _activeRecipe.maxProductionLimit;
        
        UpdateCurrentAmount();
        UpdateUI();

        foreach (ResourceCost ingredient in _ingredients)
        {
            GameObject newCellObject = Instantiate(recipeCellPrefab, contentParent);
            ResourceInfoCell newCell = newCellObject.GetComponent<ResourceInfoCell>();

            if (newCell != null)
            {
                newCell.SetInfo(ingredient.resourceType, ingredient.amount, false);
            }
        }

        foreach (Transform child in contentParent)
        {
            ResourceInfoCell cell = child.GetComponent<ResourceInfoCell>();
            if (cell != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
            }
        }
        
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
        if (_recipeData != null && type == _recipeData.resourceType)
        {
            _currentStorageAmount = newAmount;
            UpdateUI();
            
            _activeRecipe.SetProductionLimit(_produceMaxAmount);
        }
    }

    private void UpdateCurrentAmount()
    {
        if (_recipeData == null) return;
        _currentStorageAmount = ResourceManager.Instance.GetResourceAmount(_recipeData.resourceType);
        Debug.Log(_recipeData .resourceType + " Current Storage Amount: " + _currentStorageAmount);
    }

    private void UpdateUI()
    {
        produceInfoText.text = $"{_currentStorageAmount} / {_produceMaxAmount}";
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
        int newAmount = Mathf.Min(_produceMaxAmount + amountToAdd, MaxProduceAmount);

        if (_produceMaxAmount != newAmount)
        {
            _produceMaxAmount = newAmount;
            UpdateUI();
            
            _activeRecipe.SetProductionLimit(_produceMaxAmount);
        }
    }

    public void OnMinusBtnClick()
    {
        int amountToSubtract = GetAmountChange();
        int newAmount = Mathf.Max(_produceMaxAmount - amountToSubtract, 0);

        if (_produceMaxAmount != newAmount)
        {
            _produceMaxAmount = newAmount;
            UpdateUI();
            
            _activeRecipe.SetProductionLimit(_produceMaxAmount);
        }
    }
}