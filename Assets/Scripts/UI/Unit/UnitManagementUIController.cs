using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitManagementUIController : MonoBehaviour
{
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

    private void OnEnable()
    {
        UnitManager.OnUnitCountChanged += OnUnitCountChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged += OnUpgradeStateChanged;
        WireTabButtons(true);
        RefreshSummary();
        ShowTab(0);
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

    }

    private void OnMinerTabClicked()
    {
        ShowTab(0);
    }

    private void OnUnitUpgradeTabClicked()
    {
        ShowTab(1);
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
        if (minerSubPanel != null)
        {
            minerSubPanel.SetActive(index == 0);
        }

        if (unitUpgradeSubPanel != null)
        {
            unitUpgradeSubPanel.SetActive(index == 1);
        }

        if (index == 0 && minerAssignmentUI != null)
        {
            minerAssignmentUI.RefreshUIFromSystem();
        }

        if (index == 1 && unitUpgradeUI != null)
        {
            unitUpgradeUI.Refresh();
        }
    }
}
