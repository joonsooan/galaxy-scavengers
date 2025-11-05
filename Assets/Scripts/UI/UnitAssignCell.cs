using UnityEngine;
using UnityEngine.UI;

public class UnitAssignCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image droneIcon;
    [SerializeField] private Image emptySlotIcon;
    [SerializeField] private Button cellButton;

    private Unit_Drone _assignedDrone;
    private ResourceProcessor _processor;

    private void Awake()
    {
        cellButton.onClick.AddListener(OnCellClicked);
    }

    public void Initialize(ResourceProcessor processor, Unit_Drone drone)
    {
        _processor = processor;
        _assignedDrone = drone;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_assignedDrone != null) {
            droneIcon.gameObject.SetActive(true);
            droneIcon.sprite = _assignedDrone.DroneIcon;
            emptySlotIcon.gameObject.SetActive(false);
        }
        else {
            droneIcon.gameObject.SetActive(false);
            emptySlotIcon.gameObject.SetActive(true);
        }
    }

    public void OnCellClicked()
    {
        ProcessorUIManager.Instance.unitAssignPanel.ShowPanel(_processor, this, _assignedDrone);
    }

    public void SetAssignedDrone(Unit_Drone drone)
    {
        _assignedDrone = drone;
        UpdateUI();
    }
}
