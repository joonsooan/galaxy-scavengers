using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text unitNameText;
    [SerializeField] private TMP_Text unitDescText;
    [SerializeField] private TMP_Text unitHealthText;

    private UnitBase _currentUnit;

    public static UnitInfoPanel Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ClearAllInfo();
    }

    public void PreviewInfo(UnitBase unit)
    {
        if (unit == null)
        {
            CancelPreview();
            return;
        }
        _currentUnit = unit;
        RefreshUI();
        gameObject.SetActive(true);
    }

    public void CancelPreview()
    {
        _currentUnit = null;
        ClearUI();
        gameObject.SetActive(false);
    }

    public void ClearAllInfo()
    {
        _currentUnit = null;
        ClearUI();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_currentUnit == null)
        {
            return;
        }
        if (!_currentUnit.gameObject.activeInHierarchy)
        {
            CancelPreview();
            return;
        }
        if (_currentUnit.CurrentHealth <= 0)
        {
            CancelPreview();
            return;
        }
        RefreshHealthText();
    }

    private void RefreshUI()
    {
        if (_currentUnit == null)
        {
            ClearUI();
            return;
        }
        if (_currentUnit.unitData != null)
        {
            if (unitNameText != null)
            {
                unitNameText.text = _currentUnit.unitData.unitName;
            }
            if (unitDescText != null)
            {
                unitDescText.text = _currentUnit.unitData.description;
            }
        }
        RefreshHealthText();
        RebuildLayout();
    }

    private void RefreshHealthText()
    {
        if (unitHealthText == null || _currentUnit == null)
        {
            return;
        }
        int current = _currentUnit.CurrentHealth;
        int max = _currentUnit.MaxHealth;
        unitHealthText.text = $"{current} / {max}";
    }

    private void ClearUI()
    {
        if (unitNameText != null)
        {
            unitNameText.text = string.Empty;
        }
        if (unitDescText != null)
        {
            unitDescText.text = string.Empty;
        }
        if (unitHealthText != null)
        {
            unitHealthText.text = string.Empty;
        }
    }

    private void RebuildLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (unitNameText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(unitNameText.rectTransform);
        if (unitDescText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(unitDescText.rectTransform);
        if (unitHealthText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(unitHealthText.rectTransform);

        if (unitNameText != null && unitNameText.transform.parent is RectTransform headerRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(headerRect);

        RectTransform panelRect = GetComponent<RectTransform>();
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        if (panelRect != null && panelRect.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
    }
}
