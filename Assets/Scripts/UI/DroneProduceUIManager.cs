using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class DroneProduceUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject droneProduceCellPrefab;
    public Transform contentParent;

    [Header("Display UI")]
    [SerializeField] private TMP_Text droneHubName;
    [SerializeField] private TMP_Text droneHubInfo;
    [SerializeField] private TMP_Text unitCountText;
    [SerializeField] private UnitInfoPanel unitInfoPanel;

    [Header("Unit Info Panel Layout")]
    [SerializeField] private Vector2 unitInfoPanelAnchor = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 unitInfoPanelAnchoredPosition;

    private List<UnitData> _allProducibleUnits;
    private MainStructure _currentMainStructure;
    private const string DroneHubNameKey = "base.droneHub";
    private const string DroneHubInfoKey = "base.droneHubDescription";

    public static DroneProduceUIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
        }
        else {
            Instance = this;
        }

        ResolveUnitInfoPanel();
    }

    public void ShowDroneProduceUI(MainStructure mainStructure)
    {
        _currentMainStructure = mainStructure;
        ResolveUnitInfoPanel();
        if (unitInfoPanel != null) {
            unitInfoPanel.ApplyFixedAnchorLayout(unitInfoPanelAnchor, unitInfoPanelAnchoredPosition);
        }

        SetPanelTexts(mainStructure);
        LoadAllProducibleUnits(mainStructure);
        UpdateUnitCountText();
    }

    private void SetPanelTexts(MainStructure mainStructure)
    {
        if (mainStructure == null) {
            return;
        }

        if (droneHubName != null) {
            droneHubName.text = GameLocalization.GetOrDefault("UI_Common", DroneHubNameKey,
                mainStructure.DroneProduceDisplayName);
        }

        if (droneHubInfo != null) {
            droneHubInfo.text = GameLocalization.GetOrDefault("UI_Common", DroneHubInfoKey,
                mainStructure.DroneProduceDescription);
        }
    }

    private void UpdateUnitCountText()
    {
        if (unitCountText == null || UnitManager.Instance == null) {
            return;
        }

        int current = UnitManager.Instance.GetPopulationCountedAllyCount();
        int max = UnitManager.Instance.GetMaxPopulation();

        unitCountText.text = $"{current} / {max}";

        if (current >= max) {
            unitCountText.color = Color.red;
        }
        else {
            unitCountText.color = Color.white;
        }
    }

    private void OnEnable()
    {
        ResolveUnitInfoPanel();
        UnitManager.OnUnitCountChanged += OnUnitCountChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged += OnUpgradeProgressStateChanged;
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        if (UnitUnlockManager.Instance != null) {
            UnitUnlockManager.Instance.OnUnitUnlocked += OnUnitUnlocked_Handler;
        }
    }

    private void OnDisable()
    {
        UnitManager.OnUnitCountChanged -= OnUnitCountChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged -= OnUpgradeProgressStateChanged;
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        if (UnitUnlockManager.HasInstance) {
            UnitUnlockManager.Instance.OnUnitUnlocked -= OnUnitUnlocked_Handler;
        }
        ClearUnitInfo();
        if (unitInfoPanel != null) {
            unitInfoPanel.RestoreDefaultLayout();
        }
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        SetPanelTexts(_currentMainStructure);
    }

    private void OnUnitCountChanged(UnitBase unit)
    {
        if (_currentMainStructure != null) {
            UpdateUnitCountText();
        }
    }

    private void OnUpgradeProgressStateChanged()
    {
        if (_currentMainStructure != null) {
            UpdateUnitCountText();
        }
    }

    private void OnUnitUnlocked_Handler(UnitData _)
    {
        if (_currentMainStructure != null) {
            ClearAllUnits();
            InstantiateUnitCells();
        }
    }

    private void LoadAllProducibleUnits(MainStructure mainStructure)
    {
        ClearAllUnits();
        if (mainStructure == null || mainStructure.ProducibleUnits == null) {
            return;
        }

        _allProducibleUnits = mainStructure.ProducibleUnits;
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

        if (_currentMainStructure == null || _allProducibleUnits == null) {
            return;
        }

        for (int i = 0; i < _allProducibleUnits.Count; i++) {
            UnitData unitData = _allProducibleUnits[i];
            if (unitData == null) {
                continue;
            }

            if (UnitUnlockManager.Instance != null && !UnitUnlockManager.Instance.IsUnitUnlocked(unitData)) {
                continue;
            }

            GameObject newCellObject = Instantiate(droneProduceCellPrefab, contentParent);
            DroneProduceCell newCell = newCellObject.GetComponent<DroneProduceCell>();

            if (newCell != null) {
                newCell.Initialize(unitData, _currentMainStructure, i, this);
            }
        }
    }

    public void OnProduceCellHover(UnitData unitData)
    {
        ShowUnitInfo(unitData);
    }

    public void OnProduceCellHoverExit(UnitData unitData)
    {
        ClearUnitInfo();
    }

    public void OnProduceCellClicked(UnitData unitData)
    {
        ShowUnitInfo(unitData);
    }

    public void ClearUnitInfo()
    {
        if (unitInfoPanel != null) {
            unitInfoPanel.ClearAllInfo();
        }
    }

    private void ShowUnitInfo(UnitData unitData)
    {
        ResolveUnitInfoPanel();
        if (unitInfoPanel == null) {
            return;
        }

        if (unitData == null) {
            unitInfoPanel.ClearAllInfo();
            return;
        }

        unitInfoPanel.PreviewInfo(unitData);
    }

    private void ResolveUnitInfoPanel()
    {
        if (unitInfoPanel != null) {
            return;
        }

        unitInfoPanel = UnitInfoPanel.Instance;
        if (unitInfoPanel == null) {
            unitInfoPanel = FindFirstObjectByType<UnitInfoPanel>(FindObjectsInactive.Include);
        }
    }
}
