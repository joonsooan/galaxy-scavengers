using System.Collections.Generic;
using UnityEngine;

public class UnitChargeManagementUIController : MonoBehaviour
{
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private Transform contentParent;

    private readonly List<UnitChargeCellUI> _cells = new List<UnitChargeCellUI>();

    private void OnEnable()
    {
        UnitManager.OnUnitCountChanged += OnUnitCountChanged;
        Refresh();
    }

    private void OnDisable()
    {
        UnitManager.OnUnitCountChanged -= OnUnitCountChanged;
    }

    private void OnUnitCountChanged(UnitBase _)
    {
        Refresh();
    }

    public void Refresh()
    {
        ClearCells();

        if (cellPrefab == null || contentParent == null || UnitManager.Instance == null)
        {
            return;
        }

        foreach (UnitBase unit in UnitManager.Instance.AllyUnits)
        {
            if (unit == null || unit is Unit_Player)
            {
                continue;
            }

            if (unit.unitData == null || !unit.unitData.useInternalBattery)
            {
                continue;
            }

            UnitBattery battery = unit.GetComponent<UnitBattery>();
            if (battery == null)
            {
                continue;
            }

            GameObject go = Instantiate(cellPrefab, contentParent);
            UnitChargeCellUI cell = go.GetComponent<UnitChargeCellUI>();
            if (cell != null)
            {
                cell.Bind(unit);
                _cells.Add(cell);
            }
        }
    }

    private void ClearCells()
    {
        foreach (UnitChargeCellUI cell in _cells)
        {
            if (cell != null)
            {
                Destroy(cell.gameObject);
            }
        }

        _cells.Clear();

        if (contentParent != null)
        {
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                Destroy(contentParent.GetChild(i).gameObject);
            }
        }
    }
}
