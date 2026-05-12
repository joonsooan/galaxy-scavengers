using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ModuleInventoryCell : MonoBehaviour, IPointerClickHandler, IBaseInventoryCell
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text moduleNameText;
    
    private Module _module;
    private BaseInventorySystem _baseInventorySystem;
    private CoreCustomUIManager _coreCustomUIManager;
    private bool _isEmpty = true;
    
    public Module Module => _module;
    public bool IsEmpty() => _isEmpty;
    
    public void Initialize(BaseInventorySystem baseInventorySystem)
    {
        _baseInventorySystem = baseInventorySystem;
        _coreCustomUIManager = null;
        Clear();
    }

    public void Initialize(CoreCustomUIManager coreCustomUIManager)
    {
        _coreCustomUIManager = coreCustomUIManager;
        _baseInventorySystem = null;
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
            moduleNameText.text = _module.GetDisplayName();
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isEmpty || _module == null)
        {
            return;
        }
        
        if (_coreCustomUIManager != null) {
            // Only allow module placement if core custom panel is open
            _coreCustomUIManager.OnModuleCellClicked(_module);
        } else if (_baseInventorySystem != null) {
            // Modules in base inventory system should not be placeable
            // Only allow if core custom panel is explicitly open
            CoreCustomUIManager customUIManager = FindFirstObjectByType<CoreCustomUIManager>();
            if (customUIManager != null && customUIManager.IsPanelOpen()) {
                customUIManager.OnModuleCellClicked(_module);
            } else {
                // Module clicked in base inventory system - do nothing or show info
                Debug.Log($"Clicked module in base inventory: {_module.moduleName}");
            }
        }
    }
}

