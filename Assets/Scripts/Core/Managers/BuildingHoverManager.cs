using UnityEngine;
using System.Collections.Generic;

public class BuildingHoverManager : MonoBehaviour
{
    public static BuildingHoverManager Instance { get; private set; }

    private Camera _mainCamera;
    private BuildingDataHolder _currentHoveredBuilding;
    private IStorage _currentHoveredStorage;
    private IStorage _pinnedStorage;
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

        if (GameManager.Instance != null && GameManager.Instance.IsDragging())
        {
            CardDragger dragger = GameManager.Instance.cardDragger;
            bool keepPowerPreview = dragger != null && dragger.IsDraggingBuildingCard;
            ClearHoverStateOnly(keepPowerPreview);
            if (_mouseHoverDetector != null)
            {
                Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorldPos.z = 0f;
                _mouseHoverDetector.transform.position = mouseWorldPos;
            }
            return;
        }

        if (UIUtils.IsPointerOverUI())
        {
            UIManager uiManager = GameManager.Instance != null ? GameManager.Instance.uiManager : null;
            bool overStoragePanel = uiManager != null && uiManager.IsPointerOverStorageInfoPanel();
            bool hasStorageActive = _pinnedStorage != null || (_currentHoveredStorage != null && overStoragePanel);

            ClearHover();
            ClearResourceHover();
            ClearUnitHover();
            if (!hasStorageActive)
            {
                ClearStorageHover();
            }

            if (_mouseDetectorCollider != null)
            {
                _mouseDetectorCollider.enabled = false;
            }
            return;
        }

        if (_mouseDetectorCollider != null)
        {
            _mouseDetectorCollider.enabled = true;
        }

        if (_mouseHoverDetector != null)
        {
            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;
            _mouseHoverDetector.transform.position = mouseWorldPos;
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

        if (_currentHoveredStorage != null && _pinnedStorage == null)
        {
            UIManager uiManager = GameManager.Instance != null ? GameManager.Instance.uiManager : null;
            bool overStoragePanel = uiManager != null && uiManager.IsPointerOverStorageInfoPanel();
            if (!overStoragePanel && !IsPointerOverStorage(_currentHoveredStorage))
            {
                ClearStorageHover();
            }
        }
    }


    public void OnBuildingEnter(BuildingDataHolder buildingDataHolder)
    {
        if (UIUtils.IsPointerOverUI()) return;
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) return;
        if (buildingDataHolder == null || buildingDataHolder.buildingData == null) return;
        if (buildingDataHolder == _currentHoveredBuilding) return;

        if (_currentHoveredBuilding != null && _currentHoveredBuilding != buildingDataHolder)
        {
            ClearHover();
        }

        _currentHoveredBuilding = buildingDataHolder;
        _keepPanelVisible = false;
        TargetBracketEffect.Show(buildingDataHolder.transform);
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
            else if (_pinnedStorage != null && IsPinnedStorageBuilding(buildingDataHolder))
            {
                _currentHoveredBuilding = null;
            }
            else
            {
                ClearHover();
            }
        }
    }

    public void OnStorageEnter(IStorage storage)
    {
        if (UIUtils.IsPointerOverUI()) return;
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) return;
        if (storage == null) return;
        if (AreSameStorage(_currentHoveredStorage, storage)) return;

        if (_currentHoveredStorage != null && !AreSameStorage(_currentHoveredStorage, storage))
        {
            ClearStorageHover();
        }

        _currentHoveredStorage = storage;
        ShowStorageInfo(storage);
    }

    public void OnStorageExit(IStorage storage)
    {
        if (AreSameStorage(_currentHoveredStorage, storage))
        {
            if (AreSameStorage(_pinnedStorage, storage))
            {
                _currentHoveredStorage = null;
                return;
            }
            UIManager uiManager = GameManager.Instance != null ? GameManager.Instance.uiManager : null;
            if (uiManager != null && uiManager.IsPointerOverStorageInfoPanel())
            {
                return;
            }
            ClearStorageHover();
        }
    }

    public void OnStorageClick(IStorage storage)
    {
        if (storage == null) return;
        if (AreSameStorage(_pinnedStorage, storage))
        {
            return;
        }
        ClearPinnedStorage();
        _pinnedStorage = storage;
        _currentHoveredStorage = storage;
        Component storageComponent = storage as Component;
        if (storageComponent != null)
            TargetBracketEffect.Show(storageComponent.transform);
        ShowStorageInfo(storage);
    }

    public void ClearPinnedStorage()
    {
        if (_pinnedStorage == null) return;
        _pinnedStorage = null;
        _currentHoveredStorage = null;
        TargetBracketEffect.Hide();
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
            GameManager.Instance.uiManager.HideStorageInfo();
    }

    public bool IsStoragePinned() => _pinnedStorage != null;

    public void OnResourceEnter(ResourceNode node)
    {
        if (UIUtils.IsPointerOverUI()) return;
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
        if (UIUtils.IsPointerOverUI()) return;
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
        if (_currentHoveredBuilding != null)
        {
            TargetBracketEffect.Hide();
        }
        _currentHoveredBuilding = null;
        _currentHoveredStorage = null;
        if (_pinnedStorage != null)
        {
            _pinnedStorage = null;
            TargetBracketEffect.Hide();
        }
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

    private void ClearHover()
    {
        ClearPowerCoveragePreview();
        if (_currentHoveredBuilding != null)
        {
            if (!IsPinnedStorageBuilding(_currentHoveredBuilding))
            {
                TargetBracketEffect.Hide();
            }
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
            TargetBracketEffect.Hide();
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
            if (_pinnedStorage == null)
            {
                if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
                {
                    GameManager.Instance.uiManager.HideStorageInfo();
                }
            }
            else
            {
                if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
                {
                    GameManager.Instance.uiManager.DisplayStorageInfo(_pinnedStorage);
                }
            }
            _currentHoveredStorage = null;
        }
    }

    private bool IsPinnedStorageBuilding(BuildingDataHolder holder)
    {
        if (holder == null || _pinnedStorage == null) return false;
        Component c = _pinnedStorage as Component;
        return c != null && c.gameObject == holder.gameObject;
    }

    private bool AreSameStorage(IStorage a, IStorage b)
    {
        Component compA = a as Component;
        Component compB = b as Component;
        
        if (compA == null && compB == null) return true;
        if (compA == null || compB == null) return false;
        
        return compA.gameObject == compB.gameObject;
    }

    private bool IsPointerOverStorage(IStorage storage)
    {
        if (UIUtils.IsPointerOverUI()) return false;
        if (storage == null) return false;
        Component storageComp = storage as Component;
        if (storageComp == null) return false;

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return false;
        }

        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mouseWorldPos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

        Collider2D[] colliders = storageComp.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in colliders)
        {
            if (col == null || !col.enabled) continue;

            if (col.OverlapPoint(mouseWorldPos2D))
            {
                return true;
            }

            if (_mouseDetectorCollider != null && _mouseDetectorCollider.enabled && col.IsTouching(_mouseDetectorCollider))
            {
                return true;
            }
        }

        return false;
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
            !FogOfWarManager.Instance.CanSeeResources(node.cellPosition) &&
            !ShouldBypassTutorialResourceFogForHover())
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
        if (IsProcessorOrDroneHubPanelActive())
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

    public void ClearHoverOnClick(bool clearStorageHover = true)
    {
        UnlockUnitInfo();

        bool panelsActive = IsProcessorOrDroneHubPanelActive();
        if (!panelsActive && _currentHoveredBuilding != null)
        {
            _keepPanelVisible = true;
        }

        ClearHover();
        if (clearStorageHover) {
            ClearStorageHover();
        }
    }

    public void HandleNormalBuildingClick(BuildingDataHolder holder)
    {
        ClearPinnedStorage();
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

    private static bool ShouldBypassTutorialResourceFogForHover()
    {
        TutorialManager mgr = TutorialManager.Instance;
        if (mgr == null || !mgr.IsTutorialActive())
        {
            return false;
        }

        return mgr.IsUIPanelEnabledForCurrentStep(TutorialUIPanel.ResourceInfoPanel);
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

