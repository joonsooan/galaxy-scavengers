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
    private ModuleStationUIManager _uiManager;
    
    public void Initialize(ModuleStationUIManager uiManager)
    {
        _uiManager = uiManager;
        
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
        
        if (requiredResourceText != null)
        {
            requiredResourceText.gameObject.SetActive(false);
        }
        
        if (ingredientsParent != null)
        {
            foreach (Transform child in ingredientsParent.gameObject.transform)
            {
                Destroy(child.gameObject);
            }
        }
        
        if (produceButton != null)
        {
            produceButton.gameObject.SetActive(false);
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
    
    private IEnumerator UpdateUI()
    {
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
            string koreanType = GetKoreanModuleType(_currentRecipe.moduleType);
            moduleTypeText.text = $"타입 : {koreanType}";
        }

        yield return new WaitForEndOfFrame();
        LayoutRebuilder.ForceRebuildLayoutImmediate(resourcePanel);
    }
    
    private string GetKoreanModuleType(ModuleType moduleType)
    {
        return moduleType switch
        {
            ModuleType.Default => "기본",
            ModuleType.Power => "전력",
            ModuleType.Defense => "방어",
            ModuleType.Offense => "공격",
            ModuleType.Utility => "유틸리티",
            ModuleType.Production => "생산",
            ModuleType.Research => "연구",
            _ => moduleType.ToString()
        };
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
            _uiManager?.OnModuleCrafted();
            UpdateProduceButton();
            SetupIngredients();
            
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

