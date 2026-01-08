using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ModuleInventoryCell : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text moduleNameText;
    
    private Module _module;
    private BaseInventorySystem _baseInventorySystem;
    private bool _isEmpty = true;
    
    public Module Module => _module;
    public bool IsEmpty() => _isEmpty;
    
    public void Initialize(BaseInventorySystem baseInventorySystem)
    {
        _baseInventorySystem = baseInventorySystem;
        Clear();
    }
    
    public void SetModule(Module module)
    {
        _module = module;
        _isEmpty = false;
        UpdateUI();
    }
    
    public void Clear()
    {
        _module = null;
        _isEmpty = true;
        
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
        
        if (moduleNameText != null)
        {
            moduleNameText.text = "";
        }
    }
    
    private void UpdateUI()
    {
        if (_module == null) return;
        
        if (iconImage != null)
        {
            iconImage.sprite = _module.moduleIcon;
            iconImage.enabled = _module.moduleIcon != null;
        }
        
        if (moduleNameText != null)
        {
            moduleNameText.text = _module.moduleName;
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isEmpty || _baseInventorySystem == null || _module == null)
        {
            return;
        }
        
        // TODO: Show module details or allow removal/transfer to seed core
        // For now, just log
        Debug.Log($"Clicked module: {_module.moduleName}");
    }
}

