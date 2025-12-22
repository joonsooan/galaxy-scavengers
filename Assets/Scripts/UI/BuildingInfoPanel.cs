using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text buildingName;
    [SerializeField] private GameObject resourcePanel;
    [SerializeField] private TMP_Text buildingDesc;

    private BuildingData _currentData;
    private bool _isFixed;

    public static BuildingInfoPanel Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ClearPanel();
    }

    public void OnMouseHover(BaseEventData eventData)
    {
        if (_isFixed)
        {
            return;
        }

        var pointerData = eventData as PointerEventData;
        if (pointerData == null)
        {
            return;
        }

        var button = pointerData.pointerEnter != null
            ? pointerData.pointerEnter.GetComponentInParent<BuildingButton>() 
            : null;

        if (button == null)
        {
            return;
        }

        ShowInfo(button.GetComboCardData(), false);
    }

    public void OnMouseExit()
    {
        if (_isFixed)
        {
            return;
        }

        ClearPanel();
    }

    public void FixInfo(BuildingData data)
    {
        ShowInfo(data, true);
    }

    public void UnfixInfo()
    {
        _isFixed = false;
    }

    public void TogglePinnedVisibility()
    {
        if (_currentData == null)
        {
            return;
        }

        if (_isFixed)
        {
            ClearPanel();
        }
        else
        {
            _isFixed = true;
        }
    }

    private void ShowInfo(BuildingData data, bool fix)
    {
        if (data == null)
        {
            ClearPanel();
            return;
        }

        _currentData = data;
        _isFixed = fix;

        if (buildingName != null)
        {
            buildingName.text = data.displayName;
        }

        if (buildingDesc != null)
        {
            buildingDesc.text = data.description;
        }

        if (resourcePanel != null)
        {
            resourcePanel.SetActive(true);
        }
    }

    private void ClearPanel()
    {
        _currentData = null;
        _isFixed = false;

        if (buildingName != null)
        {
            buildingName.text = string.Empty;
        }

        if (buildingDesc != null)
        {
            buildingDesc.text = string.Empty;
        }

        if (resourcePanel != null)
        {
            resourcePanel.SetActive(false);
        }
    }
}
