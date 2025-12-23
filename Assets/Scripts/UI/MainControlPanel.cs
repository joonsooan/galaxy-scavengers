using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
    
    private void Start()
    {
        _buildingInfoPanelComponent =  buildingInfoPanel.GetComponent<BuildingInfoPanel>(); 
        
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
        if (Input.GetMouseButtonDown(1))
        {
            if (EventSystem.current.IsPointerOverGameObject())
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
        GameManager.Instance.uiManager.UnpinAndHideAllPanels();
        buildingInfoPanel.SetActive(true);
        ShowPanel(baseBuildingPanel);
    }

    private void OnProcessorBtnClicked()
    {
        GameManager.Instance.uiManager.UnpinAndHideAllPanels();
        buildingInfoPanel.SetActive(true);
        ShowPanel(processorPanel);
    }

    private void OnResourceStatBtnClicked()
    {
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
