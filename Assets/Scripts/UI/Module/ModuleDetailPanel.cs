using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModuleDetailPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject ingredientCellPrefab;
    [SerializeField] private TMP_Text moduleNameText;
    [SerializeField] private TMP_Text moduleDescriptionText;
    [SerializeField] private TMP_Text moduleTypeText;
    [SerializeField] private TMP_Text requiredResourceText;
    [SerializeField] private TMP_Text produceButtonText;
    [SerializeField] private RectTransform resourcePanel;
    [SerializeField] private RectTransform ingredientsParent;
    [SerializeField] private Button produceButton;

    [Header("Text Settings")]
    [SerializeField] private string produceText;
    [SerializeField] private string notProducableText;
    
    private ModuleRecipe _currentRecipe;
    private ModuleStation _station;

    private void Awake()
    {
        RefreshCraftButtonLocalizedStrings();
    }

    public void ApplyLocalizationRefresh()
    {
        RefreshCraftButtonLocalizedStrings();
        if (requiredResourceText != null && requiredResourceText.gameObject.activeSelf)
        {
            requiredResourceText.text = GameLocalization.GetOrDefault("UI_Common", "base.requiredResources",
                "필요한 자원");
        }

        if (_station != null && _currentRecipe != null)
        {
            StartCoroutine(UpdateUI());
            UpdateProduceButton();
        }
    }

    private void RefreshCraftButtonLocalizedStrings()
    {
        produceText = GameLocalization.GetOrDefault("UI_Common", "module.buttonCraft", "제작");
        notProducableText = GameLocalization.GetOrDefault("UI_Common", "module.notCraftable", "제작 불가능");
    }
    
    public void Initialize()
    {
        if (produceButton != null)
        {
            produceButton.onClick.RemoveAllListeners();
            produceButton.onClick.AddListener(OnProduceButtonClicked);
        }
        
        HidePanel();
    }

    private void ShowModuleDetail(ModuleRecipe recipe, ModuleStation station)
    {
        _currentRecipe = recipe;
        _station = station;

        if (requiredResourceText != null)
        {
            requiredResourceText.text = GameLocalization.GetOrDefault("UI_Common", "base.requiredResources",
                "필요한 자원");
        }
        
        SetupIngredients();
        StartCoroutine(UpdateUI());
        UpdateProduceButton();

        if (requiredResourceText != null)
        {
            requiredResourceText.gameObject.SetActive(true);
        }

        gameObject.SetActive(true);
    }
    
    public void ShowInfo(ModuleRecipe recipe, ModuleStation station)
    {
        ShowModuleDetail(recipe, station);
    }
    
    public void ClearInfo()
    {
        _currentRecipe = null;
        _station = null;
        
       moduleNameText.text = "";
       moduleDescriptionText.text = "";
       moduleTypeText.text = "";
       requiredResourceText.gameObject.SetActive(false);
       
       foreach (Transform child in ingredientsParent.gameObject.transform)
       {
           Destroy(child.gameObject);
       }
        
        produceButton.gameObject.SetActive(false);
        produceButtonText.text = "";
    }

    public void ShowPanel()
    {
        gameObject.SetActive(true);
        ClearInfo();
    }
    
    public void HidePanel()
    {
        gameObject.SetActive(false);
        ClearInfo();
    }
    
    private IEnumerator UpdateUI()
    {
        moduleNameText.text = _currentRecipe.GetDisplayName();
        moduleDescriptionText.text = _currentRecipe.GetDescription();
        string localizedType = GameLocalization.GetModuleType(_currentRecipe.moduleType);
        moduleTypeText.text = GameLocalization.GetOrDefault("UI_Common", "label.typeFormat", "타입 : {0}", localizedType);

        yield return CoroutineCache.GetWaitForEndOfFrame();
        LayoutRebuilder.ForceRebuildLayoutImmediate(resourcePanel);
    }
    
    private void SetupIngredients()
    {
        if (ingredientsParent == null || ingredientCellPrefab == null || _currentRecipe == null)
        {
            return;
        }
        
        foreach (Transform child in ingredientsParent.transform)
        {
            Destroy(child.gameObject);
        }
        
        foreach (ResourceCost ingredient in _currentRecipe.ingredients)
        {
            GameObject cellObj = Instantiate(ingredientCellPrefab, ingredientsParent.transform);
            BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();
            if (cell != null)
            {
                cell.SetResource(ingredient.resourceType, ingredient.amount);
            }
        }
    }
    
    private void UpdateProduceButton()
    {
        if (produceButton == null || _station == null || _currentRecipe == null) return;
        
        produceButton.gameObject.SetActive(true);
        bool canCraft = _station.CanCraftModule(_currentRecipe);
        produceButton.interactable = canCraft;
        
        if (produceButtonText != null)
        {
            produceButtonText.text = canCraft ? produceText : notProducableText;
        }
    }
    
    private void OnProduceButtonClicked()
    {
        if (_station == null || _currentRecipe == null)
        {
            return;
        }
        
        if (_station.CraftModule(_currentRecipe))
        {
            UpdateProduceButton();
            SetupIngredients();
            
            BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
            if (inventorySystem != null && inventorySystem.GetInventoryPanel() != null && inventorySystem.GetInventoryPanel().activeSelf)
            {
                inventorySystem.RefreshModulesOnly();
            }
            
            CoreCustomUIManager coreCustomUIManager = FindFirstObjectByType<CoreCustomUIManager>();
            if (coreCustomUIManager != null)
            {
                coreCustomUIManager.RefreshModuleSelectionGrid();
            }
        }
        else
        {
            Debug.LogWarning($"Failed to produce module {_currentRecipe.moduleName}");
        }
    }
    
    private void Update()
    {
        if (Input.GetMouseButtonUp(1))
        {
            if (gameObject.activeSelf)
            {
                ClearInfo();
            }
            return;
        }
        
        if (_station != null && _currentRecipe != null)
        {
            UpdateProduceButton();
        }
    }
}

