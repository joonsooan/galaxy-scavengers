using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BuildingHoverManager : MonoBehaviour
{
    public static BuildingHoverManager Instance { get; private set; }

    private Camera _mainCamera;
    private BuildingDataHolder _currentHoveredBuilding;
    private IStorage _currentHoveredStorage;
    private ResourceNode _currentHoveredResource;
    private UnitBase _currentHoveredUnit;
    private UnitBase _lockedUnitForInfo;
    private UnitInfoPanel _unitInfoPanel;
    private bool _keepPanelVisible = false;
    private bool _panelsJustClosed = false;
    private float _panelsClosedTime = 0f;
    private const float PanelCloseCooldown = 0.1f;

    private GameObject _mouseHoverDetector;
    private BoxCollider2D _mouseDetectorCollider;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _mainCamera = Camera.main;
        _unitInfoPanel = UnitInfoPanel.Instance;
        CreateMouseHoverDetector();
    }

    private void CreateMouseHoverDetector()
    {
        _mouseHoverDetector = new GameObject("MouseHoverDetector");
        _mouseHoverDetector.tag = "MouseHoverDetector";
        _mouseHoverDetector.layer = LayerMask.NameToLayer("Ignore Raycast");
        
        _mouseDetectorCollider = _mouseHoverDetector.AddComponent<BoxCollider2D>();
        _mouseDetectorCollider.isTrigger = true;
        _mouseDetectorCollider.size = Vector2.one * 0.2f;
        
        Rigidbody2D rb = _mouseHoverDetector.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    private void Update()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        if (_mouseHoverDetector == null)
        {
            CreateMouseHoverDetector();
        }

        if (_mouseHoverDetector != null)
        {
            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;
            _mouseHoverDetector.transform.position = mouseWorldPos;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsDragging())
        {
            CardDragger dragger = GameManager.Instance.cardDragger;
            bool keepPowerPreview = dragger != null && dragger.IsDraggingBuildingCard;
            ClearHoverStateOnly(keepPowerPreview);
            return;
        }

        if (UIUtils.IsPointerOverUI() && !IsPointerOverFloatingNumText() && !IsPointerOverProgressSlider() &&
            !IsPointerOverPowerStatusWorldFollower())
        {
            ClearAllHovers();
            return;
        }

        TryStartDragFromHoveredBuildingViaShortcut();

        if (_panelsJustClosed)
        {
            _panelsClosedTime += Time.unscaledDeltaTime;
            if (_panelsClosedTime >= PanelCloseCooldown)
            {
                _panelsJustClosed = false;
                _panelsClosedTime = 0f;
            }
        }
    }


    public void OnBuildingEnter(BuildingDataHolder buildingDataHolder)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) return;
        if (buildingDataHolder == null || buildingDataHolder.buildingData == null) return;
        if (buildingDataHolder == _currentHoveredBuilding) return;

        if (_currentHoveredBuilding != null && _currentHoveredBuilding != buildingDataHolder)
        {
            ClearHover();
        }

        _currentHoveredBuilding = buildingDataHolder;
        _keepPanelVisible = false;
        ShowBuildingInfo(buildingDataHolder);
        TryShowPowerCoveragePreview(buildingDataHolder);
    }

    public void OnBuildingExit(BuildingDataHolder buildingDataHolder)
    {
        if (buildingDataHolder == _currentHoveredBuilding)
        {
            ClearPowerCoveragePreview();
            if (_keepPanelVisible)
            {
                ClearBuildingInfoButKeepPanel();
            }
            else
            {
                ClearHover();
            }
        }
    }

    public void OnStorageEnter(IStorage storage)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) return;
        if (storage == null) return;
        if (storage == _currentHoveredStorage) return;

        if (_currentHoveredStorage != null && _currentHoveredStorage != storage)
        {
            ClearStorageHover();
        }

        _currentHoveredStorage = storage;
        ShowStorageInfo(storage);
    }

    public void OnStorageExit(IStorage storage)
    {
        if (storage == _currentHoveredStorage)
        {
            ClearStorageHover();
        }
    }

    public void OnResourceEnter(ResourceNode node)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) return;
        if (node == null)
        {
            return;
        }
        if (node == _currentHoveredResource)
        {
            return;
        }
        if (_currentHoveredResource != null && _currentHoveredResource != node)
        {
            ClearResourceHover();
        }
        _currentHoveredResource = node;
        ShowResourceInfo(node);
    }

    public void OnResourceExit(ResourceNode node)
    {
        if (node == _currentHoveredResource)
        {
            ClearResourceHover();
        }
    }

    public void OnUnitEnter(UnitBase unit)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) return;
        if (unit == null)
        {
            return;
        }
        if (unit == _currentHoveredUnit)
        {
            return;
        }
        if (_currentHoveredUnit != null && _currentHoveredUnit != unit)
        {
            ClearUnitHover();
        }
        _currentHoveredUnit = unit;
        ShowUnitInfo(unit);
    }

    public void OnUnitExit(UnitBase unit)
    {
        if (_lockedUnitForInfo != null && unit == _lockedUnitForInfo)
        {
            return;
        }

        if (unit == _currentHoveredUnit)
        {
            ClearUnitHover();
        }
    }

    private void ClearAllHovers()
    {
        ClearHover();
        ClearStorageHover();
        ClearResourceHover();
        ClearUnitHover();
    }

    private void ClearHoverStateOnly(bool keepPowerCoveragePreview = false)
    {
        if (!keepPowerCoveragePreview) {
            ClearPowerCoveragePreview();
        }
        _currentHoveredBuilding = null;
        _currentHoveredStorage = null;
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.HideStorageInfo();
        }
        if (_currentHoveredResource != null)
        {
            if (ResourceInfoPanel.Instance != null)
            {
                ResourceInfoPanel.Instance.CancelPreview();
                ResourceInfoPanel.Instance.gameObject.SetActive(false);
            }
            _currentHoveredResource = null;
        }
        if (_currentHoveredUnit != null)
        {
            UnitInfoPanel unitInfoPanel = GetUnitInfoPanel();
            if (unitInfoPanel != null)
            {
                unitInfoPanel.CancelPreview();
                unitInfoPanel.gameObject.SetActive(false);
            }
            _currentHoveredUnit = null;
        }
    }


    private static void ClearPowerCoveragePreview()
    {
        PowerCoveragePreviewOverlay overlay = PowerCoveragePreviewOverlay.Instance;
        if (overlay != null) {
            overlay.Clear();
        }
    }

    private static void TryShowPowerCoveragePreview(BuildingDataHolder buildingDataHolder)
    {
        if (buildingDataHolder == null || buildingDataHolder.buildingData == null) {
            return;
        }
        if (!CardDragger.IsPowerGridPreviewBuildingType(buildingDataHolder.buildingData.buildingType)) {
            return;
        }
        PowerCoveragePreviewOverlay overlay = PowerCoveragePreviewOverlay.Instance;
        if (overlay != null) {
            overlay.Show();
        }
    }

    private void TryStartDragFromHoveredBuildingViaShortcut()
    {
        if (!Input.GetKeyDown(KeyCode.Q))
        {
            return;
        }
        if (_currentHoveredBuilding == null)
        {
            return;
        }
        BuildingData data = _currentHoveredBuilding.buildingData;
        if (data == null)
        {
            return;
        }
        if (data.buildingType == BuildingType.MainStructure)
        {
            return;
        }
        if (BuildingUnlockManager.Instance != null && !BuildingUnlockManager.Instance.IsBuildingUnlocked(data))
        {
            return;
        }
        if (GameManager.Instance == null)
        {
            return;
        }
        UIManager uiManager = GameManager.Instance.uiManager;
        if (uiManager != null)
        {
            if (uiManager.IsProcessorPanelActive())
            {
                uiManager.HideProcessorInfo();
            }
            if (uiManager.IsDroneHubPanelActive())
            {
                uiManager.HideDroneHubInfo();
            }
        }
        GameManager.Instance.StartDrag(data);
        if (BuildingInfoPanel.Instance != null)
        {
            BuildingInfoPanel.Instance.SelectBuilding(data);
        }
    }

    private void ShowBuildingInfo(BuildingDataHolder buildingDataHolder)
    {
        if (BuildingInfoPanel.Instance == null) return;
        if (buildingDataHolder.buildingData == null) return;

        if (IsProcessorOrDroneHubPanelActive() || _panelsJustClosed)
        {
            return;
        }

        BuildingInfoPanel.Instance.gameObject.SetActive(true);
        Damageable damageable = buildingDataHolder.GetDamageable();
        IElectricityConsumer runtimeConsumer = GetElectricityConsumer(buildingDataHolder);
        BuildingInfoPanel.Instance.PreviewInfo(buildingDataHolder.buildingData, damageable, false, true, runtimeConsumer);
    }

    private bool IsProcessorOrDroneHubPanelActive()
    {
        if (GameManager.Instance == null || GameManager.Instance.uiManager == null)
        {
            return false;
        }

        UIManager uiManager = GameManager.Instance.uiManager;
        
        if (uiManager.IsProcessorPanelActive())
        {
            return true;
        }

        if (uiManager.IsDroneHubPanelActive())
        {
            return true;
        }

        return false;
    }

    private bool IsPointerOverFloatingNumText()
    {
        if (EventSystem.current == null) return false;

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject != null && result.gameObject.GetComponent<FloatingNumText>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointerOverPowerStatusWorldFollower()
    {
        if (EventSystem.current == null) {
            return false;
        }

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);

        foreach (RaycastResult result in results) {
            if (result.gameObject == null) {
                continue;
            }
            if (result.gameObject.GetComponentInParent<PowerStatusWorldFollower>() != null) {
                return true;
            }
        }

        return false;
    }

    private bool IsPointerOverProgressSlider()
    {
        if (EventSystem.current == null) return false;

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject != null)
            {
                ProductionProgressSlider slider = result.gameObject.GetComponent<ProductionProgressSlider>();
                if (slider == null)
                {
                    slider = result.gameObject.GetComponentInParent<ProductionProgressSlider>();
                }
                if (slider == null)
                {
                    slider = result.gameObject.GetComponentInChildren<ProductionProgressSlider>();
                }
                if (slider != null)
                {
                    return true;
                }
                
                Transform current = result.gameObject.transform;
                while (current != null)
                {
                    if (current.GetComponent<ProductionProgressSlider>() != null)
                    {
                        return true;
                    }
                    current = current.parent;
                }
            }
        }

        return false;
    }

    private void ClearHover()
    {
        ClearPowerCoveragePreview();
        if (_currentHoveredBuilding != null)
        {
            if (BuildingInfoPanel.Instance != null)
            {
                BuildingInfoPanel.Instance.CancelPreview();
                BuildingInfoPanel.Instance.gameObject.SetActive(false);
            }
            _currentHoveredBuilding = null;
        }
    }

    private void ClearBuildingInfoButKeepPanel()
    {
        ClearPowerCoveragePreview();
        if (_currentHoveredBuilding != null)
        {
            if (BuildingInfoPanel.Instance != null)
            {
                BuildingInfoPanel.Instance.CancelPreview();
            }
            _currentHoveredBuilding = null;
        }
    }

    private void ShowStorageInfo(IStorage storage)
    {
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.DisplayStorageInfo(storage);
        }
    }

    private void ClearStorageHover()
    {
        if (_currentHoveredStorage != null)
        {
            if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
            {
                GameManager.Instance.uiManager.HideStorageInfo();
            }
            _currentHoveredStorage = null;
        }
    }

    private void ShowResourceInfo(ResourceNode node)
    {
        if (ResourceInfoPanel.Instance == null)
        {
            return;
        }
        if (node == null || node.IsDepleted)
        {
            return;
        }
        if (!node.gameObject.activeInHierarchy)
        {
            return;
        }
        if (FogOfWarManager.Instance != null && FogOfWarManager.Instance.IsInitialized &&
            !FogOfWarManager.Instance.CanSeeResources(node.cellPosition))
        {
            return;
        }
        if (IsProcessorOrDroneHubPanelActive() || _panelsJustClosed)
        {
            return;
        }
        ResourceInfoPanel.Instance.gameObject.SetActive(true);
        ResourceInfoPanel.Instance.PreviewInfo(node);
    }

    private void ClearResourceHover()
    {
        if (_currentHoveredResource != null)
        {
            if (ResourceInfoPanel.Instance != null)
            {
                ResourceInfoPanel.Instance.CancelPreview();
                ResourceInfoPanel.Instance.gameObject.SetActive(false);
            }
            _currentHoveredResource = null;
        }
    }

    private void ShowUnitInfo(UnitBase unit)
    {
        UnitInfoPanel unitInfoPanel = GetUnitInfoPanel();
        if (unitInfoPanel == null)
        {
            return;
        }
        if (unit == null)
        {
            return;
        }
        if (!unit.gameObject.activeInHierarchy)
        {
            return;
        }
        if (unit.CurrentHealth <= 0)
        {
            return;
        }
        if (_panelsJustClosed)
        {
            return;
        }
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null && GameManager.Instance.uiManager.IsProcessorPanelActive())
        {
            return;
        }
        unitInfoPanel.gameObject.SetActive(true);
        unitInfoPanel.PreviewInfo(unit);
    }

    public void LockUnitInfo(UnitBase unit)
    {
        if (unit == null || !unit.gameObject.activeInHierarchy || unit.CurrentHealth <= 0)
        {
            return;
        }

        _lockedUnitForInfo = unit;
        _currentHoveredUnit = unit;
        ShowUnitInfo(unit);
    }

    public void UnlockUnitInfo()
    {
        _lockedUnitForInfo = null;
    }

    public bool IsUnitInfoLocked()
    {
        return _lockedUnitForInfo != null;
    }

    public void ClearLockedUnitInfo()
    {
        _lockedUnitForInfo = null;

        if (_currentHoveredUnit == null)
        {
            return;
        }

        UnitInfoPanel unitInfoPanel = GetUnitInfoPanel();
        if (unitInfoPanel != null)
        {
            unitInfoPanel.CancelPreview();
            unitInfoPanel.gameObject.SetActive(false);
        }

        _currentHoveredUnit = null;
    }

    private void ClearUnitHover()
    {
        if (_lockedUnitForInfo != null)
        {
            if (!_lockedUnitForInfo.gameObject.activeInHierarchy || _lockedUnitForInfo.CurrentHealth <= 0)
            {
                _lockedUnitForInfo = null;
            }
            else
            {
                _currentHoveredUnit = _lockedUnitForInfo;
                ShowUnitInfo(_lockedUnitForInfo);
                return;
            }
        }

        if (_currentHoveredUnit != null)
        {
            UnitInfoPanel unitInfoPanel = GetUnitInfoPanel();
            if (unitInfoPanel != null)
            {
                unitInfoPanel.CancelPreview();
                unitInfoPanel.gameObject.SetActive(false);
            }
            _currentHoveredUnit = null;
        }
    }

    private UnitInfoPanel GetUnitInfoPanel()
    {
        if (_unitInfoPanel == null)
            _unitInfoPanel = UnitInfoPanel.Instance;
        if (_unitInfoPanel == null)
            _unitInfoPanel = FindFirstObjectByType<UnitInfoPanel>();
        return _unitInfoPanel;
    }

    public void ClearHoverOnClick()
    {
        UnlockUnitInfo();

        bool panelsActive = IsProcessorOrDroneHubPanelActive();
        if (!panelsActive && _currentHoveredBuilding != null)
        {
            _keepPanelVisible = true;
        }

        ClearHover();
        ClearStorageHover();
    }

    public void HandleNormalBuildingClick(BuildingDataHolder holder)
    {
        UnlockUnitInfo();
        ClearStorageHover();
        if (holder == null || holder.buildingData == null)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.HideProcessorAndDroneHubPanels();
        }

        _currentHoveredBuilding = holder;
        _keepPanelVisible = true;
        if (BuildingInfoPanel.Instance != null)
        {
            BuildingInfoPanel.Instance.gameObject.SetActive(true);
            Damageable damageable = holder.GetDamageable();
            IElectricityConsumer runtimeConsumer = GetElectricityConsumer(holder);
            BuildingInfoPanel.Instance.PreviewInfo(holder.buildingData, damageable, false, true, runtimeConsumer);
        }
    }

    private static IElectricityConsumer GetElectricityConsumer(BuildingDataHolder holder)
    {
        if (holder == null)
        {
            return null;
        }

        IElectricityConsumer consumer = holder.GetComponent<IElectricityConsumer>();
        if (consumer == null)
        {
            consumer = holder.GetComponentInChildren<IElectricityConsumer>();
        }

        return consumer;
    }

    public void NotifyPanelsClosed()
    {
        _panelsJustClosed = true;
        _panelsClosedTime = 0f;
    }

    private void OnDisable()
    {
        ClearAllHovers();
    }

    private void OnDestroy()
    {
        if (_mouseHoverDetector != null)
        {
            Destroy(_mouseHoverDetector);
        }
    }
}

