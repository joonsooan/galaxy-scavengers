using UnityEngine;
using UnityEngine.UI;

public class MainControlPanel : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button baseBuildingBtn;
    [SerializeField] private Button processorBtn;
    [SerializeField] private Button resourceStatBtn;
    
    [Header("UI Panels")]
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
            // First, check if we're dragging a building - if so, end the drag first
            // The UI will be hidden after the drag ends
            if (GameManager.Instance != null && GameManager.Instance.IsDragging()) {
                // Don't hide UI yet - let the drag end first
                return;
            }
            
            // Only hide UI if we're not dragging
            HideAllPanels();
        }
    }
    
    public void OnBaseBuildingBtnClicked()
    {
        ShowPanel(baseBuildingPanel);
    }
    
    public void OnProcessorBtnClicked()
    {
        ShowPanel(processorPanel);
    }
    
    public void OnResourceStatBtnClicked()
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
