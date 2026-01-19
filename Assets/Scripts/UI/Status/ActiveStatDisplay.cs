using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActiveStatDisplay : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private Transform iconContainer;
    [SerializeField] private GameObject statIconPrefab;
    [SerializeField] private float iconSpacing = 50f;
    [SerializeField] private Vector2 iconSize = new Vector2(40f, 40f);

    private readonly Dictionary<ModuleStatType, GameObject> _activeIcons = new Dictionary<ModuleStatType, GameObject>();

    private void Start()
    {
        if (iconContainer == null) {
            iconContainer = transform;
        }

        if (ModuleEffectManager.Instance != null) {
            UpdateDisplay();
        }
    }

    private void OnEnable()
    {
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        ClearIcons();
    }

    public void UpdateDisplay()
    {
        if (ModuleEffectManager.Instance == null) return;

        ClearIcons();

        IReadOnlyDictionary<ModuleStatType, float> activeModifiers = ModuleEffectManager.Instance.ActiveStatModifiers;
        if (activeModifiers == null || activeModifiers.Count == 0) return;

        int index = 0;
        foreach (KeyValuePair<ModuleStatType, float> kvp in activeModifiers) {
            if (kvp.Value <= 0f) continue;

            CreateStatIcon(kvp.Key, kvp.Value, index);
            index++;
        }
    }

    private void CreateStatIcon(ModuleStatType statType, float modifierValue, int index)
    {
        if (statIconPrefab == null || iconContainer == null) return;

        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        Sprite icon = null;

        if (inventoryManager != null) {
            foreach (Module module in inventoryManager.GetAllModules()) {
                if (module.effectData == null) continue;

                foreach (ModuleStatModifier modifier in module.effectData.StatModifiers) {
                    if (modifier.statType == statType && modifier.statIcon != null) {
                        icon = modifier.statIcon;
                        break;
                    }
                }

                if (icon != null) break;
            }
        }

        if (icon == null) return;

        GameObject iconObj = Instantiate(statIconPrefab, iconContainer);
        iconObj.name = $"StatIcon_{statType}";

        Image image = iconObj.GetComponent<Image>();
        if (image != null) {
            image.sprite = icon;
        }

        RectTransform rectTransform = iconObj.GetComponent<RectTransform>();
        if (rectTransform != null) {
            rectTransform.sizeDelta = iconSize;
            rectTransform.anchoredPosition = new Vector2(0, -index * iconSpacing);
        }

        TextMeshProUGUI tooltip = iconObj.GetComponentInChildren<TextMeshProUGUI>();
        if (tooltip != null) {
            tooltip.text = $"+{modifierValue * 100f:F0}%";
        }

        _activeIcons[statType] = iconObj;
    }

    private void ClearIcons()
    {
        foreach (GameObject icon in _activeIcons.Values) {
            if (icon != null) {
                Destroy(icon);
            }
        }
        _activeIcons.Clear();
    }
}
