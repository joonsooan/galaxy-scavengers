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

    [Header("UI Panels")]
    [SerializeField] private GameObject buildingInfoPanel;
    [SerializeField] private GameObject baseBuildingPanel;
    [SerializeField] private GameObject processorPanel;
    [SerializeField] private GameObject resourceStatPanel;
    
    private GameObject _currentlyActivePanel;
    private BuildingInfoPanel _buildingInfoPanelComponent;
    
    private void OnEnable()
    {
        DroneHub.OnDroneHubClicked += HideBuildingInfoPanel;
        Processor.OnProcessorClicked += HideBuildingInfoPanel;
    }

    private void OnDisable()
    {
        DroneHub.OnDroneHubClicked -= HideBuildingInfoPanel;
        Processor.OnProcessorClicked -= HideBuildingInfoPanel;
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
        
        HideAllPanels();
    }
    
    private void Update()
    {
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
        ShowPanel(processorPanel);
    }

    private void OnResourceStatBtnClicked()
    {
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.DisableHighlightForTarget(resourceStatBtn.gameObject);
        }

        HideAllPanels();
        ShowPanel(resourceStatPanel);
    }

    private void HideBuildingInfoPanel(Damageable _)
    {
        buildingInfoPanel.SetActive(false);
    }
    
    private void ShowPanel(GameObject panel)
    {
        if (panel == null) return;
        
        // 현재 보여주고 있는 판넬과 다른 버튼 클릭 시 보여주던 판넬 비활성화
        if (_currentlyActivePanel != null && _currentlyActivePanel != panel)
        {
            _buildingInfoPanelComponent.ClearInfo();
            _currentlyActivePanel.SetActive(false);
        }
        
        // 같은 버튼 클릭 시 판넬 숨김
        if (_currentlyActivePanel == panel)
        {
            panel.SetActive(false);
            _buildingInfoPanelComponent.ClearInfo();
            buildingInfoPanel.SetActive(false);
            _currentlyActivePanel = null;
        }
        // _currentlyActivePanel = null 일 때 버튼 클릭 시 보여줌
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
        
        if (processorPanel != null)
        {
            processorPanel.SetActive(false);
        }
        
        if (resourceStatPanel != null)
        {
            resourceStatPanel.SetActive(false);
        }
        
        _currentlyActivePanel = null;
    }

    public void ShowResourceStatPanelForTutorial()
    {
        if (resourceStatPanel != null)
        {
            if (baseBuildingPanel != null) baseBuildingPanel.SetActive(false);
            if (processorPanel != null) processorPanel.SetActive(false);
            resourceStatPanel.SetActive(true);
            _currentlyActivePanel = resourceStatPanel;
        }
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
    }
}
