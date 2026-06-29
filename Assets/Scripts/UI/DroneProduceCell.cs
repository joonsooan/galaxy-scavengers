using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DroneProduceCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private const int MaxProduceAmount = 999;
    private const float ButtonClickCooldown = 0.1f;
    [Header("UI References")]
    [SerializeField] private Image unitIcon;
    [SerializeField] private TMP_Text unitName;
    [SerializeField] private TMP_Text productionTime;
    [SerializeField] private TMP_Text produceInfoText;
    [SerializeField] private GameObject resourceCellPrefab;
    [SerializeField] private RectTransform contentParent;

    [Header("Sound Effects")]
    [SerializeField] private EventReference plusButtonSound;
    [SerializeField] private EventReference minusButtonSound;

    [Header("Tutorial Settings")]
    [SerializeField] private Material glowMaterial;
    private int _currentProducedCount;

    private MainStructure _mainStructure;

    private float _lastButtonClickTime;
    private int _targetCount;
    private string _tutorialID;
    private int _unitIndex;
    private UnitData _unitData;
    private DroneProduceUIManager _uiManager;

    private void OnDisable()
    {
        MainStructure.OnUnitTargetChanged -= HandleUnitTargetChanged;
    }

    public void Initialize(UnitData unitData, MainStructure mainStructure, int unitIndex, DroneProduceUIManager uiManager)
    {
        MainStructure.OnUnitTargetChanged -= HandleUnitTargetChanged;

        _mainStructure = mainStructure;
        _unitIndex = unitIndex;
        _unitData = unitData;
        _uiManager = uiManager;

        if (unitData == null) {
            Debug.LogError("UnitData is null");
            return;
        }

        _tutorialID = !string.IsNullOrEmpty(unitData.tutorialKey) ? unitData.tutorialKey : unitData.name;

        if (unitName != null) {
            unitName.text = unitData.GetDisplayName();
        }

        if (productionTime != null) {
            productionTime.text = GameLocalization.GetOrDefault("UI_Common", "label.timeSecondsFormat", "Time: {0}s", unitData.productionTime);
        }

        if (unitIcon != null && unitData.unitIcon != null) {
            unitIcon.sprite = unitData.unitIcon;
        }

        if (unitData.productionCosts != null && resourceCellPrefab != null && contentParent != null) {
            foreach (Transform child in contentParent) {
                Destroy(child.gameObject);
            }

            foreach (ResourceCost cost in unitData.productionCosts) {
                GameObject newCellObject = Instantiate(resourceCellPrefab, contentParent);
                ResourceInfoCell newCell = newCellObject.GetComponent<ResourceInfoCell>();

                if (newCell != null) {
                    newCell.SetInfo(cost.resourceType, cost.amount, false);
                }
            }

            foreach (Transform child in contentParent) {
                ResourceInfoCell cell = child.GetComponent<ResourceInfoCell>();
                if (cell != null) {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
        }

        _currentProducedCount = _mainStructure.GetCurrentUnitCount(_unitIndex);
        _targetCount = _mainStructure.GetTargetUnitCount(_unitIndex);
        UpdateUI();
        if (!string.IsNullOrEmpty(_tutorialID) && glowMaterial != null && TutorialManager.Instance != null) {
            TutorialManager.Instance.RegisterRuntimeUI(_tutorialID, gameObject, glowMaterial);
        }

        MainStructure.OnUnitTargetChanged += HandleUnitTargetChanged;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_uiManager != null)
        {
            _uiManager.OnProduceCellHover(_unitData);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_uiManager != null)
        {
            _uiManager.OnProduceCellHoverExit(_unitData);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_uiManager != null)
        {
            _uiManager.OnProduceCellClicked(_unitData);
        }
    }

    private void HandleUnitTargetChanged(int unitIndex, int currentCount, int targetCount)
    {
        if (unitIndex == _unitIndex) {
            _currentProducedCount = currentCount;
            _targetCount = targetCount;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (produceInfoText != null) {
            produceInfoText.text = $"{_currentProducedCount} / {_targetCount}";
        }
    }

    private int GetAmountChange()
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
            return 100;
        }
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) {
            return 10;
        }
        return 1;
    }

    public void OnPlusBtnClick()
    {
        if (_mainStructure == null) {
            return;
        }


        float currentTime = Time.unscaledTime;
        if (currentTime - _lastButtonClickTime < ButtonClickCooldown) {
            return;
        }
        _lastButtonClickTime = currentTime;

        int amountToAdd = GetAmountChange();
        int newTarget = Mathf.Min(_targetCount + amountToAdd, MaxProduceAmount);

        _mainStructure.SetTargetUnitCount(_unitIndex, newTarget);

        if (!plusButtonSound.IsNull) {
            RuntimeManager.PlayOneShot(plusButtonSound);
        }
    }

    public void OnMinusBtnClick()
    {
        if (_mainStructure == null) {
            return;
        }


        float currentTime = Time.unscaledTime;
        if (currentTime - _lastButtonClickTime < ButtonClickCooldown) {
            return;
        }
        _lastButtonClickTime = currentTime;

        int amountToSubtract = GetAmountChange();
        int newTarget = Mathf.Max(_targetCount - amountToSubtract, 0);

        _mainStructure.SetTargetUnitCount(_unitIndex, newTarget);

        if (!minusButtonSound.IsNull) {
            RuntimeManager.PlayOneShot(minusButtonSound);
        }
    }
}
