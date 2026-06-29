using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProcessorRecipeCell : MonoBehaviour
{
    private const float ButtonClickCooldown = 0.1f;
    [Header("UI References")]
    [SerializeField] private Image recipeIcon;
    [SerializeField] private TMP_Text recipeName;
    [SerializeField] private TMP_Text recipeProcessTime;
    [SerializeField] private TMP_Text produceInfoText;
    [SerializeField] private GameObject recipeCellPrefab;
    [SerializeField] private RectTransform contentParent;

    [Header("Sound Effects")]
    [SerializeField] private EventReference plusButtonSound;
    [SerializeField] private EventReference minusButtonSound;

    [Header("Tutorial Settings")]
    [SerializeField] private Material glowMaterial;
    private ActiveRecipe _activeRecipe;

    private int _currentStorageAmount;
    private ResourceCost[] _ingredients;

    private float _lastButtonClickTime;
    private int _produceMaxAmount;

    private ResourceCost _product;

    private ProcessorRecipe _recipeData;
    private string _tutorialID;

    private void OnEnable()
    {
        ResourceManager.OnResourceAmountChanged += HandleResourceChange;
    }

    private void OnDisable()
    {
        ResourceManager.OnResourceAmountChanged -= HandleResourceChange;
    }

    public void Initialize(ActiveRecipe activeRecipe)
    {
        _activeRecipe = activeRecipe;
        _recipeData = _activeRecipe.recipeData;

        recipeName.text = GameLocalization.GetResourceType(_recipeData.resourceType);
        recipeIcon.sprite = _recipeData.recipeIcon;
        _ingredients = _recipeData.ingredients;
        _produceMaxAmount = _activeRecipe.maxProductionLimit;

        UpdateCurrentAmount();
        UpdateUI();

        foreach (ResourceCost ingredient in _ingredients) {
            GameObject newCellObject = Instantiate(recipeCellPrefab, contentParent);
            ResourceInfoCell newCell = newCellObject.GetComponent<ResourceInfoCell>();

            if (newCell != null) {
                newCell.SetInfo(ingredient.resourceType, ingredient.amount, false);
            }
        }

        foreach (Transform child in contentParent) {
            ResourceInfoCell cell = child.GetComponent<ResourceInfoCell>();
            if (cell != null) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);

        _tutorialID = "processor_" + _recipeData.resourceType.ToString();
        if (!string.IsNullOrEmpty(_tutorialID) && glowMaterial != null && TutorialManager.Instance != null) {
            TutorialManager.Instance.RegisterRuntimeUI(_tutorialID, gameObject, glowMaterial);
        }
    }

    private void HandleResourceChange(ResourceType type, int newAmount)
    {
        if (_recipeData != null && type == _recipeData.resourceType) {
            _currentStorageAmount = newAmount;
            UpdateUI();

            _activeRecipe.SetProductionLimit(_produceMaxAmount);
        }
    }

    private void UpdateCurrentAmount()
    {
        if (_recipeData == null) return;
        _currentStorageAmount = ResourceManager.Instance.GetResourceAmount(_recipeData.resourceType);
    }

    private void UpdateUI()
    {
        produceInfoText.text = $"{_currentStorageAmount} / {_produceMaxAmount}";
    }

    private int GetAmountChange()
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
            return 100;
        }
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) {
            return 10;
        }
        return 1;
    }

    public void OnPlusBtnClick()
    {
        float currentTime = Time.unscaledTime;
        if (currentTime - _lastButtonClickTime < ButtonClickCooldown) {
            return;
        }
        _lastButtonClickTime = currentTime;

        int amountToAdd = GetAmountChange();
        long increasedAmount = (long)_produceMaxAmount + amountToAdd;
        int newAmount = increasedAmount > int.MaxValue ? int.MaxValue : (int)increasedAmount;

        if (_produceMaxAmount != newAmount) {
            _produceMaxAmount = newAmount;
            UpdateUI();

            _activeRecipe.SetProductionLimit(_produceMaxAmount);
        }

        if (!plusButtonSound.IsNull) {
            RuntimeManager.PlayOneShot(plusButtonSound);
        }
    }

    public void OnMinusBtnClick()
    {
        float currentTime = Time.unscaledTime;
        if (currentTime - _lastButtonClickTime < ButtonClickCooldown) {
            return;
        }
        _lastButtonClickTime = currentTime;

        int amountToSubtract = GetAmountChange();
        int newAmount = Mathf.Max(_produceMaxAmount - amountToSubtract, 0);

        if (_produceMaxAmount != newAmount) {
            _produceMaxAmount = newAmount;
            UpdateUI();

            _activeRecipe.SetProductionLimit(_produceMaxAmount);
        }

        if (!minusButtonSound.IsNull) {
            RuntimeManager.PlayOneShot(minusButtonSound);
        }
    }
}
