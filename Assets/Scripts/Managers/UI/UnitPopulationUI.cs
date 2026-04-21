using TMPro;
using UnityEngine;

public class UnitPopulationUI : MonoBehaviour
{
    [SerializeField] private TMP_Text populationText;

    private void OnEnable()
    {
        UnitManager.OnUnitCountChanged += HandleUnitCountChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged += OnUpgradeStateChanged;
        UpdateText();
    }

    private void OnDisable()
    {
        UnitManager.OnUnitCountChanged -= HandleUnitCountChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged -= OnUpgradeStateChanged;
    }

    private void OnUpgradeStateChanged()
    {
        UpdateText();
    }

    private void Start()
    {
        UpdateText();
    }

    private void HandleUnitCountChanged(UnitBase unit)
    {
        UpdateText();
    }

    private void UpdateText()
    {
        if (populationText == null) return;
        if (UnitManager.Instance == null) return;

        int current = UnitManager.Instance.AllyUnits.Count;
        int max = UnitManager.Instance.GetMaxPopulation();

        populationText.text = $"현재 유닛 수 : {current.ToString()} / {max.ToString()}";
    }
}

