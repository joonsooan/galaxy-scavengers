using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ModuleGridCell : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image moduleIcon;
    [SerializeField] private TMP_Text moduleNameText;
    
    private ModuleRecipe _recipe;
    private ModuleStationUIManager _uiManager;
    
    public ModuleRecipe Recipe => _recipe;
    
    public void Initialize(ModuleRecipe recipe, ModuleStationUIManager uiManager)
    {
        _recipe = recipe;
        _uiManager = uiManager;
        
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        if (_recipe == null) return;
        
        if (moduleIcon != null)
        {
            moduleIcon.sprite = _recipe.moduleIcon;
            moduleIcon.enabled = _recipe.moduleIcon != null;
        }
        
        if (moduleNameText != null)
        {
            moduleNameText.text = _recipe.GetDisplayName();
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_recipe != null && _uiManager != null)
        {
            _uiManager.ShowModuleDetail(_recipe);
        }
    }
}

