using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-100)]
public class MainControlPanel : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button baseBuildingBtn;
    [SerializeField] private Button processorBtn;
    [SerializeField] private Button resourceStatBtn;
    [SerializeField] private Button unitManagementBtn;
    [SerializeField] private Button researchBtn;

    [Header("Research Complete Highlight")]
    [SerializeField] private Material researchCompletedHighlightMaterial;

    [Header("UI Panels")]
    [SerializeField] private GameObject buildingInfoPanel;
    [SerializeField] private GameObject baseBuildingPanel;
    [SerializeField] private GameObject resourceStatPanel;
    [SerializeField] private GameObject unitManagementPanelRoot;
    [SerializeField] private GameObject researchPanel;
    [SerializeField] private ResourceStatsUIController resourceStatsUIController;
    
    private GameObject _currentlyActivePanel;
    private BuildingInfoPanel _buildingInfoPanelComponent;
    private Image _researchBtnImage;
    private Material _researchBtnOriginalMaterial;
    
    private void OnEnable()
    {
        MainStructure.OnDroneProducePanelClicked += HideBuildingInfoPanelMainStructure;
        Processor.OnProcessorClicked += HideBuildingInfoPanel;
        DataExtractor.OnDataExtractorClicked += HideBuildingInfoPanelExtractor;
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        TechResearchManager.OnResearchCompleted += OnTechResearchCompleted;
        GameManager.OnGameSceneInitialized += OnGameSceneInitialized;
        ApplyLocalizedStaticTexts();
    }

    private void OnDisable()
    {
        MainStructure.OnDroneProducePanelClicked -= HideBuildingInfoPanelMainStructure;
        Processor.OnProcessorClicked -= HideBuildingInfoPanel;
        DataExtractor.OnDataExtractorClicked -= HideBuildingInfoPanelExtractor;
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        TechResearchManager.OnResearchCompleted -= OnTechResearchCompleted;
        GameManager.OnGameSceneInitialized -= OnGameSceneInitialized;
    }
    
    
    private AreaBuildingDestroyer _areaBuildingDestroyer;
    
    private void Start()
    {
        _buildingInfoPanelComponent = buildingInfoPanel.GetComponent<BuildingInfoPanel>();
        _areaBuildingDestroyer = FindFirstObjectByType<AreaBuildingDestroyer>();

        if (researchBtn != null)
        {
            _researchBtnImage = researchBtn.GetComponent<Image>();
            if (_researchBtnImage != null)
                _researchBtnOriginalMaterial = _researchBtnImage.material;
        }
        
        if (baseBuildingBtn != null)
        {
            baseBuildingBtn.onClick.AddListener(OnBaseBuildingBtnClicked);
        }
        
        if (processorBtn != null)
        {
            processorBtn.onClick.AddListener(OnProcessorBtnClicked);
        }
        
        if (resourceStatBtn != null)
        {
            resourceStatBtn.onClick.AddListener(OnResourceStatBtnClicked);
        }

        if (unitManagementBtn != null)
        {
            unitManagementBtn.onClick.AddListener(OnUnitManagementBtnClicked);
        }

        if (researchBtn != null)
        {
            researchBtn.onClick.AddListener(OnResearchBtnClicked);
        }

        if (resourceStatsUIController == null && resourceStatPanel != null)
        {
            resourceStatsUIController = resourceStatPanel.GetComponentInChildren<ResourceStatsUIController>(true);
        }

        ApplyLocalizedStaticTexts();
        
        HideAllPanels();
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyLocalizedStaticTexts();
    }

    public void ApplyPassiveLocaleRefresh()
    {
        ApplyLocalizedStaticTexts();
    }

    private void ApplyLocalizedStaticTexts()
    {
        SetButtonLabel(baseBuildingBtn, "game.baseBuilding", "기지 건설");
        SetButtonLabel(unitManagementBtn, "game.unitManage", "유닛 관리");
        SetButtonLabel(resourceStatBtn, "game.resourceStats", "자원 통계");

        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_Text text in texts)
        {
            if (text == null)
            {
                continue;
            }

            if (text.gameObject.name == "Zone Text")
            {
                if (HasAncestorNamed(text.transform, "Left Time Panel"))
                {
                    text.text = GameLocalization.GetOrDefault("UI_Common", "game.leftTime", "남은 시간");
                }
                else if (HasAncestorNamed(text.transform, "Noise Panel"))
                {
                    text.text = GameLocalization.GetOrDefault("UI_Common", "game.noise", "소음");
                }
            }

            SetTextIfUnderNamedHierarchy(text, "Base Building Btn", "game.baseBuilding", "기지 건설");
            SetTextIfUnderNamedHierarchy(text, "Unit Manage Btn", "game.unitManage", "유닛 관리");
            SetTextIfUnderNamedHierarchy(text, "Resource Stats Btn", "game.resourceStats", "자원 통계");
        }
    }

    private static void SetButtonLabel(Button button, string key, string fallback)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = GameLocalization.GetOrDefault("UI_Common", key, fallback);
        }
    }

    private static void SetTextIfUnderNamedHierarchy(TMP_Text text, string ancestorName, string key, string fallback)
    {
        if (text != null && HasAncestorNamed(text.transform, ancestorName))
        {
            text.text = GameLocalization.GetOrDefault("UI_Common", key, fallback);
        }
    }

    private static bool HasAncestorNamed(Transform transform, string ancestorName)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == ancestorName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
    
    private void Update()
    {
        if (IsLoadingScreenActive())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            PlayShortcutClickSound(baseBuildingBtn);
            OnBaseBuildingBtnClicked();
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            if (TutorialManager.Instance != null &&
                !TutorialManager.Instance.IsUIPanelEnabledForCurrentStep(TutorialUIPanel.UnitManagePanel)) {
                return;
            }
            PlayShortcutClickSound(unitManagementBtn);
            OnUnitManagementBtnClicked();
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            if (TutorialManager.Instance != null &&
                !TutorialManager.Instance.IsUIPanelEnabledForCurrentStep(TutorialUIPanel.ResourceStatsPanel)) {
                return;
            }
            PlayShortcutClickSound(resourceStatBtn);
            OnResourceStatBtnClicked();
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            OnResearchBtnClicked();
        }

        if (IsResourceStatPanelActive())
        {
            if (Input.GetMouseButtonUp(1))
            {
                CloseResourceStatPanel();
                return;
            }
        }

        if (IsUnitManagementPanelActive())
        {
            if (Input.GetMouseButtonUp(1))
            {
                CloseUnitManagementPanel();
                return;
            }
        }

        if (Input.GetMouseButtonUp(1))
        {
            if (BuildingHoverManager.Instance != null && BuildingHoverManager.Instance.IsUnitInfoLocked())
            {
                BuildingHoverManager.Instance.ClearLockedUnitInfo();
                return;
            }

            if (UIUtils.IsPointerOverUI())
            {
                return;
            }
            
            if (_areaBuildingDestroyer != null && _areaBuildingDestroyer.HasMoved)
            {
                return;
            }
            
            if (_areaBuildingDestroyer != null && _areaBuildingDestroyer.JustFinishedAreaDrag)
            {
                return;
            }
            
            if (GameManager.Instance != null && GameManager.Instance.IsDragging()) {
                _buildingInfoPanelComponent.ClearAllInfo();
                return;
            }
            
            HideAllPanels();
            _buildingInfoPanelComponent.ClearAllInfo();
        }
    }

    public void OpenResearchPanel()
    {
        if (!IsResearchPanelActive())
            OnResearchBtnClicked();
    }

    public void CloseResearchPanel()
    {
        if (IsResearchPanelActive())
            OnResearchBtnClicked();
    }

    public void ToggleResearchPanel()
    {
        OnResearchBtnClicked();
    }

    private void OnResearchBtnClicked()
    {
        DisableResearchButtonHighlight();

        if (IsResearchPanelActive())
        {
            researchPanel.SetActive(false);
            if (_currentlyActivePanel == researchPanel)
                _currentlyActivePanel = null;
            return;
        }

        HideAllPanels();
        if (_buildingInfoPanelComponent != null)
            _buildingInfoPanelComponent.ClearAllInfo();
        if (buildingInfoPanel != null)
            buildingInfoPanel.SetActive(false);

        researchPanel.SetActive(true);
        _currentlyActivePanel = researchPanel;
    }

    private void OnTechResearchCompleted(int techIndex)
    {
        EnableResearchButtonHighlight();
    }

    private void OnGameSceneInitialized()
    {
        if (baseBuildingPanel == null) return;

        BuildingButton[] buttons = baseBuildingPanel.GetComponentsInChildren<BuildingButton>(true);
        foreach (BuildingButton btn in buttons)
        {
            btn.RefreshUnlockStatus();
        }
    }

    private void EnableResearchButtonHighlight()
    {
        if (_researchBtnImage == null || researchCompletedHighlightMaterial == null) return;
        _researchBtnImage.material = researchCompletedHighlightMaterial;
    }

    private void DisableResearchButtonHighlight()
    {
        if (_researchBtnImage == null) return;
        _researchBtnImage.material = _researchBtnOriginalMaterial;
    }

    private void OnBaseBuildingBtnClicked()
    {
        GameManager.Instance.uiManager.UnpinAndHideAllPanels();
        buildingInfoPanel.SetActive(true);
        ShowPanel(baseBuildingPanel);
    }

    private void OnProcessorBtnClicked()
    {
        GameManager.Instance.uiManager.UnpinAndHideAllPanels();
        buildingInfoPanel.SetActive(true);
    }

    private void OnResourceStatBtnClicked()
    {
        if (IsResourceStatPanelActive())
        {
            CloseResourceStatPanel();
            return;
        }

        HideAllPanels();
        if (_buildingInfoPanelComponent != null)
        {
            _buildingInfoPanelComponent.ClearAllInfo();
        }
        if (buildingInfoPanel != null)
        {
            buildingInfoPanel.SetActive(false);
        }
        ShowPanel(resourceStatPanel);
        resourceStatsUIController?.Refresh();
    }

    private void OnUnitManagementBtnClicked()
    {
        if (IsUnitManagementPanelActive())
        {
            CloseUnitManagementPanel();
            return;
        }

        HideAllPanels();
        if (_buildingInfoPanelComponent != null)
        {
            _buildingInfoPanelComponent.ClearAllInfo();
        }

        if (buildingInfoPanel != null)
        {
            buildingInfoPanel.SetActive(false);
        }

        ShowPanel(unitManagementPanelRoot);
    }


    private void HideBuildingInfoPanelExtractor(DataExtractor _)
    {
        if (buildingInfoPanel != null)
        {
            buildingInfoPanel.SetActive(false);
        }
    }
    private void HideBuildingInfoPanel(Damageable _)
    {
        buildingInfoPanel.SetActive(false);
    }

    private void HideBuildingInfoPanelMainStructure(MainStructure _)
    {
        if (buildingInfoPanel != null) {
            buildingInfoPanel.SetActive(false);
        }
    }
    
    private void ShowPanel(GameObject panel)
    {
        if (panel == null) return;
        
        // ?꾩옱 蹂댁뿬二쇨퀬 ?덈뒗 ?먮꽟怨??ㅻⅨ 踰꾪듉 ?대┃ ??蹂댁뿬二쇰뜕 ?먮꽟 鍮꾪솢?깊솕
        if (_currentlyActivePanel != null && _currentlyActivePanel != panel)
        {
            _buildingInfoPanelComponent.ClearInfo();
            _currentlyActivePanel.SetActive(false);
        }
        
        // 媛숈? 踰꾪듉 ?대┃ ???먮꽟 ?④?
        if (_currentlyActivePanel == panel)
        {
            panel.SetActive(false);
            _buildingInfoPanelComponent.ClearInfo();
            buildingInfoPanel.SetActive(false);
            _currentlyActivePanel = null;
        }
        // _currentlyActivePanel = null ????踰꾪듉 ?대┃ ??蹂댁뿬以?
        else
        {
            panel.SetActive(true);
            _currentlyActivePanel = panel;
        }
    }
    
    public void HideAllPanels()
    {
        if (baseBuildingPanel != null)
        {
            baseBuildingPanel.SetActive(false);
        }

        if (resourceStatPanel != null)
        {
            resourceStatPanel.SetActive(false);
        }

        if (unitManagementPanelRoot != null)
        {
            unitManagementPanelRoot.SetActive(false);
        }

        if (researchPanel != null)
        {
            researchPanel.SetActive(false);
        }

        _currentlyActivePanel = null;
    }

    public void CloseResourceStatPanel()
    {
        if (resourceStatPanel != null)
        {
            resourceStatPanel.SetActive(false);
        }

        if (_currentlyActivePanel == resourceStatPanel)
        {
            _currentlyActivePanel = null;
        }
    }

    private void PlayShortcutClickSound(Button button)
    {
        if (button == null)
        {
            return;
        }

        FMODUIButton fmodButton = button.GetComponent<FMODUIButton>();
        if (fmodButton == null)
        {
            return;
        }

        PointerEventData eventData = EventSystem.current != null ? new PointerEventData(EventSystem.current) : null;
        fmodButton.OnPointerUp(eventData);
    }

    public void ShowResourceStatPanelForTutorial()
    {
        if (resourceStatPanel != null)
        {
            if (baseBuildingPanel != null) baseBuildingPanel.SetActive(false);
            if (_buildingInfoPanelComponent != null)
            {
                _buildingInfoPanelComponent.ClearAllInfo();
            }
            if (buildingInfoPanel != null)
            {
                buildingInfoPanel.SetActive(false);
            }
            resourceStatPanel.SetActive(true);
            _currentlyActivePanel = resourceStatPanel;
            if (resourceStatsUIController == null)
            {
                resourceStatsUIController = resourceStatPanel.GetComponentInChildren<ResourceStatsUIController>(true);
            }
            resourceStatsUIController?.Refresh();
        }

        if (unitManagementPanelRoot != null)
        {
            unitManagementPanelRoot.SetActive(false);
        }
    }

    public bool IsResourceStatPanelActive()
    {
        return resourceStatPanel != null && resourceStatPanel.activeSelf;
    }

    public void CloseUnitManagementPanel()
    {
        if (unitManagementPanelRoot != null)
        {
            unitManagementPanelRoot.SetActive(false);
        }

        if (_currentlyActivePanel == unitManagementPanelRoot)
        {
            _currentlyActivePanel = null;
        }
    }

    public bool IsUnitManagementPanelActive()
    {
        return unitManagementPanelRoot != null && unitManagementPanelRoot.activeSelf;
    }

    public bool IsResearchPanelActive()
    {
        return researchPanel != null && researchPanel.activeSelf;
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null)
        {
            return false;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        if (loadingScreen == null)
        {
            return false;
        }

        return loadingScreen.gameObject.activeSelf;
    }
    
    private void OnDestroy()
    {
        if (baseBuildingBtn != null)
        {
            baseBuildingBtn.onClick.RemoveListener(OnBaseBuildingBtnClicked);
        }
        
        if (processorBtn != null)
        {
            processorBtn.onClick.RemoveListener(OnProcessorBtnClicked);
        }
        
        if (resourceStatBtn != null)
        {
            resourceStatBtn.onClick.RemoveListener(OnResourceStatBtnClicked);
        }

        if (unitManagementBtn != null)
        {
            unitManagementBtn.onClick.RemoveListener(OnUnitManagementBtnClicked);
        }

        if (researchBtn != null)
        {
            researchBtn.onClick.RemoveListener(OnResearchBtnClicked);
        }
    }
}

