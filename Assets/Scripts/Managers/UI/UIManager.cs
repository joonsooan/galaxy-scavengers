using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    [Header("Processor Info Panel")]
    [SerializeField] private GameObject processorInfoPanel;

    [Header("Drone Hub Info Panel")]
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
    private DroneHubData _pinnedDroneHubData;
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
        if (pausePanel != null) pausePanel.SetActive(false);
        if (buildingInfoPanel != null) buildingInfoPanel.SetActive(false);
        if (processorInfoPanel != null) processorInfoPanel.SetActive(false);
        if (droneHubInfoPanel != null) droneHubInfoPanel.SetActive(false);
        if (extractorInfoPanel != null) extractorInfoPanel.SetActive(false);
        if (storageInfoPanel != null) storageInfoPanel.SetActive(false);

        _areaBuildingDestroyer = FindFirstObjectByType<AreaBuildingDestroyer>();
        if (inventorySystem == null)
        {
            inventorySystem = GetComponent<InventorySystem>();
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonUp(1)) {
            if (UIUtils.IsPointerOverUI()) return;
            if (GameManager.Instance != null && GameManager.Instance.IsDragging()) return;

            if (_areaBuildingDestroyer != null && _areaBuildingDestroyer.HasMoved) {
                return;
            }

            if (_areaBuildingDestroyer != null && _areaBuildingDestroyer.JustFinishedAreaDrag) {
                return;
            }

            UnpinAndHideAllPanels();
        }

        if (storageInfoPanel != null && storageInfoPanel.activeSelf && _trackedStorage != null) {
            UpdateStorageUIPosition();
        }
    }

    private void OnEnable()
    {
        Processor.OnProcessorClicked += HandleProcessorClicked;
        DroneHub.OnDroneHubClicked += HandleDroneHubClicked;
        DataExtractor.OnDataExtractorClicked += HandleDataExtractorClicked;
    }

    private void OnDisable()
    {
        Processor.OnProcessorClicked -= HandleProcessorClicked;
        DroneHub.OnDroneHubClicked -= HandleDroneHubClicked;
        DataExtractor.OnDataExtractorClicked -= HandleDataExtractorClicked;
    }

    private void HandleProcessorClicked(Processor processor)
    {
        if (processor == null) return;

        HideCurrentIClickableUI();
        
        if (buildingInfoPanel != null)
        {
            buildingInfoPanel.SetActive(false);
        }

        _activeUIPanel = ActiveUIPanel.Processor;
        DisplayProcessorInfo(processor);
    }

    private void HandleDroneHubClicked(DroneHub droneHub)
    {
        if (droneHub == null) return;

        HideCurrentIClickableUI();
        
        if (buildingInfoPanel != null)
        {
            buildingInfoPanel.SetActive(false);
        }

        _activeUIPanel = ActiveUIPanel.DroneHub;
        DisplayDroneHubInfo(droneHub);
    }


    private void HandleDataExtractorClicked(DataExtractor extractor)
    {
        if (extractor == null) return;

        HideCurrentIClickableUI();

        if (buildingInfoPanel != null)
        {
            buildingInfoPanel.SetActive(false);
        }

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
        if (storageInfoPanel != null && storageInfoPanel.activeSelf) {
            if (_trackedStorage != null && _storageResourceChangedHandler != null) {
                _trackedStorage.OnResourceChanged -= _storageResourceChangedHandler;
            }
            storageInfoPanel.SetActive(false);
            _trackedStorage = null;
        }

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
        if (buildingInfoPanel != null) {
            if (GameManager.Instance.IsDragging()) return;

            if (_areaBuildingDestroyer != null && _areaBuildingDestroyer.HasMoved) {
                return;
            }

            if (_areaBuildingDestroyer != null && _areaBuildingDestroyer.JustFinishedAreaDrag) {
                return;
            }

            buildingInfoPanel.SetActive(false);
        }

        _pinnedProcessorData = null;
        if (processorInfoPanel != null) {
            processorInfoPanel.SetActive(false);
        }

        _pinnedDroneHubData = null;
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
        _pinnedDroneHubData = null;
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
        buildingNameText.text = data.displayName;
        buildingDescText.text = data.description;

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

    private void DisplayDroneHubInfo(DroneHub droneHub)
    {
        if (droneHub == null || droneHubInfoPanel == null) return;
        
        if (!buildingUIClickSound.IsNull)
        {
            RuntimeManager.PlayOneShot(buildingUIClickSound);
        }
        
        droneHubInfoPanel.gameObject.SetActive(true);
        DroneProduceUIManager.Instance.ShowDroneHubUI(droneHub);
    }

    public void HideDroneHubInfo()
    {
        if (droneHubInfoPanel == null) return;

        if (_pinnedDroneHubData == null) {
            droneHubInfoPanel.gameObject.SetActive(false);
            if (_activeUIPanel == ActiveUIPanel.DroneHub) {
                _activeUIPanel = ActiveUIPanel.None;
            }
        }
    }

    public void PinDroneHubInfo(DroneHub droneHub)
    {
        if (droneHub == null) return;

        DroneHubData data = droneHub.DroneHubData;

        if (_pinnedDroneHubData == data) {
            UnpinAndHideAllPanels();
        }
        else {
            UnpinAndHideAllPanels();
            _pinnedDroneHubData = data;
            DisplayDroneHubInfo(droneHub);
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
}



