using System.Collections;
using UnityEngine;

public class UnitUpgradeUIController : MonoBehaviour
{
    [SerializeField] private UnitUpgradeCatalog catalog;
    [SerializeField] private Transform cellParent;
    [SerializeField] private GameObject unitUpgradeCellPrefab;
    [SerializeField] private UnityEngine.UI.Button questButton;
    [SerializeField] private ProceduralQuestPanelController proceduralQuestPanelController;

    private UnitUpgradeProgress _progress;

    private void OnEnable()
    {
        ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged += OnUpgradeStateChanged;
        WireQuestButton(true);
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
        ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged -= OnUpgradeStateChanged;
        WireQuestButton(false);
    }

    private void WireQuestButton(bool add)
    {
        if (questButton == null)
        {
            return;
        }

        questButton.onClick.RemoveListener(OnQuestButtonClicked);
        if (add)
        {
            questButton.onClick.AddListener(OnQuestButtonClicked);
        }
    }

    private void OnQuestButtonClicked()
    {
        if (proceduralQuestPanelController == null)
        {
            proceduralQuestPanelController = FindFirstObjectByType<ProceduralQuestPanelController>(FindObjectsInactive.Include);
        }

        proceduralQuestPanelController?.TogglePanel();
    }

    private void OnResourceAmountChanged(ResourceType _, int __)
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
            cell?.RefreshDisplay();
        }
    }
}
