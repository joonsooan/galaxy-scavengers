using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitAssignPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject availableUnitPanel;
    [SerializeField] private TMP_Text unassignedDroneCountText;
    [SerializeField] private Button assignDroneButton;
    [SerializeField] private Button unassignDroneButton;

    // [SerializeField] private Button closeButton;

    private Unit_Drone _currentDrone;
    private UnitAssignCell _targetCell;

    private Processor _targetProcessor;

    private void OnEnable()
    {
        UnitManager.OnUnitCountChanged += OnUnitCountChangedHandler;
        
        if (availableUnitPanel.activeSelf)
        {
            UpdatePanelInfo();
        }
    }

    private void OnDisable()
    {
        UnitManager.OnUnitCountChanged -= OnUnitCountChangedHandler;
    }

    private void Awake()
    {
        assignDroneButton.onClick.AddListener(OnAssignDroneClicked);
        unassignDroneButton.onClick.AddListener(OnUnassignDroneClicked);

        // closeButton.onClick.AddListener(HidePanel);

        availableUnitPanel.SetActive(false);
    }

    public void ShowPanel(Processor processor, UnitAssignCell cell, Unit_Drone drone)
    {
        _targetProcessor = processor;
        _targetCell = cell;
        _currentDrone = drone;

        UpdatePanelInfo();
        availableUnitPanel.SetActive(true);
    }

    private void UpdatePanelInfo()
    {
        int availableDrones = GetAvailableDroneCount();
        unassignedDroneCountText.text = $"x {availableDrones}";

        if (_currentDrone == null) {
            assignDroneButton.gameObject.SetActive(true);
            unassignDroneButton.gameObject.SetActive(false);

            assignDroneButton.interactable = availableDrones > 0;
        }
        else {
            assignDroneButton.gameObject.SetActive(false);
            unassignDroneButton.gameObject.SetActive(true);
        }
    }
    
    private void OnUnitCountChangedHandler(UnitBase unit)
    {
        if (!availableUnitPanel.activeSelf)
        {
            return;
        }

        UpdatePanelInfo();
    }

    private int GetAvailableDroneCount()
    {
        return UnitManager.Instance.AllyUnits
            .OfType<Unit_Drone>()
            .Count(d => !d.IsAssigned);
    }

    private void OnAssignDroneClicked()
    {
        Unit_Drone droneToAssign = UnitManager.Instance.AllyUnits
            .OfType<Unit_Drone>()
            .FirstOrDefault(d => !d.IsAssigned);

        if (droneToAssign != null) {
            droneToAssign.AssignProcessor(_targetProcessor, true);

            _targetCell.SetAssignedDrone(droneToAssign);

            HidePanel();
        }
    }

    private void OnUnassignDroneClicked()
    {
        if (_currentDrone != null) {
            _currentDrone.AssignProcessor(null, true);

            _targetCell.SetAssignedDrone(null);

            HidePanel();
        }
    }

    private void HidePanel()
    {
        availableUnitPanel.SetActive(false);
    }
}
