using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text unitNameText;
    [SerializeField] private TMP_Text unitDescText;
    [SerializeField] private TMP_Text unitHealthText;

    public static UnitInfoPanel Instance { get; private set; }

    private UnitBase _currentUnit;
    private UnitData _previewUnitData;
    private RectTransform _rectTransform;
    private bool _isLayoutOverridden;
    private Vector2 _defaultAnchorMin;
    private Vector2 _defaultAnchorMax;
    private Vector2 _defaultPivot;
    private Vector2 _defaultAnchoredPosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform != null)
        {
            _defaultAnchorMin = _rectTransform.anchorMin;
            _defaultAnchorMax = _rectTransform.anchorMax;
            _defaultPivot = _rectTransform.pivot;
            _defaultAnchoredPosition = _rectTransform.anchoredPosition;
        }

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
        _previewUnitData = null;
        RefreshUI();
        gameObject.SetActive(true);
    }

    public void PreviewInfo(UnitData unitData)
    {
        if (unitData == null)
        {
            CancelPreview();
            return;
        }

        _currentUnit = null;
        _previewUnitData = unitData;
        RefreshUI();
        gameObject.SetActive(true);
    }

    public void CancelPreview()
    {
        _currentUnit = null;
        _previewUnitData = null;
        ClearUI();
        gameObject.SetActive(false);
    }

    public void ClearAllInfo()
    {
        _currentUnit = null;
        _previewUnitData = null;
        ClearUI();
        gameObject.SetActive(false);
    }

    public void ApplyFixedAnchorLayout(Vector2 anchor, Vector2 anchoredPosition)
    {
        if (_rectTransform == null)
        {
            return;
        }

        if (!_isLayoutOverridden)
        {
            _defaultAnchorMin = _rectTransform.anchorMin;
            _defaultAnchorMax = _rectTransform.anchorMax;
            _defaultPivot = _rectTransform.pivot;
            _defaultAnchoredPosition = _rectTransform.anchoredPosition;
            _isLayoutOverridden = true;
        }

        _rectTransform.anchorMin = anchor;
        _rectTransform.anchorMax = anchor;
        _rectTransform.pivot = anchor;
        _rectTransform.anchoredPosition = anchoredPosition;
    }

    public void RestoreDefaultLayout()
    {
        if (_rectTransform == null || !_isLayoutOverridden)
        {
            return;
        }

        _rectTransform.anchorMin = _defaultAnchorMin;
        _rectTransform.anchorMax = _defaultAnchorMax;
        _rectTransform.pivot = _defaultPivot;
        _rectTransform.anchoredPosition = _defaultAnchoredPosition;
        _isLayoutOverridden = false;
    }

    private void Update()
    {
        if (_previewUnitData != null)
        {
            return;
        }
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
        if (_previewUnitData != null)
        {
            if (unitNameText != null)
            {
                unitNameText.text = _previewUnitData.unitName;
            }
            if (unitDescText != null)
            {
                unitDescText.text = _previewUnitData.description;
            }
            if (unitHealthText != null)
            {
                int previewMaxHealth = GetPreviewMaxHealth(_previewUnitData);
                unitHealthText.text = $"체력 : {previewMaxHealth}";
            }
            RebuildLayout();
            return;
        }

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

    private static int GetPreviewMaxHealth(UnitData unitData)
    {
        if (unitData == null || unitData.unitPrefab == null)
        {
            return 0;
        }

        UnitBase unitBase = unitData.unitPrefab.GetComponent<UnitBase>();
        if (unitBase == null)
        {
            unitBase = unitData.unitPrefab.GetComponentInChildren<UnitBase>(true);
        }

        return unitBase != null ? unitBase.MaxHealth : 0;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
