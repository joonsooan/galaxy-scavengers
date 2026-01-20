using UnityEngine;
using UnityEngine.UI;

public class BaseSceneManager : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button inventoryButton;
    [SerializeField] private Button moduleButton;
    [SerializeField] private Button laboratoryButton;
    [SerializeField] private Button farmButton;
    [SerializeField] private Button mapButton;
    [SerializeField] private Button coreLaunchButton;

    [Header("UI Panels")]
    [SerializeField] private GameObject inventoryUIPanel;
    [SerializeField] private GameObject moduleUIPanel;
    [SerializeField] private GameObject laboratoryUIPanel;
    [SerializeField] private GameObject farmUIPanel;
    [SerializeField] private GameObject mapUIPanel;
    [SerializeField] private GameObject coreLaunchUIPanel;
    
    private int _currentPanelIndex = -1;
    private BaseInventorySystem _baseInventorySystem;

    private void Awake()
    {
        inventoryButton.onClick.AddListener(ToggleInventoryPanel);
        moduleButton.onClick.AddListener(() => OpenUIPanel(1));
        laboratoryButton.onClick.AddListener(() => OpenUIPanel(2));
        farmButton.onClick.AddListener(() => OpenUIPanel(3));
        mapButton.onClick.AddListener(() => OpenUIPanel(4));
        coreLaunchButton.onClick.AddListener(() => OpenUIPanel(5));
    }
    
    private void Start()
    {
        _baseInventorySystem = FindFirstObjectByType<BaseInventorySystem>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseUIPanel(_currentPanelIndex);
        }
        
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventoryPanel();
        }
    }
    
    private void ToggleInventoryPanel()
    {
        if (_baseInventorySystem != null)
        {
            GameObject inventoryPanel = _baseInventorySystem.GetInventoryPanel();
            if (inventoryPanel != null)
            {
                bool isActive = inventoryPanel.activeSelf;
                if (isActive)
                {
                    _baseInventorySystem.ToggleInventory();
                    _currentPanelIndex = -1;
                }
                else
                {
                    _baseInventorySystem.ToggleInventory();
                    _currentPanelIndex = 0;
                }
            }
        }
        else if (inventoryUIPanel != null)
        {
            bool isActive = inventoryUIPanel.activeSelf;
            if (isActive)
            {
                inventoryUIPanel.SetActive(false);
                _currentPanelIndex = -1;
            }
            else
            {
                inventoryUIPanel.SetActive(true);
                _currentPanelIndex = 0;
            }
        }
    }

    private void OnDestroy()
    {
        inventoryButton.onClick.RemoveAllListeners();
        moduleButton.onClick.RemoveAllListeners();
        laboratoryButton.onClick.RemoveAllListeners();
        farmButton.onClick.RemoveAllListeners();
        mapButton.onClick.RemoveAllListeners();
        coreLaunchButton.onClick.RemoveAllListeners();
    }

    private void OpenUIPanel(int panelIndex)
    {
        GameObject targetPanel = null;
        _currentPanelIndex = panelIndex;

        switch (panelIndex)
        {
            case 0:
                targetPanel = inventoryUIPanel;
                break;
            case 1:
                targetPanel = moduleUIPanel;
                break;
            case 2:
                targetPanel = laboratoryUIPanel;
                break;
            case 3:
                targetPanel = farmUIPanel;
                break;
            case 4:
                targetPanel = mapUIPanel;
                break;
            case 5:
                targetPanel = coreLaunchUIPanel;
                break;
        }

        if (targetPanel != null)
        {
            targetPanel.SetActive(true);
        }
    }

    private void CloseUIPanel(int panelIndex)
    {
        GameObject targetPanel = null;
        _currentPanelIndex = -1;
        
        switch (panelIndex)
        {
            case 0:
                targetPanel = inventoryUIPanel;
                break;
            case 1:
                targetPanel = moduleUIPanel;
                break;
            case 2:
                targetPanel = laboratoryUIPanel;
                break;
            case 3:
                targetPanel = farmUIPanel;
                break;
            case 4:
                targetPanel = mapUIPanel;
                break;
            case 5:
                targetPanel = coreLaunchUIPanel;
                break;
        }

        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
        }
    }

    private void CloseAllPanels()
    {
        if (moduleUIPanel != null) moduleUIPanel.SetActive(false);
        if (laboratoryUIPanel != null) laboratoryUIPanel.SetActive(false);
        if (farmUIPanel != null) farmUIPanel.SetActive(false);
        if (mapUIPanel != null) mapUIPanel.SetActive(false);
        if (coreLaunchUIPanel != null) coreLaunchUIPanel.SetActive(false);
    }
}

