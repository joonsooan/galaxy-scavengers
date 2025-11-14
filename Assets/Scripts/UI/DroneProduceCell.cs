using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DroneProduceCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image unitIcon;
    [SerializeField] private TMP_Text unitName;
    [SerializeField] private TMP_Text productionTime;
    [SerializeField] private TMP_Text produceInfoText;
    [SerializeField] private GameObject resourceCellPrefab;
    [SerializeField] private RectTransform contentParent;

    private const int MaxProduceAmount = 999;

    private UnitData _unitData;
    private DroneHub _droneHub;
    private int _unitIndex;
    private int _currentProducedCount;
    private int _targetCount;

    public void Initialize(UnitData unitData, DroneHub droneHub, int unitIndex)
    {
        _unitData = unitData;
        _droneHub = droneHub;
        _unitIndex = unitIndex;

        if (unitData == null)
        {
            Debug.LogError("UnitData is null");
            return;
        }

        if (unitName != null)
        {
            unitName.text = unitData.unitName;
        }

        if (productionTime != null)
        {
            productionTime.text = $"Time: {unitData.productionTime}s";
        }

        if (unitIcon != null && unitData.unitIcon != null)
        {
            unitIcon.sprite = unitData.unitIcon;
        }

        if (unitData.productionCosts != null && resourceCellPrefab != null && contentParent != null)
        {
            foreach (Transform child in contentParent)
            {
                Destroy(child.gameObject);
            }

            foreach (ResourceCost cost in unitData.productionCosts)
            {
                GameObject newCellObject = Instantiate(resourceCellPrefab, contentParent);
                ResourceInfoCell newCell = newCellObject.GetComponent<ResourceInfoCell>();

                if (newCell != null)
                {
                    newCell.resourceImage.sprite = GetResourceImage(cost.resourceType);
                    newCell.resourceAmount.text = cost.amount.ToString();
                }
            }
        }

        _currentProducedCount = _droneHub.GetCurrentUnitCount(_unitIndex);
        _targetCount = _droneHub.GetTargetUnitCount(_unitIndex);
        UpdateUI();

        DroneHub.OnUnitTargetChanged += HandleUnitTargetChanged;
    }

    private void OnDisable()
    {
        DroneHub.OnUnitTargetChanged -= HandleUnitTargetChanged;
    }

    private void HandleUnitTargetChanged(int unitIndex, int currentCount, int targetCount)
    {
        if (unitIndex == _unitIndex)
        {
            _currentProducedCount = currentCount;
            _targetCount = targetCount;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (produceInfoText != null)
        {
            produceInfoText.text = $"{_currentProducedCount} / {_targetCount}";
        }
    }

    private int GetAmountChange()
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            return 100;
        }
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            return 10;
        }
        return 1;
    }

    public void OnPlusBtnClick()
    {
        if (_droneHub == null) return;

        int amountToAdd = GetAmountChange();
        int newTarget = Mathf.Min(_targetCount + amountToAdd, MaxProduceAmount);
        
        _droneHub.SetTargetUnitCount(_unitIndex, newTarget);
    }

    public void OnMinusBtnClick()
    {
        if (_droneHub == null) return;

        int amountToSubtract = GetAmountChange();
        int newTarget = Mathf.Max(_targetCount - amountToSubtract, 0);
        
        _droneHub.SetTargetUnitCount(_unitIndex, newTarget);
    }

    private Sprite GetResourceImage(ResourceType type)
    {
        return ResourceManager.Instance?.GetResourceIcon(type);
    }
}

