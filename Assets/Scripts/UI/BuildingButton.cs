using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class BuildingButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Building Data")]
    [SerializeField] private BuildingData buildingData;

    [Header("Optional Settings")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text btnName;
    [SerializeField] private bool closePanelOnClick;
    [SerializeField] private MainControlPanel controlPanel;
    
    private Button _button;
    
    private void Awake()
    {
        _button = GetComponent<Button>();
        
        if (_button != null)
        {
            _button.onClick.AddListener(OnButtonClicked);
        }

        InitializeBtn();
    }
    
    private void Start()
    {
        // Wait for BuildingUnlockManager to initialize
        if (BuildingUnlockManager.Instance == null)
        {
            GameObject unlockManagerObj = new GameObject("BuildingUnlockManager");
            unlockManagerObj.AddComponent<BuildingUnlockManager>();
        }
        
        UpdateUnlockStatus();
        
        // Subscribe to unlock events
        if (BuildingUnlockManager.Instance != null)
        {
            BuildingUnlockManager.Instance.OnBuildingUnlocked += OnBuildingUnlocked;
        }
    }
    
    private void OnEnable()
    {
        // Re-check unlock status when enabled
        if (BuildingUnlockManager.Instance != null)
        {
            UpdateUnlockStatus();
            BuildingUnlockManager.Instance.OnBuildingUnlocked += OnBuildingUnlocked;
        }
    }
    
    private void OnDisable()
    {
        if (BuildingUnlockManager.Instance != null)
        {
            BuildingUnlockManager.Instance.OnBuildingUnlocked -= OnBuildingUnlocked;
        }
    }

    private void InitializeBtn()
    {
        if (buildingData == null) return;
        
        if (icon != null)
        {
            icon.sprite = buildingData.icon;
        }
        
        if (btnName != null)
        {
            btnName.text = buildingData.displayName;
        }
    }
    
    private void UpdateUnlockStatus()
    {
        if (buildingData == null)
        {
            gameObject.SetActive(false);
            return;
        }
        
        bool isUnlocked = BuildingUnlockManager.Instance != null && 
                          BuildingUnlockManager.Instance.IsBuildingUnlocked(buildingData);
        
        // Hide/show the button based on unlock status
        gameObject.SetActive(isUnlocked);
    }
    
    private void OnBuildingUnlocked(BuildingData unlockedBuilding)
    {
        // If this button's building was unlocked, show it
        if (unlockedBuilding == buildingData)
        {
            UpdateUnlockStatus();
        }
    }

    private void OnButtonClicked()
    {
        GameManager.Instance.StartDrag(buildingData);
        BuildingInfoPanel.Instance.SelectBuilding(buildingData);
        
        if (closePanelOnClick)
        {
            ClosePanel();
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (buildingData != null && BuildingInfoPanel.Instance != null)
        {
            BuildingInfoPanel.Instance.PreviewInfo(buildingData);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (BuildingInfoPanel.Instance != null)
        {
            BuildingInfoPanel.Instance.CancelPreview();
        }
    }
    
    private void ClosePanel()
    {
        if (controlPanel != null)
        {
            controlPanel.HideAllPanels();
        }
        else
        {
            Transform panelTransform = transform.parent;
            while (panelTransform != null)
            {
                if (panelTransform.name.Contains("Panel") || panelTransform.name.Contains("panel"))
                {
                    panelTransform.gameObject.SetActive(false);
                    break;
                }
                panelTransform = panelTransform.parent;
            }
        }
    }
    
    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }
    }
}

