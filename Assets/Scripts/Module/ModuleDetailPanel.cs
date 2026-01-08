using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModuleDetailPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image moduleIcon;
    [SerializeField] private TMP_Text moduleNameText;
    [SerializeField] private TMP_Text moduleDescriptionText;
    [SerializeField] private TMP_Text moduleTypeText;
    [SerializeField] private Transform ingredientsParent;
    [SerializeField] private GameObject ingredientCellPrefab;
    [SerializeField] private Button produceButton;
    [SerializeField] private TMP_Text produceButtonText;
    [SerializeField] private Button closeButton;
    
    private ModuleRecipe _currentRecipe;
    private ModuleStation _station;
    private ModuleStationUIManager _uiManager;
    
    public void Initialize(ModuleStationUIManager uiManager)
    {
        _uiManager = uiManager;
        
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            if (_uiManager != null)
            {
                // Close the entire module panel (station + detail) when the close button is pressed
                closeButton.onClick.AddListener(_uiManager.OnCloseButtonClicked);
            }
            else
            {
                // Fallback: just hide this detail panel
                closeButton.onClick.AddListener(HidePanel);
            }
        }
        
        if (produceButton != null)
        {
            produceButton.onClick.RemoveAllListeners();
            produceButton.onClick.AddListener(OnProduceButtonClicked);
        }
        
        HidePanel();
    }
    
    public void ShowModuleDetail(ModuleRecipe recipe, ModuleStation station)
    {
        _currentRecipe = recipe;
        _station = station;
        
        if (recipe == null) return;
        
        UpdateUI();
        SetupIngredients();
        UpdateProduceButton();
        
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
        
        if (moduleIcon != null)
        {
            moduleIcon.sprite = null;
            moduleIcon.enabled = false;
        }
        
        if (moduleNameText != null)
        {
            moduleNameText.text = "";
        }
        
        if (moduleDescriptionText != null)
        {
            moduleDescriptionText.text = "";
        }
        
        if (moduleTypeText != null)
        {
            moduleTypeText.text = "";
        }
        
        if (ingredientsParent != null)
        {
            foreach (Transform child in ingredientsParent)
            {
                Destroy(child.gameObject);
            }
        }
        
        if (produceButton != null)
        {
            produceButton.interactable = false;
        }
        
        if (produceButtonText != null)
        {
            produceButtonText.text = "";
        }
    }
    
    public void HidePanel()
    {
        gameObject.SetActive(false);
        ClearInfo();
    }
    
    private void UpdateUI()
    {
        if (_currentRecipe == null) return;
        
        if (moduleIcon != null)
        {
            moduleIcon.sprite = _currentRecipe.moduleIcon;
            moduleIcon.enabled = _currentRecipe.moduleIcon != null;
        }
        
        if (moduleNameText != null)
        {
            moduleNameText.text = _currentRecipe.moduleName;
        }
        
        if (moduleDescriptionText != null)
        {
            moduleDescriptionText.text = _currentRecipe.moduleDescription;
        }
        
        if (moduleTypeText != null)
        {
            moduleTypeText.text = $"Type: {_currentRecipe.moduleType}";
        }
    }
    
    private void SetupIngredients()
    {
        if (ingredientsParent == null || ingredientCellPrefab == null || _currentRecipe == null)
        {
            return;
        }
        
        foreach (Transform child in ingredientsParent)
        {
            Destroy(child.gameObject);
        }
        
        foreach (ResourceCost ingredient in _currentRecipe.ingredients)
        {
            GameObject cellObj = Instantiate(ingredientCellPrefab, ingredientsParent);
            ResourceInfoCell cell = cellObj.GetComponent<ResourceInfoCell>();
            
            if (cell != null)
            {
                cell.SetInfo(ingredient.resourceType, ingredient.amount);
            }
        }
    }
    
    private void UpdateProduceButton()
    {
        if (produceButton == null || _station == null || _currentRecipe == null) return;
        
        bool canCraft = _station.CanCraftModule(_currentRecipe);
        produceButton.interactable = canCraft;
        
        if (produceButtonText != null)
        {
            produceButtonText.text = canCraft ? "Produce" : "Cannot Produce";
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
            _uiManager?.OnModuleCrafted();
            UpdateProduceButton();
            SetupIngredients();
            
            // Refresh only modules (preserves resources)
            // The OnModuleAdded event will also trigger RefreshModulesOnly(), but we call it here
            // to ensure immediate update if the panel is open
            BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
            if (inventorySystem != null && inventorySystem.GetInventoryPanel() != null && inventorySystem.GetInventoryPanel().activeSelf)
            {
                inventorySystem.RefreshModulesOnly();
            }
        }
        else
        {
            Debug.LogWarning($"Failed to produce module {_currentRecipe.moduleName}");
        }
    }
    
    private void Update()
    {
        if (_station != null && _currentRecipe != null)
        {
            UpdateProduceButton();
        }
    }
}

