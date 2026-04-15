using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitManagementUIController : MonoBehaviour
{
    [SerializeField] private TMP_Text totalAllyUnitCountText;

    [SerializeField] private Button minerTabButton;

    [SerializeField] private TMP_Text minerTabCountText;

    [SerializeField] private GameObject minerSubPanel;

    [SerializeField] private UnitMinerAssignmentUIController minerAssignmentUI;

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

    }

    private void OnMinerTabClicked()
    {
        ShowTab(0);
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

        if (index == 0 && minerAssignmentUI != null)
        {
            minerAssignmentUI.RefreshUIFromSystem();
        }
    }
}
