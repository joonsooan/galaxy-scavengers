using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DroneProduceUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject droneProduceCellPrefab;
    public Transform contentParent;

    [Header("Display UI")]
    [SerializeField] private TMP_Text droneHubName;
    [SerializeField] private TMP_Text droneHubInfo;
    [SerializeField] private TMP_Text unitCountText;

    private List<UnitData> _allProducibleUnits;
    private DroneHubData _currentData;
    private DroneHub _currentDroneHub;

    public static DroneProduceUIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
        }
        else {
            Instance = this;
        }
    }

    public void ShowDroneHubUI(DroneHub droneHub)
    {
        _currentDroneHub = droneHub;
        _currentData = droneHub.DroneHubData;

        SetDroneHubInfo(_currentData);
        LoadAllProducibleUnits(_currentData);
        UpdateUnitCountText();
    }

    private void SetDroneHubInfo(DroneHubData data)
    {
        if (data == null) return;

        if (droneHubName != null) {
            droneHubName.text = data.DroneHubName;
        }

        if (droneHubInfo != null) {
            droneHubInfo.text = data.DroneHubInfo;
        }
    }

    private void UpdateUnitCountText()
    {
        if (unitCountText == null || UnitManager.Instance == null) return;

        int current = UnitManager.Instance.AllyUnits.Count;
        int max = UnitManager.Instance.GetMaxPopulation();

        unitCountText.text = $"{current} / {max}";

        if (current >= max)
        {
            unitCountText.color = Color.red;
        }
        else
        {
            unitCountText.color = Color.white;
        }
    }

    private void OnEnable()
    {
        UnitManager.OnUnitCountChanged += OnUnitCountChanged;
    }

    private void OnDisable()
    {
        UnitManager.OnUnitCountChanged -= OnUnitCountChanged;
    }

    private void OnUnitCountChanged(UnitBase unit)
    {
        if (_currentDroneHub != null)
        {
            UpdateUnitCountText();
        }
    }

    private void LoadAllProducibleUnits(DroneHubData data)
    {
        ClearAllUnits();
        if (data == null || data.ProducibleUnits == null) {
            return;
        }

        _allProducibleUnits = data.ProducibleUnits;
        InstantiateUnitCells();
    }

    private void ClearAllUnits()
    {
        foreach (Transform child in contentParent) {
            Destroy(child.gameObject);
        }
    }

    private void InstantiateUnitCells()
    {
        if (droneProduceCellPrefab == null || contentParent == null) {
            Debug.LogError("Drone Produce Cell Prefab and Content Parent not set");
            return;
        }

        if (_currentDroneHub == null || _allProducibleUnits == null) {
            return;
        }

        for (int i = 0; i < _allProducibleUnits.Count; i++) {
            UnitData unitData = _allProducibleUnits[i];
            if (unitData == null) continue;

            GameObject newCellObject = Instantiate(droneProduceCellPrefab, contentParent);
            DroneProduceCell newCell = newCellObject.GetComponent<DroneProduceCell>();

            if (newCell != null) {
                newCell.Initialize(unitData, _currentDroneHub, i);
            }
        }
    }
}
