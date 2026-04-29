using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class UnitAssignCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image droneIcon;
    [SerializeField] private Image emptySlotIcon;
    [SerializeField] private Button cellButton;

    [Header("Tutorial Settings")]
    [SerializeField] private Material glowMaterial;

    private Unit_Processor _assignedDrone;
    private Processor _processor;
    private string _tutorialID;

    private void Awake()
    {
        cellButton.onClick.AddListener(OnCellClicked);
    }

    public void Initialize(Processor processor, Unit_Processor drone)
    {
        _processor = processor;
        _assignedDrone = drone;
        
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_assignedDrone != null) {
            droneIcon.gameObject.SetActive(true);
            emptySlotIcon.gameObject.SetActive(false);
        }
        else {
            droneIcon.gameObject.SetActive(false);
            emptySlotIcon.gameObject.SetActive(true);
        }
    }

    private void OnCellClicked()
    {
        if (_assignedDrone == null) {
            Unit_Processor droneToAssign = UnitManager.Instance.AllyUnits
                .OfType<Unit_Processor>()
                .FirstOrDefault(d => !d.IsAssigned);

            if (droneToAssign != null) {
                droneToAssign.AssignProcessor(_processor);
                SetAssignedDrone(droneToAssign);
            }
        }
        else {
            _assignedDrone.AssignProcessor(null);
            SetAssignedDrone(null);
        }
    }

    private void SetAssignedDrone(Unit_Processor drone)
    {
        _assignedDrone = drone;
        UpdateUI();
    }
}
