using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitManagementUIController : MonoBehaviour
{
    [SerializeField] private TMP_Text totalAllyUnitCountText;

    [SerializeField] private Button minerTabButton;
    [SerializeField] private Button constructTabButton;
    [SerializeField] private Button processorTabButton;

    [SerializeField] private TMP_Text minerTabCountText;
    [SerializeField] private TMP_Text constructTabCountText;
    [SerializeField] private TMP_Text processorTabCountText;

    [SerializeField] private GameObject minerSubPanel;
    [SerializeField] private GameObject constructPlaceholderSubPanel;
    [SerializeField] private GameObject processorSubPanel;

    [SerializeField] private UnitMinerAssignmentUIController minerAssignmentUI;
    [SerializeField] private UnitProcessorActivityUIController processorActivityUI;

    private void OnEnable()
    {
        UnitManager.OnUnitCountChanged += OnUnitCountChanged;
        WireTabButtons(true);
        RefreshSummary();
        ShowTab(0);
    }

    private void OnDisable()
    {
        UnitManager.OnUnitCountChanged -= OnUnitCountChanged;
        WireTabButtons(false);
    }

    private void WireTabButtons(bool add)
    {
        if (minerTabButton != null)
        {
            minerTabButton.onClick.RemoveListener(OnMinerTabClicked);
            if (add)
            {
                minerTabButton.onClick.AddListener(OnMinerTabClicked);
            }
        }

        if (constructTabButton != null)
        {
            constructTabButton.onClick.RemoveListener(OnConstructTabClicked);
            if (add)
            {
                constructTabButton.onClick.AddListener(OnConstructTabClicked);
            }
        }

        if (processorTabButton != null)
        {
            processorTabButton.onClick.RemoveListener(OnProcessorTabClicked);
            if (add)
            {
                processorTabButton.onClick.AddListener(OnProcessorTabClicked);
            }
        }
    }

    private void OnMinerTabClicked()
    {
        ShowTab(0);
    }

    private void OnConstructTabClicked()
    {
        ShowTab(1);
    }

    private void OnProcessorTabClicked()
    {
        ShowTab(2);
    }

    private void OnUnitCountChanged(UnitBase _)
    {
        RefreshSummary();
    }

    public void RefreshSummary()
    {
        if (UnitManager.Instance == null)
        {
            return;
        }

        int total = UnitManager.Instance.GetPopulationCountedAllyCount();
        int max = UnitManager.Instance.GetMaxPopulation();
        int miners = UnitManager.Instance.AllyUnits.Count(u => u is Unit_Miner);
        int constructs = UnitManager.Instance.AllyUnits.Count(u => u is Unit_Construct);
        int drones = UnitManager.Instance.AllyUnits.Count(u => u is Unit_Drone);

        if (totalAllyUnitCountText != null)
        {
            totalAllyUnitCountText.text = $"{total} / {max}";
        }

        if (minerTabCountText != null)
        {
            minerTabCountText.text = miners.ToString();
        }

        if (constructTabCountText != null)
        {
            constructTabCountText.text = constructs.ToString();
        }

        if (processorTabCountText != null)
        {
            processorTabCountText.text = drones.ToString();
        }
    }

    private void ShowTab(int index)
    {
        if (minerSubPanel != null)
        {
            minerSubPanel.SetActive(index == 0);
        }

        if (constructPlaceholderSubPanel != null)
        {
            constructPlaceholderSubPanel.SetActive(index == 1);
        }

        if (processorSubPanel != null)
        {
            processorSubPanel.SetActive(index == 2);
        }

        if (index == 0 && minerAssignmentUI != null)
        {
            minerAssignmentUI.RefreshUIFromSystem();
        }

        if (index == 2 && processorActivityUI != null)
        {
            processorActivityUI.Refresh();
        }
    }
}
