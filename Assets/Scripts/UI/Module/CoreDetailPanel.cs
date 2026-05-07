using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CoreDetailPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text moduleNameText;
    [SerializeField] private TMP_Text moduleEffectText;

    private CoreCustomizationManager _customizationManager;

    private void Awake()
    {
        _customizationManager = FindFirstObjectByType<CoreCustomizationManager>();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
        UpdateModuleEffects();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (_customizationManager != null) {
            _customizationManager.OnModuleSlotChanged += OnModuleSlotChanged;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (_customizationManager != null) {
            _customizationManager.OnModuleSlotChanged -= OnModuleSlotChanged;
        }
    }

    private void OnModuleSlotChanged(int slotIndex, Module module)
    {
        UpdateModuleEffects();
    }

    public void UpdateModuleEffects()
    {
        if (_customizationManager == null) {
            _customizationManager = FindFirstObjectByType<CoreCustomizationManager>();
        }

        List<Module> activeModules = _customizationManager.GetActiveModules();

        if (activeModules == null || activeModules.Count == 0) {
            moduleNameText.text = GameLocalization.GetOrDefault("UI_Common", "module.noneEquipped", "장착된 모듈이 없습니다.");
            moduleEffectText.text = "";
            return;
        }

        foreach (Module module in activeModules) {
            if (module == null) continue;
            if (module.effectData != null) {
                moduleNameText.text = module.moduleName;
                moduleEffectText.text = module.moduleDescription;
            }
        }

        if (moduleEffectText.text.Length == 0) {
            moduleEffectText.text = GameLocalization.GetOrDefault("UI_Common", "module.noActiveEffects", "활성화된 모듈 효과가 없습니다.");
        }
    }
    
    public void ClearInfo()
    {
        if (moduleNameText != null)
        {
            moduleNameText.text = GameLocalization.GetOrDefault("UI_Common", "module.noneEquipped", "장착된 모듈이 없습니다.");
        }
        if (moduleEffectText != null)
        {
            moduleEffectText.text = "";
        }
    }
}
