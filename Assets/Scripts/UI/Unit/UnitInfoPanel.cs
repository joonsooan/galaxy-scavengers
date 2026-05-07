using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text unitNameText;
    [SerializeField] private TMP_Text unitDescText;
    [SerializeField] private TMP_Text unitHealthText;

    [Header("Internal battery (ally units)")]
    [SerializeField] private GameObject internalBatteryRow;
    [SerializeField] private Slider unitBatteryRatioSlider;
    [SerializeField] private TMP_Text unitBatteryAmountText;
    [SerializeField] private TMP_Text unitBatteryStatusText;
    [SerializeField] private GameObject unitBatteryStatusProblemIcon;
    [SerializeField] private GameObject unitBatteryStatusOkIcon;
    [SerializeField] private string unitBatteryTextPowerEmpty = "\uC804\uB825 \uBD80\uC871";
    [SerializeField] private string unitBatteryTextNeedCharge = "\uCDA9\uC804 \uD544\uC694";
    [SerializeField] private string unitBatteryTextGoingToCharge = "\uCDA9\uC804\uC18C \uC774\uB3D9 \uC911";
    [SerializeField] private string unitBatteryTextQueued = "\uB300\uAE30 \uC911";
    [SerializeField] private string unitBatteryTextCharging = "\uCDA9\uC804 \uC911";
    [SerializeField] private string unitBatteryTextOk = "\uC815\uC0C1";
    [Header("Battery slider fill")]
    [SerializeField] private Color unitBatterySliderColorEmpty = new Color(0.92f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color unitBatterySliderColorNeedCharge = new Color(1f, 0.75f, 0.2f, 1f);
    [SerializeField] private Color unitBatterySliderColorCharging = new Color(0.22f, 0.65f, 1f, 1f);
    [SerializeField] private Color unitBatterySliderColorFull = new Color(0.25f, 0.78f, 0.35f, 1f);

    public static UnitInfoPanel Instance { get; private set; }

    private UnitBase _currentUnit;
    private UnitData _previewUnitData;
    private RectTransform _rectTransform;
    private bool _isLayoutOverridden;
    private Vector2 _defaultAnchorMin;
    private Vector2 _defaultAnchorMax;
    private Vector2 _defaultPivot;
    private Vector2 _defaultAnchoredPosition;
    private UnitBattery _batteryEventSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureLayoutDefaultsInitialized();

        ClearAllInfo();
    }

    public void PreviewInfo(UnitBase unit)
    {
        if (unit == null)
        {
            CancelPreview();
            return;
        }
        UnbindBatteryListener();
        _currentUnit = unit;
        _previewUnitData = null;
        if (unit.unitData != null && unit.unitData.useInternalBattery)
        {
            _batteryEventSource = unit.GetComponent<UnitBattery>();
            if (_batteryEventSource != null)
            {
                _batteryEventSource.OnBatteryChanged += OnBatteryChangedForPanel;
            }
        }

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

        UnbindBatteryListener();
        _currentUnit = null;
        _previewUnitData = unitData;
        RefreshUI();
        gameObject.SetActive(true);
    }

    public void CancelPreview()
    {
        UnbindBatteryListener();
        _currentUnit = null;
        _previewUnitData = null;
        ClearUI();
        gameObject.SetActive(false);
    }

    public void ClearAllInfo()
    {
        UnbindBatteryListener();
        _currentUnit = null;
        _previewUnitData = null;
        ClearUI();
        gameObject.SetActive(false);
    }

    public void ApplyFixedAnchorLayout(Vector2 anchor, Vector2 anchoredPosition)
    {
        if (!EnsureLayoutDefaultsInitialized())
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
        if (!EnsureLayoutDefaultsInitialized() || !_isLayoutOverridden)
        {
            return;
        }

        _rectTransform.anchorMin = _defaultAnchorMin;
        _rectTransform.anchorMax = _defaultAnchorMax;
        _rectTransform.pivot = _defaultPivot;
        _rectTransform.anchoredPosition = _defaultAnchoredPosition;
        _isLayoutOverridden = false;
    }

    private bool EnsureLayoutDefaultsInitialized()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                return false;
            }
        }

        if (!_isLayoutOverridden)
        {
            _defaultAnchorMin = _rectTransform.anchorMin;
            _defaultAnchorMax = _rectTransform.anchorMax;
            _defaultPivot = _rectTransform.pivot;
            _defaultAnchoredPosition = _rectTransform.anchoredPosition;
        }

        return true;
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
        RefreshBatteryDisplay();
    }

    private static bool IsWorldPointerOverUnitCollider(UnitBase unit)
    {
        if (unit == null)
        {
            return false;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
        w.z = 0f;
        Collider2D[] hits = Physics2D.OverlapPointAll(w);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i];
            if (c == null)
            {
                continue;
            }

            if (c.GetComponentInParent<UnitBase>() == unit)
            {
                return true;
            }
        }

        return false;
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

            ApplyBatteryPreviewFromData(_previewUnitData);
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
        RefreshBatteryDisplay();
        RebuildLayout();
    }

    private void OnBatteryChangedForPanel()
    {
        RefreshBatteryDisplay();
    }

    private void UnbindBatteryListener()
    {
        if (_batteryEventSource != null)
        {
            _batteryEventSource.OnBatteryChanged -= OnBatteryChangedForPanel;
        }

        _batteryEventSource = null;
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

    private void RefreshBatteryDisplay()
    {
        if (_currentUnit != null && _currentUnit.unitData != null && _currentUnit.unitData.useInternalBattery)
        {
            UnitBattery b = _currentUnit.GetComponent<UnitBattery>();
            if (b != null)
            {
                if (unitBatteryRatioSlider != null)
                {
                    unitBatteryRatioSlider.minValue = 0f;
                    unitBatteryRatioSlider.maxValue = 1f;
                    unitBatteryRatioSlider.SetValueWithoutNotify(b.NormalizedRatio);
                    unitBatteryRatioSlider.interactable = false;
                    BatterySliderFillColorUtility.ApplyDiscreteFillColor(
                        unitBatteryRatioSlider,
                        b,
                        unitBatterySliderColorEmpty,
                        unitBatterySliderColorNeedCharge,
                        unitBatterySliderColorCharging,
                        unitBatterySliderColorFull);
                }

                if (unitBatteryAmountText != null)
                {
                    unitBatteryAmountText.text = BatteryAmountTextFormatter.Format(b.CurrentAmount, b.MaxAmount);
                }

                ApplyUnitBatteryStatusVisuals(b);
            }

            bool isLockedUnitInfo = BuildingHoverManager.Instance != null && BuildingHoverManager.Instance.IsUnitInfoLocked();
            bool showRow = b != null && (isLockedUnitInfo || IsWorldPointerOverUnitCollider(_currentUnit));
            if (internalBatteryRow != null)
            {
                internalBatteryRow.SetActive(showRow);
            }

            return;
        }

        if (_previewUnitData != null && _previewUnitData.useInternalBattery)
        {
            ApplyBatteryPreviewFromData(_previewUnitData);
            return;
        }

        if (internalBatteryRow != null)
        {
            internalBatteryRow.SetActive(false);
        }
    }

    private void ApplyBatteryPreviewFromData(UnitData data)
    {
        if (internalBatteryRow != null)
        {
            internalBatteryRow.SetActive(false);
        }

        if (data == null || !data.useInternalBattery)
        {
            ClearUnitBatteryStatusVisuals();
            return;
        }

        ClearUnitBatteryStatusVisuals();

        float max = Mathf.Max(0.01f, data.maxBattery);
        if (unitBatteryRatioSlider != null)
        {
            unitBatteryRatioSlider.minValue = 0f;
            unitBatteryRatioSlider.maxValue = 1f;
            unitBatteryRatioSlider.SetValueWithoutNotify(1f);
            unitBatteryRatioSlider.interactable = false;
            BatterySliderFillColorUtility.ApplyDiscreteByRatio(
                unitBatteryRatioSlider,
                1f,
                unitBatterySliderColorEmpty,
                unitBatterySliderColorFull);
        }

        if (unitBatteryAmountText != null)
        {
            unitBatteryAmountText.text = BatteryAmountTextFormatter.Format(max, max);
        }
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

        if (unitBatteryAmountText != null)
        {
            unitBatteryAmountText.text = string.Empty;
        }

        ClearUnitBatteryStatusVisuals();

        if (unitBatteryRatioSlider != null)
        {
            unitBatteryRatioSlider.SetValueWithoutNotify(0f);
            BatterySliderFillColorUtility.ApplyDiscreteByRatio(
                unitBatteryRatioSlider,
                0f,
                unitBatterySliderColorEmpty,
                unitBatterySliderColorFull);
        }

        if (internalBatteryRow != null)
        {
            internalBatteryRow.SetActive(false);
        }
    }

    private void ClearUnitBatteryStatusVisuals()
    {
        if (unitBatteryStatusText != null)
        {
            unitBatteryStatusText.text = string.Empty;
        }

        if (unitBatteryStatusProblemIcon != null)
        {
            unitBatteryStatusProblemIcon.SetActive(false);
        }

        if (unitBatteryStatusOkIcon != null)
        {
            unitBatteryStatusOkIcon.SetActive(false);
        }
    }

    private void ApplyUnitBatteryStatusVisuals(UnitBattery b)
    {
        UnitBatteryStatusUiMapper.Result r = UnitBatteryStatusUiMapper.Map(
            b,
            unitBatteryTextPowerEmpty,
            unitBatteryTextNeedCharge,
            unitBatteryTextGoingToCharge,
            unitBatteryTextQueued,
            unitBatteryTextCharging,
            unitBatteryTextOk);

        if (unitBatteryStatusText != null)
        {
            unitBatteryStatusText.text = r.StatusText;
        }

        if (unitBatteryStatusProblemIcon != null)
        {
            unitBatteryStatusProblemIcon.SetActive(r.ShowProblemIcon);
        }

        if (unitBatteryStatusOkIcon != null)
        {
            unitBatteryStatusOkIcon.SetActive(r.ShowOkIcon);
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
        if (unitBatteryAmountText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(unitBatteryAmountText.rectTransform);
        if (unitBatteryStatusText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(unitBatteryStatusText.rectTransform);

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

    public void WarmupFirstUse()
    {
        bool wasActive = gameObject.activeSelf;
        gameObject.SetActive(true);
        WarmTouchTmp(unitNameText);
        WarmTouchTmp(unitDescText);
        WarmTouchTmp(unitHealthText);
        WarmTouchTmp(unitBatteryAmountText);
        WarmTouchTmp(unitBatteryStatusText);
        RebuildLayout();
        Canvas.ForceUpdateCanvases();
        ClearAllInfo();
        if (wasActive)
        {
            gameObject.SetActive(true);
        }
    }

    private static void WarmTouchTmp(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }
        text.text = " ";
        text.ForceMeshUpdate(true);
        text.text = string.Empty;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
