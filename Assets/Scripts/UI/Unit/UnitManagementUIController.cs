using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitManagementUIController : MonoBehaviour
{
    private const int TabCount = 3;
    private static int s_lastSelectedTabIndex;

    [Header("Default Settings")]
    [SerializeField] private TMP_Text totalAllyUnitCountText;

    [Header("Miner Settings")]
    [SerializeField] private Button minerTabButton;
    [SerializeField] private TMP_Text minerTabCountText;
    [SerializeField] private GameObject minerSubPanel;
    [SerializeField] private UnitMinerAssignmentUIController minerAssignmentUI;

    [Header("Upgrade Settings")]
    [SerializeField] private Button unitUpgradeTabButton;
    [SerializeField] private GameObject unitUpgradeSubPanel;
    [SerializeField] private UnitUpgradeUIController unitUpgradeUI;

    [Header("Charge Settings")]
    [SerializeField] private Button unitChargeTabButton;
    [SerializeField] private GameObject unitChargeSubPanel;
    [SerializeField] private UnitChargeManagementUIController unitChargeUI;

    private void OnEnable()
    {
        UnitManager.OnUnitCountChanged += OnUnitCountChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged += OnUpgradeStateChanged;
        WireTabButtons(true);
        RefreshSummary();
        ShowTab(GetValidTabIndex(s_lastSelectedTabIndex));
    }

    private void OnDisable()
    {
        UnitManager.OnUnitCountChanged -= OnUnitCountChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged -= OnUpgradeStateChanged;
        WireTabButtons(false);
    }

    private void OnUpgradeStateChanged()
    {
        RefreshSummary();
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

        if (unitUpgradeTabButton != null)
        {
            unitUpgradeTabButton.onClick.RemoveListener(OnUnitUpgradeTabClicked);
            if (add)
            {
                unitUpgradeTabButton.onClick.AddListener(OnUnitUpgradeTabClicked);
            }
        }

        if (unitChargeTabButton != null)
        {
            unitChargeTabButton.onClick.RemoveListener(OnUnitChargeTabClicked);
            if (add)
            {
                unitChargeTabButton.onClick.AddListener(OnUnitChargeTabClicked);
            }
        }

    }

    private void OnMinerTabClicked()
    {
        ShowTab(0);
    }

    private void OnUnitUpgradeTabClicked()
    {
        ShowTab(1);
    }

    private void OnUnitChargeTabClicked()
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

        if (totalAllyUnitCountText != null)
        {
            totalAllyUnitCountText.text = $"{total} / {max}";
        }

        if (minerTabCountText != null)
        {
            minerTabCountText.text = miners.ToString();
        }
    }

    private void ShowTab(int index)
    {
        int safeIndex = GetValidTabIndex(index);
        s_lastSelectedTabIndex = safeIndex;

        if (minerSubPanel != null)
        {
            minerSubPanel.SetActive(safeIndex == 0);
        }

        if (unitUpgradeSubPanel != null)
        {
            unitUpgradeSubPanel.SetActive(safeIndex == 1);
        }

        if (unitChargeSubPanel != null)
        {
            unitChargeSubPanel.SetActive(safeIndex == 2);
        }

        if (safeIndex == 0 && minerAssignmentUI != null)
        {
            minerAssignmentUI.RefreshUIFromSystem();
        }

        if (safeIndex == 1 && unitUpgradeUI != null)
        {
            unitUpgradeUI.Refresh();
        }

        if (safeIndex == 2 && unitChargeUI != null)
        {
            unitChargeUI.Refresh();
        }
    }

    private static int GetValidTabIndex(int index)
    {
        return Mathf.Clamp(index, 0, TabCount - 1);
    }
}
