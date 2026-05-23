using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using FMODUnity;

[DefaultExecutionOrder(-100)]
public class UIManager : MonoBehaviour
{
    [Header("Building Info Panel")]
    [SerializeField] private GameObject buildingInfoPanel;
    [SerializeField] private TMP_Text buildingNameText;
    [SerializeField] private TMP_Text buildingDescText;
    [SerializeField] private GameObject buildingResourcePanel;
    [SerializeField] private GameObject resourceInfoCellPrefab;

    [Header("Production Info Panels")]
    [SerializeField] private GameObject processorInfoPanel;

    [SerializeField] private GameObject droneHubInfoPanel;

    [Header("Extractor Info Panel")]
    [SerializeField] private GameObject extractorInfoPanel;

    [Header("Pause Panel")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private InventorySystem inventorySystem;

    [Header("Storage Info Panel")]
    [SerializeField] private GameObject storageInfoPanel;
    [SerializeField] private GameObject storageResourceListParent;
    [SerializeField] private TMP_Text storageAmountText;
    [SerializeField] private float storageBatteryPanelOffsetX = 150f;
    [SerializeField] private float mainStructurePanelOffsetX = 200f;
    [SerializeField] private float storagePanelOffsetY = 0f;

    [Header("Audio")]
    [SerializeField] private EventReference buildingUIClickSound;

    [Header("Object UI")]
    [SerializeField] private Canvas objectUICanvas;
    private const string ObjectUICanvasName = "ObjectUI Canvas";

    private ActiveUIPanel _activeUIPanel = ActiveUIPanel.None;
    private AreaBuildingDestroyer _areaBuildingDestroyer;

    private BuildingPieceData _pinnedBuildingPieceData;
    private MainStructure _pinnedMainStructureDroneSource;
    private ProcessorData _pinnedProcessorData;
    private ExtractorData _pinnedExtractorData;
    private Action<ResourceType, int, int> _storageResourceChangedHandler;
    private IStorage _trackedStorage;
    private bool _pausePanelLock;

    public Canvas GetObjectUICanvas()
    {
        if (objectUICanvas != null) return objectUICanvas;
        GameObject canvasObj = GameObject.Find(ObjectUICanvasName);
        if (canvasObj != null)
        {
            objectUICanvas = canvasObj.GetComponent<Canvas>();
            return objectUICanvas;
        }
        return null;
    }

    private void Start()
    {
        SetActiveIfNotNull(pausePanel, false);
        SetActiveIfNotNull(buildingInfoPanel, false);
        SetActiveIfNotNull(processorInfoPanel, false);
        SetActiveIfNotNull(droneHubInfoPanel, false);
        SetActiveIfNotNull(extractorInfoPanel, false);
        SetActiveIfNotNull(storageInfoPanel, false);

        _areaBuildingDestroyer = FindFirstObjectByType<AreaBuildingDestroyer>();
        if (inventorySystem == null)
        {
            inventorySystem = GetComponent<InventorySystem>();
        }

        ApplyLocalizedStaticTexts();
    }

    private void Update()
    {
        if (Input.GetMouseButtonUp(1)) {
            if (GameManager.Instance != null && GameManager.Instance.IsDragging()) return;

            if (_areaBuildingDestroyer != null && _areaBuildingDestroyer.HasMoved) {
                return;
            }

            if (_areaBuildingDestroyer != null && _areaBuildingDestroyer.JustFinishedAreaDrag) {
                return;
            }

            InventorySystem inv = GetInventorySystem();
            if (inv != null && inv.GetInventoryPanel() != null && inv.GetInventoryPanel().activeSelf) {
                LaunchUIController launchUI = FindFirstObjectByType<LaunchUIController>(FindObjectsInactive.Include);
                bool isLaunchActive = launchUI != null && launchUI.IsLaunchInputLockActive();
                if (!isLaunchActive) {
                    inv.ToggleInventory();
                    if (_activeUIPanel == ActiveUIPanel.MainStructure) {
                        _activeUIPanel = ActiveUIPanel.None;
                    }
                    return;
                }
            }

            if (UIUtils.IsPointerOverUI()) return;

            UnpinAndHideAllPanels();
        }

        if (storageInfoPanel != null && storageInfoPanel.activeSelf && _trackedStorage != null) {
            UpdateStorageUIPosition();
        }
    }

    private void OnEnable()
    {
        Processor.OnProcessorClicked += HandleProcessorClicked;
        MainStructure.OnDroneProducePanelClicked += HandleMainStructureDroneClicked;
        DataExtractor.OnDataExtractorClicked += HandleDataExtractorClicked;
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDisable()
    {
        Processor.OnProcessorClicked -= HandleProcessorClicked;
        MainStructure.OnDroneProducePanelClicked -= HandleMainStructureDroneClicked;
        DataExtractor.OnDataExtractorClicked -= HandleDataExtractorClicked;
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyPassiveLocaleRefresh();
    }

    public void ApplyPassiveLocaleRefresh()
    {
        ApplyLocalizedStaticTexts();

        if (storageInfoPanel != null && storageInfoPanel.activeSelf && _trackedStorage != null)
        {
            RefreshStorageInfoUI();
        }
    }

    private void ApplyLocalizedStaticTexts()
    {
        SetLocalizedChildText(storageInfoPanel, "Title Text", "game.storageInfoTitle", "저장량");
        SetLocalizedChildText(pausePanel, "txt", "game.pause", "일시정지");
    }

    private static void SetLocalizedChildText(GameObject root, string childName, string key, string fallback)
    {
        if (root == null)
        {
            return;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text != null && text.gameObject.name == childName)
            {
                text.text = GameLocalization.GetOrDefault("UI_Common", key, fallback);
                return;
            }
        }
    }

    private void HandleProcessorClicked(Processor processor)
    {
        if (processor == null) return;

        if (TutorialManager.Instance != null &&
            !TutorialManager.Instance.IsUIPanelEnabledForCurrentStep(TutorialUIPanel.ProcessorInfoPanel)) {
            return;
        }

        HideCurrentIClickableUI();
        HideBuildingInfoPanel();

        _activeUIPanel = ActiveUIPanel.Processor;
        DisplayProcessorInfo(processor);
    }

    private void HandleMainStructureDroneClicked(MainStructure mainStructure)
    {
        if (mainStructure == null) {
            return;
        }

        if (TutorialManager.Instance != null &&
            !TutorialManager.Instance.IsUIPanelEnabledForCurrentStep(TutorialUIPanel.DroneProduceInfoPanel)) {
            return;
        }

        HideCurrentIClickableUI();
        HideBuildingInfoPanel();

        _activeUIPanel = ActiveUIPanel.DroneHub;
        DisplayMainStructureDroneInfo(mainStructure);
    }


    private void HandleDataExtractorClicked(DataExtractor extractor)
    {
        if (extractor == null) return;

        HideCurrentIClickableUI();
        HideBuildingInfoPanel();

        _activeUIPanel = ActiveUIPanel.DataExtractor;
        DisplayExtractorInfo(extractor);
    }
    private void HideCurrentIClickableUI()
    {
        switch (_activeUIPanel) {
        case ActiveUIPanel.Processor:
            if (processorInfoPanel != null) {
                processorInfoPanel.gameObject.SetActive(false);
            }
            break;

        case ActiveUIPanel.DroneHub:
            if (droneHubInfoPanel != null) {
                droneHubInfoPanel.gameObject.SetActive(false);
            }
            break;

        case ActiveUIPanel.DataExtractor:
            if (ExtractorUIManager.Instance != null) {
                ExtractorUIManager.Instance.HideExtractorUI();
            } else if (extractorInfoPanel != null) {
                extractorInfoPanel.SetActive(false);
            }
            break;

        case ActiveUIPanel.MainStructure:
            HideMainStructureInventory();
            break;
        }

        _activeUIPanel = ActiveUIPanel.None;
    }

    private void HideMainStructureInventory()
    {
        InventorySystem inventorySystem = GetInventorySystem();
        if (inventorySystem == null) return;

        GameObject inventoryPanel = inventorySystem.GetInventoryPanel();
        if (inventoryPanel != null && inventoryPanel.activeSelf) {
            inventorySystem.ToggleInventory();
        }
    }

    public void ShowMainStructureUI()
    {
        HideCurrentIClickableUI();

        InventorySystem inventorySystem = GetInventorySystem();
        if (inventorySystem == null) return;

        GameObject inventoryPanel = inventorySystem.GetInventoryPanel();
        if (inventoryPanel == null) return;

        if (!inventoryPanel.activeSelf) {
            inventorySystem.ToggleInventory();
        }

        _activeUIPanel = ActiveUIPanel.MainStructure;
    }

    public void UnpinAndHideAllPanels()
    {
        _pinnedBuildingPieceData = null;
        if (!CanChangePinnedPanels()) {
            return;
        }
        HideBuildingInfoPanel();

        _pinnedProcessorData = null;
        if (processorInfoPanel != null) {
            processorInfoPanel.SetActive(false);
        }

        _pinnedMainStructureDroneSource = null;
        if (droneHubInfoPanel != null) {
            droneHubInfoPanel.SetActive(false);
        }

        _pinnedExtractorData = null;
        if (extractorInfoPanel != null) {
            extractorInfoPanel.SetActive(false);
        }
        if (ExtractorUIManager.Instance != null) {
            ExtractorUIManager.Instance.HideExtractorUI();
        }

        if (storageInfoPanel != null) {
            if (_trackedStorage != null && _storageResourceChangedHandler != null) {
                _trackedStorage.OnResourceChanged -= _storageResourceChangedHandler;
            }
            storageInfoPanel.SetActive(false);
            _trackedStorage = null;
        }

        HideCurrentIClickableUI();

        if (BuildingHoverManager.Instance != null) {
            BuildingHoverManager.Instance.NotifyPanelsClosed();
        }
    }

    public void HideProcessorAndDroneHubPanels()
    {
        _pinnedProcessorData = null;
        if (processorInfoPanel != null) {
            processorInfoPanel.SetActive(false);
        }
        _pinnedMainStructureDroneSource = null;
        if (droneHubInfoPanel != null) {
            droneHubInfoPanel.SetActive(false);
        }
        if (_activeUIPanel == ActiveUIPanel.Processor || _activeUIPanel == ActiveUIPanel.DroneHub ||
            _activeUIPanel == ActiveUIPanel.DataExtractor) {
            _activeUIPanel = ActiveUIPanel.None;
        }
    }

    public void DisplayCardInfo(BuildingPieceData data)
    {
        if (data == null) return;
        buildingInfoPanel.SetActive(true);
        buildingNameText.text = data.GetDisplayName();
        buildingDescText.text = data.GetDescription();

        foreach (Transform child in buildingResourcePanel.transform) Destroy(child.gameObject);
        foreach (ResourceCost cost in data.costs) {
            GameObject cell = Instantiate(resourceInfoCellPrefab, buildingResourcePanel.transform);
            cell.GetComponent<ResourceInfoCell>().SetInfo(cost.resourceType, cost.amount);
        }
    }

    public void HideCardInfo()
    {
        if (_pinnedBuildingPieceData == null) {
            buildingInfoPanel.SetActive(false);
        }
        else {
            DisplayCardInfo(_pinnedBuildingPieceData);
        }
    }

    public void PinCardInfo(BuildingPieceData data)
    {
        if (_pinnedBuildingPieceData == data) {
            UnpinAndHideAllPanels();
        }
        else {
            _pinnedBuildingPieceData = data;
            DisplayCardInfo(data);
        }
    }

    public void UnpinAndHideCardPanel()
    {
        _pinnedBuildingPieceData = null;
        if (buildingInfoPanel != null) {
            buildingInfoPanel.SetActive(false);
        }
    }

    private void DisplayProcessorInfo(Processor processor)
    {
        if (processor == null || processorInfoPanel == null) return;
        
        if (!buildingUIClickSound.IsNull)
        {
            RuntimeManager.PlayOneShot(buildingUIClickSound);
        }
        
        processorInfoPanel.gameObject.SetActive(true);
        ProcessorUIManager.Instance.ShowProcessorUI(processor);
    }

    public void HideProcessorInfo()
    {
        if (processorInfoPanel == null) return;

        if (_pinnedProcessorData == null) {
            processorInfoPanel.gameObject.SetActive(false);
            if (_activeUIPanel == ActiveUIPanel.Processor) {
                _activeUIPanel = ActiveUIPanel.None;
            }
        }
    }

    public void PinProcessorInfo(Processor processor)
    {
        if (processor == null) return;

        ProcessorData data = processor.ProcessorData;

        if (_pinnedProcessorData == data) {
            UnpinAndHideAllPanels();
        }
        else {
            UnpinAndHideAllPanels();
            _pinnedProcessorData = data;
            DisplayProcessorInfo(processor);
        }
    }

    private void DisplayMainStructureDroneInfo(MainStructure mainStructure)
    {
        if (mainStructure == null || droneHubInfoPanel == null) {
            return;
        }

        if (!buildingUIClickSound.IsNull) {
            RuntimeManager.PlayOneShot(buildingUIClickSound);
        }

        droneHubInfoPanel.gameObject.SetActive(true);
        if (DroneProduceUIManager.Instance != null) {
            DroneProduceUIManager.Instance.ShowDroneProduceUI(mainStructure);
        }
    }

    public void HideDroneHubInfo()
    {
        if (droneHubInfoPanel == null) {
            return;
        }

        if (_pinnedMainStructureDroneSource == null) {
            droneHubInfoPanel.gameObject.SetActive(false);
            if (_activeUIPanel == ActiveUIPanel.DroneHub) {
                _activeUIPanel = ActiveUIPanel.None;
            }
        }
    }

    public void PinMainStructureDroneInfo(MainStructure mainStructure)
    {
        if (mainStructure == null) {
            return;
        }

        if (_pinnedMainStructureDroneSource == mainStructure) {
            UnpinAndHideAllPanels();
        }
        else {
            UnpinAndHideAllPanels();
            _pinnedMainStructureDroneSource = mainStructure;
            DisplayMainStructureDroneInfo(mainStructure);
        }
    }

    public bool IsProcessorPanelActive()
    {
        return processorInfoPanel != null && processorInfoPanel.activeSelf;
    }

    private void DisplayExtractorInfo(DataExtractor extractor)
    {
        if (extractor == null || extractorInfoPanel == null) return;

        if (!buildingUIClickSound.IsNull)
        {
            RuntimeManager.PlayOneShot(buildingUIClickSound);
        }

        extractorInfoPanel.SetActive(true);
        if (ExtractorUIManager.Instance != null) {
            ExtractorUIManager.Instance.ShowExtractorUI(extractor);
        }
    }

    public void HideExtractorInfo()
    {
        if (extractorInfoPanel == null) return;

        if (_pinnedExtractorData == null) {
            if (ExtractorUIManager.Instance != null) {
                ExtractorUIManager.Instance.HideExtractorUI();
            } else {
                extractorInfoPanel.SetActive(false);
            }
            if (_activeUIPanel == ActiveUIPanel.DataExtractor) {
                _activeUIPanel = ActiveUIPanel.None;
            }
        }
    }

    public void PinExtractorInfo(DataExtractor extractor)
    {
        if (extractor == null) return;

        ExtractorData data = extractor.ExtractorDataAsset;

        if (_pinnedExtractorData == data) {
            UnpinAndHideAllPanels();
        }
        else {
            UnpinAndHideAllPanels();
            _pinnedExtractorData = data;
            DisplayExtractorInfo(extractor);
        }
    }

    public bool IsExtractorPanelActive()
    {
        return extractorInfoPanel != null && extractorInfoPanel.activeSelf;
    }

    public bool TryCloseExtractorPanelWithEscape()
    {
        if (!IsExtractorPanelActive()) {
            return false;
        }

        _pinnedExtractorData = null;
        if (ExtractorUIManager.Instance != null) {
            ExtractorUIManager.Instance.HideExtractorUI();
        }

        if (extractorInfoPanel != null) {
            extractorInfoPanel.SetActive(false);
        }

        if (_activeUIPanel == ActiveUIPanel.DataExtractor) {
            _activeUIPanel = ActiveUIPanel.None;
        }

        return true;
    }

    public bool IsDroneHubPanelActive()
    {
        return droneHubInfoPanel != null && droneHubInfoPanel.activeSelf;
    }

    public void SetPausePanelActive(bool active)
    {
        if (_pausePanelLock && !active)
        {
            return;
        }

        if (pausePanel != null) {
            pausePanel.SetActive(active);
        }
    }

    public void SetPausePanelLock(bool locked)
    {
        _pausePanelLock = locked;
        if (_pausePanelLock && pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
    }

    public InventorySystem GetInventorySystem()
    {
        if (inventorySystem != null)
        {
            return inventorySystem;
        }

        inventorySystem = GetComponent<InventorySystem>();
        if (inventorySystem != null)
        {
            return inventorySystem;
        }

        inventorySystem = FindFirstObjectByType<InventorySystem>(FindObjectsInactive.Include);
        return inventorySystem;
    }

    public void DisplayStorageInfo(IStorage storage)
    {
        if (storageInfoPanel == null || storage == null) return;

        if (TutorialManager.Instance != null &&
            !TutorialManager.Instance.IsUIPanelEnabledForCurrentStep(TutorialUIPanel.StorageResourceInfoPanel)) {
            return;
        }

        if (_trackedStorage != null && _storageResourceChangedHandler != null) {
            _trackedStorage.OnResourceChanged -= _storageResourceChangedHandler;
        }

        _trackedStorage = storage;
        storageInfoPanel.SetActive(true);
        UpdateStorageUIPosition();

        if (_storageResourceChangedHandler == null) {
            _storageResourceChangedHandler = OnStorageResourceChanged;
        }
        storage.OnResourceChanged += _storageResourceChangedHandler;

        RefreshStorageInfoUI();
    }

    private void RefreshStorageInfoUI()
    {
        if (storageInfoPanel == null || _trackedStorage == null || !storageInfoPanel.activeSelf) return;

        Transform parent = storageResourceListParent != null ? storageResourceListParent.transform : storageInfoPanel.transform;

        bool isFirstChild = true;
        foreach (Transform child in parent) {
            if (isFirstChild) {
                isFirstChild = false;
            }
            else {
                Destroy(child.gameObject);
            }
        }

        Dictionary<ResourceType, int> resources = _trackedStorage.GetStoredResources();
        int maxCapacity = _trackedStorage.GetMaxCapacity();
        int totalAmount = 0;

        foreach (KeyValuePair<ResourceType, int> kvp in resources) {
            if (kvp.Value > 0) {
                GameObject cell = Instantiate(resourceInfoCellPrefab, parent);
                ResourceInfoCell cellScript = cell.GetComponent<ResourceInfoCell>();
                if (cellScript != null) {
                    cellScript.SetInfoDisplayOnly(kvp.Key, kvp.Value);
                    totalAmount += kvp.Value;
                }
            }
        }

        if (storageAmountText != null) {
            storageAmountText.text = $"{totalAmount.ToString()} / {maxCapacity.ToString()}";
        }
    }

    private void OnStorageResourceChanged(ResourceType type, int current, int max)
    {
        if (storageInfoPanel != null && storageInfoPanel.activeSelf && _trackedStorage != null) {
            RefreshStorageInfoUI();
        }
    }

    public void HideStorageInfo()
    {
        if (_trackedStorage != null && _storageResourceChangedHandler != null) {
            _trackedStorage.OnResourceChanged -= _storageResourceChangedHandler;
        }

        if (storageInfoPanel != null) {
            storageInfoPanel.SetActive(false);
        }

        _trackedStorage = null;
    }

    private void UpdateStorageUIPosition()
    {
        if (_trackedStorage == null || Camera.main == null) return;

        Vector3 worldPos = _trackedStorage.GetPosition();
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        float offsetX = _trackedStorage is MainStructure ? mainStructurePanelOffsetX : storageBatteryPanelOffsetX;
        screenPos.x += offsetX;
        screenPos.y += storagePanelOffsetY;

        storageInfoPanel.transform.position = screenPos;
    }

    private enum ActiveUIPanel
    {
        None,
        Processor,
        DroneHub,
        DataExtractor,
        MainStructure
    }

    private static void SetActiveIfNotNull(GameObject panel, bool active)
    {
        if (panel != null) {
            panel.SetActive(active);
        }
    }

    private void HideBuildingInfoPanel()
    {
        SetActiveIfNotNull(buildingInfoPanel, false);
    }

    private bool CanChangePinnedPanels()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) {
            return false;
        }

        if (_areaBuildingDestroyer != null && _areaBuildingDestroyer.HasMoved) {
            return false;
        }

        if (_areaBuildingDestroyer != null && _areaBuildingDestroyer.JustFinishedAreaDrag) {
            return false;
        }

        return true;
    }
}



