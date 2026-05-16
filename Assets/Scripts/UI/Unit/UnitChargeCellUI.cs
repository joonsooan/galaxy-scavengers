using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class UnitChargeCellUI : MonoBehaviour
{
    [Header("Unit")]
    [SerializeField] private Image unitIconImage;
    [SerializeField] private TMP_Text unitNameText;

    [Header("Status (Extractor-style)")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject statusProblemIcon;
    [SerializeField] private GameObject statusOkIcon;
    [SerializeField] private string textPowerEmpty = "전력 부족";
    [SerializeField] private string textNeedCharge = "충전 필요";
    [SerializeField] private string textGoingToCharge = "충전소 이동 중";
    [SerializeField] private string textQueued = "대기 중";
    [SerializeField] private string textCharging = "충전 중";
    [SerializeField] private string textOk = "정상";

    [Header("Charge display")]
    [SerializeField] private Slider chargeRatioSlider;
    [SerializeField] private TMP_Text currentMaxBatteryText;
    [Header("Focus unit")]
    [SerializeField] private Button focusUnitButton;

    [Header("Charge slider fill")]
    [SerializeField] private Color chargeSliderColorEmpty = new Color(0.92f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color chargeSliderColorNeedCharge = new Color(1f, 0.75f, 0.2f, 1f);
    [SerializeField] private Color chargeSliderColorCharging = new Color(0.22f, 0.65f, 1f, 1f);
    [SerializeField] private Color chargeSliderColorFull = new Color(0.25f, 0.78f, 0.35f, 1f);

    private UnitBase _unit;
    private UnitBattery _battery;

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnDestroy()
    {
        UnbindBattery();
    }

    public void Bind(UnitBase unit)
    {
        UnbindBattery();
        _unit = unit;
        _battery = unit != null ? unit.GetComponent<UnitBattery>() : null;
        RefreshLocalizedStatusStrings();

        if (_battery != null)
        {
            _battery.OnBatteryChanged += OnBatteryChanged;
        }

        WireFocusButton(true);

        RefreshStaticVisuals();
        RefreshDynamicVisuals();
    }

    private void RefreshLocalizedStatusStrings()
    {
        textPowerEmpty = GameLocalization.GetOrDefault("UI_Common", "status.powerInsufficient", textPowerEmpty);
        textNeedCharge = GameLocalization.GetOrDefault("UI_Common", "status.needCharge", textNeedCharge);
        textGoingToCharge = GameLocalization.GetOrDefault("UI_Common", "status.movingToChargingStation", textGoingToCharge);
        textQueued = GameLocalization.GetOrDefault("UI_Common", "status.queued", textQueued);
        textCharging = GameLocalization.GetOrDefault("UI_Common", "status.charging", textCharging);
        textOk = GameLocalization.GetOrDefault("UI_Common", "status.normal", textOk);
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        RefreshLocalizedStatusStrings();
        RefreshStaticVisuals();
        RefreshDynamicVisuals();
    }

    private void UnbindBattery()
    {
        WireFocusButton(false);

        if (_battery != null)
        {
            _battery.OnBatteryChanged -= OnBatteryChanged;
        }

        _unit = null;
        _battery = null;
    }

    private void WireFocusButton(bool add)
    {
        if (focusUnitButton == null)
        {
            return;
        }

        focusUnitButton.onClick.RemoveListener(OnFocusUnitClicked);
        if (add)
        {
            focusUnitButton.onClick.AddListener(OnFocusUnitClicked);
        }
    }

    private void OnFocusUnitClicked()
    {
        if (_unit == null || !_unit.gameObject.activeInHierarchy)
        {
            return;
        }

        if (BuildingHoverManager.Instance != null)
        {
            BuildingHoverManager.Instance.LockUnitInfo(_unit);
        }
        else if (UnitInfoPanel.Instance != null)
        {
            UnitInfoPanel.Instance.PreviewInfo(_unit);
        }

        MainControlPanel mainPanel = FindFirstObjectByType<MainControlPanel>(FindObjectsInactive.Include);
        if (mainPanel != null)
        {
            mainPanel.CloseUnitManagementPanel();
        }

        CameraTargetController cam = FindFirstObjectByType<CameraTargetController>();
        if (cam != null)
        {
            cam.SetFollowTargetImmediate(_unit.transform);
        }

        TargetBracketEffect.Show(_unit.transform);
    }

    private void OnBatteryChanged()
    {
        RefreshDynamicVisuals();
    }

    private void RefreshStaticVisuals()
    {
        if (unitNameText != null)
        {
            unitNameText.text = _unit != null && _unit.unitData != null ? _unit.unitData.GetDisplayName() : string.Empty;
        }

        if (unitIconImage != null)
        {
            unitIconImage.sprite = _unit != null && _unit.unitData != null ? _unit.unitData.unitIcon : null;
            unitIconImage.enabled = unitIconImage.sprite != null;
        }
    }

    private void RefreshDynamicVisuals()
    {
        if (_battery == null)
        {
            return;
        }

        float ratio = _battery.NormalizedRatio;
        if (chargeRatioSlider != null)
        {
            chargeRatioSlider.minValue = 0f;
            chargeRatioSlider.maxValue = 1f;
            chargeRatioSlider.SetValueWithoutNotify(ratio);
            BatterySliderFillColorUtility.ApplyDiscreteFillColor(
                chargeRatioSlider,
                _battery,
                chargeSliderColorEmpty,
                chargeSliderColorNeedCharge,
                chargeSliderColorCharging,
                chargeSliderColorFull);
        }

        if (currentMaxBatteryText != null)
        {
            currentMaxBatteryText.text = BatteryAmountTextFormatter.Format(_battery.CurrentAmount, _battery.MaxAmount);
        }

        ApplyExtractorStyleStatus();
    }

    private void ApplyExtractorStyleStatus()
    {
        UnitBatteryStatusUiMapper.Result r = UnitBatteryStatusUiMapper.Map(
            _battery,
            textPowerEmpty,
            textNeedCharge,
            textGoingToCharge,
            textQueued,
            textCharging,
            textOk);

        if (statusText != null)
        {
            statusText.text = r.StatusText;
        }

        if (statusProblemIcon != null)
        {
            statusProblemIcon.SetActive(r.ShowProblemIcon);
        }

        if (statusOkIcon != null)
        {
            statusOkIcon.SetActive(r.ShowOkIcon);
        }
    }
}
