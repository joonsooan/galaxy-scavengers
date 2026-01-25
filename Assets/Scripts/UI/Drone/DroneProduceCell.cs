using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FMODUnity;

public class DroneProduceCell : MonoBehaviour
{
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

    private const int MaxProduceAmount = 999;

    private DroneHub _droneHub;
    private int _unitIndex;
    private int _currentProducedCount;
    private int _targetCount;
    
    private float _lastButtonClickTime;
    private const float ButtonClickCooldown = 0.1f;

    public void Initialize(UnitData unitData, DroneHub droneHub, int unitIndex)
    {
        DroneHub.OnUnitTargetChanged -= HandleUnitTargetChanged;
        
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
                    newCell.SetInfo(cost.resourceType, cost.amount, false);
                }
            }
            
            foreach (Transform child in contentParent)
            {
                ResourceInfoCell cell = child.GetComponent<ResourceInfoCell>();
                if (cell != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
                }
            }
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
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
        
        float currentTime = Time.unscaledTime;
        if (currentTime - _lastButtonClickTime < ButtonClickCooldown)
        {
            return;
        }
        _lastButtonClickTime = currentTime;

        int amountToAdd = GetAmountChange();
        int newTarget = Mathf.Min(_targetCount + amountToAdd, MaxProduceAmount);
        
        _droneHub.SetTargetUnitCount(_unitIndex, newTarget);
        
        if (!plusButtonSound.IsNull)
        {
            RuntimeManager.PlayOneShot(plusButtonSound);
        }
    }

    public void OnMinusBtnClick()
    {
        if (_droneHub == null) return;
        
        float currentTime = Time.unscaledTime;
        if (currentTime - _lastButtonClickTime < ButtonClickCooldown)
        {
            return;
        }
        _lastButtonClickTime = currentTime;

        int amountToSubtract = GetAmountChange();
        int newTarget = Mathf.Max(_targetCount - amountToSubtract, 0);
        
        _droneHub.SetTargetUnitCount(_unitIndex, newTarget);
        
        if (!minusButtonSound.IsNull)
        {
            RuntimeManager.PlayOneShot(minusButtonSound);
        }
    }
}

