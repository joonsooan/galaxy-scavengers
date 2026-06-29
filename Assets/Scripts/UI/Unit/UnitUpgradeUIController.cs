using System.Collections;
using UnityEngine;

public class UnitUpgradeUIController : MonoBehaviour
{
    [SerializeField] private UnitUpgradeCatalog catalog;
    [SerializeField] private Transform cellParent;
    [SerializeField] private GameObject unitUpgradeCellPrefab;

    private UnitUpgradeProgress _progress;

    private void OnEnable()
    {
        GameplayTokenWallet.EnsureExists(this);
        GameplayTokenWallet.OnBalanceChanged += OnTokenBalanceChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged += OnUpgradeStateChanged;
        ResolveProgressReference();
        Refresh();
        StartCoroutine(RefreshAfterProgressReady());
    }

    private IEnumerator RefreshAfterProgressReady()
    {
        yield return null;
        ResolveProgressReference();
        Refresh();
    }

    private void OnDisable()
    {
        GameplayTokenWallet.OnBalanceChanged -= OnTokenBalanceChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged -= OnUpgradeStateChanged;
    }

    private void OnTokenBalanceChanged()
    {
        RefreshCellsOnly();
    }

    private void OnUpgradeStateChanged()
    {
        RefreshCellsOnly();
    }

    public void Refresh()
    {
        if (cellParent == null || unitUpgradeCellPrefab == null || catalog == null) {
            return;
        }

        ResolveProgressReference();

        for (int i = cellParent.childCount - 1; i >= 0; i--) {
            Destroy(cellParent.GetChild(i).gameObject);
        }

        foreach (UnitUpgradeLineData line in catalog.GetLinesInDisplayOrder())
        {
            SpawnLine(line);
        }
    }

    private void ResolveProgressReference()
    {
        _progress = UnitUpgradeProgress.Instance;
        if (_progress == null) {
            _progress = FindFirstObjectByType<UnitUpgradeProgress>(FindObjectsInactive.Include);
        }
    }

    private void SpawnLine(UnitUpgradeLineData line)
    {
        if (line == null || cellParent == null) {
            return;
        }

        GameObject go = Instantiate(unitUpgradeCellPrefab, cellParent);
        UnitUpgradeCell cell = go.GetComponent<UnitUpgradeCell>();
        if (cell != null) {
            cell.Initialize(line, _progress);
        }
    }

    private void RefreshCellsOnly()
    {
        if (cellParent == null) {
            return;
        }

        for (int i = 0; i < cellParent.childCount; i++) {
            UnitUpgradeCell cell = cellParent.GetChild(i).GetComponent<UnitUpgradeCell>();
            if (cell != null) cell.RefreshDisplay();
        }
    }
}
