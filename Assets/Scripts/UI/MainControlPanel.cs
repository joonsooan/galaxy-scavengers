using UnityEngine;
using UnityEngine.UI;

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
    
    private void Start()
    {
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
            if (GameManager.Instance != null && GameManager.Instance.IsDragging()) {
                return;
            }
            HideAllPanels();
        }
    }

    private void OnBaseBuildingBtnClicked()
    {
        buildingInfoPanel.SetActive(true);
        ShowPanel(baseBuildingPanel);
    }

    private void OnProcessorBtnClicked()
    {
        buildingInfoPanel.SetActive(true);
        ShowPanel(processorPanel);
    }

    private void OnResourceStatBtnClicked()
    {
        ShowPanel(resourceStatPanel);
    }
    
    private void ShowPanel(GameObject panel)
    {
        if (panel == null) return;
        
        if (_currentlyActivePanel != null && _currentlyActivePanel != panel)
        {
            _currentlyActivePanel.SetActive(false);
        }
        
        if (_currentlyActivePanel == panel)
        {
            panel.SetActive(false);
            _currentlyActivePanel = null;
        }
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
        
        // buildingInfoPanel.SetActive(false);
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
