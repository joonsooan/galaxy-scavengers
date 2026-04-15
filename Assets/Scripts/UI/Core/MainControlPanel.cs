using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-100)]
public class MainControlPanel : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button baseBuildingBtn;
    [SerializeField] private Button processorBtn;
    [SerializeField] private Button resourceStatBtn;
    [SerializeField] private Button resourceStatCloseBtn;
    [SerializeField] private Button unitManagementBtn;

    [Header("UI Panels")]
    [SerializeField] private GameObject buildingInfoPanel;
    [SerializeField] private GameObject baseBuildingPanel;
    [SerializeField] private GameObject resourceStatPanel;
    [SerializeField] private GameObject unitManagementPanelRoot;
    [SerializeField] private ResourceStatsUIController resourceStatsUIController;
    
    private GameObject _currentlyActivePanel;
    private BuildingInfoPanel _buildingInfoPanelComponent;
    
    private void OnEnable()
    {
        MainStructure.OnDroneProducePanelClicked += HideBuildingInfoPanelMainStructure;
        Processor.OnProcessorClicked += HideBuildingInfoPanel;
        DataExtractor.OnDataExtractorClicked += HideBuildingInfoPanelExtractor;
    }

    private void OnDisable()
    {
        MainStructure.OnDroneProducePanelClicked -= HideBuildingInfoPanelMainStructure;
        Processor.OnProcessorClicked -= HideBuildingInfoPanel;
        DataExtractor.OnDataExtractorClicked -= HideBuildingInfoPanelExtractor;
    }
    
    
    private AreaBuildingDestroyer _areaBuildingDestroyer;
    
    private void Start()
    {
        _buildingInfoPanelComponent =  buildingInfoPanel.GetComponent<BuildingInfoPanel>();
        _areaBuildingDestroyer = FindFirstObjectByType<AreaBuildingDestroyer>();
        
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

        if (resourceStatCloseBtn != null)
        {
            resourceStatCloseBtn.onClick.AddListener(OnResourceStatCloseBtnClicked);
        }

        if (unitManagementBtn != null)
        {
            unitManagementBtn.onClick.AddListener(OnUnitManagementBtnClicked);
        }

        if (resourceStatsUIController == null && resourceStatPanel != null)
        {
            resourceStatsUIController = resourceStatPanel.GetComponentInChildren<ResourceStatsUIController>(true);
        }
        
        HideAllPanels();
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
            PlayShortcutClickSound(unitManagementBtn);
            OnUnitManagementBtnClicked();
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            PlayShortcutClickSound(resourceStatBtn);
            OnResourceStatBtnClicked();
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

    private void OnBaseBuildingBtnClicked()
    {
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.DisableHighlightForTarget(baseBuildingBtn.gameObject);
        }

        GameManager.Instance.uiManager.UnpinAndHideAllPanels();
        buildingInfoPanel.SetActive(true);
        ShowPanel(baseBuildingPanel);
    }

    private void OnProcessorBtnClicked()
    {
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.DisableHighlightForTarget(processorBtn.gameObject);
        }

        GameManager.Instance.uiManager.UnpinAndHideAllPanels();
        buildingInfoPanel.SetActive(true);
    }

    private void OnResourceStatBtnClicked()
    {
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.DisableHighlightForTarget(resourceStatBtn.gameObject);
        }

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
        if (TutorialManager.Instance != null && unitManagementBtn != null)
        {
            TutorialManager.Instance.DisableHighlightForTarget(unitManagementBtn.gameObject);
        }

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

    private void OnResourceStatCloseBtnClicked()
    {
        CloseResourceStatPanel();
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

        if (resourceStatCloseBtn != null)
        {
            resourceStatCloseBtn.onClick.RemoveListener(OnResourceStatCloseBtnClicked);
        }

        if (unitManagementBtn != null)
        {
            unitManagementBtn.onClick.RemoveListener(OnUnitManagementBtnClicked);
        }
    }
}

