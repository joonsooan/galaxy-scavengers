using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        if (_button != null) {
            _button.onClick.AddListener(OnButtonClicked);
        }

        InitializeBtn();
    }

    private void Start()
    {
        if (BuildingUnlockManager.Instance == null) {
            GameObject unlockManagerObj = new GameObject("BuildingUnlockManager");
            unlockManagerObj.AddComponent<BuildingUnlockManager>();
        }

        UpdateUnlockStatus();

        if (BuildingUnlockManager.Instance != null) {
            BuildingUnlockManager.Instance.OnBuildingUnlocked += OnBuildingUnlocked;
        }
    }

    private void OnEnable()
    {
        if (BuildingUnlockManager.Instance != null) {
            UpdateUnlockStatus();
            BuildingUnlockManager.Instance.OnBuildingUnlocked += OnBuildingUnlocked;
        }

        DroneHub.OnDroneHubClicked += OnDroneHubStatusChanged;
    }

    private void OnDisable()
    {
        if (BuildingUnlockManager.Instance != null) {
            BuildingUnlockManager.Instance.OnBuildingUnlocked -= OnBuildingUnlocked;
        }

        DroneHub.OnDroneHubClicked -= OnDroneHubStatusChanged;
    }

    private void OnDestroy()
    {
        if (_button != null) {
            _button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            UIManager uiManager = GameManager.Instance.uiManager;
            if (uiManager.IsProcessorPanelActive() || uiManager.IsDroneHubPanelActive())
            {
                return;
            }
        }
        
        if (buildingData != null && BuildingInfoPanel.Instance != null) {
            BuildingInfoPanel.Instance.PreviewInfo(buildingData);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (BuildingInfoPanel.Instance != null) {
            BuildingInfoPanel.Instance.CancelPreview();
        }
    }

    private void OnDroneHubStatusChanged(DroneHub droneHub)
    {
        if (buildingData != null && buildingData.buildingType == BuildingType.DroneHub) {
            UpdateUnlockStatus();
        }
    }

    private void InitializeBtn()
    {
        if (buildingData == null) return;

        if (icon != null) {
            icon.sprite = buildingData.icon;
        }

        if (btnName != null) {
            btnName.text = buildingData.displayName;
        }
    }

    private void UpdateUnlockStatus()
    {
        if (buildingData == null) {
            gameObject.SetActive(false);
            return;
        }

        bool isUnlocked = BuildingUnlockManager.Instance != null &&
            BuildingUnlockManager.Instance.IsBuildingUnlocked(buildingData);

        if (!isUnlocked) {
            gameObject.SetActive(false);
            return;
        }

        if (buildingData.buildingType == BuildingType.DroneHub) {
            int existingDroneHubCount = FindObjectsByType<DroneHub>(FindObjectsSortMode.None)
                .Count(dh => dh != null && BuildingManager.IsBuildingProperlyPlaced(dh.transform));

            if (existingDroneHubCount >= 1) {
                if (_button != null) {
                    _button.interactable = false;
                }
            }
            else {
                if (_button != null) {
                    _button.interactable = true;
                }
            }
        }

        gameObject.SetActive(true);
    }

    private void OnBuildingUnlocked(BuildingData unlockedBuilding)
    {
        if (unlockedBuilding == buildingData) {
            UpdateUnlockStatus();
        }
    }

    private void OnButtonClicked()
    {
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            UIManager uiManager = GameManager.Instance.uiManager;
            if (uiManager.IsProcessorPanelActive())
            {
                uiManager.HideProcessorInfo();
            }
            if (uiManager.IsDroneHubPanelActive())
            {
                uiManager.HideDroneHubInfo();
            }
        }
        
        GameManager.Instance.StartDrag(buildingData);
        BuildingInfoPanel.Instance.SelectBuilding(buildingData);

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.DisableHighlightForTarget(gameObject);
        }

        if (closePanelOnClick) {
            ClosePanel();
        }
    }

    private void ClosePanel()
    {
        if (controlPanel != null) {
            controlPanel.HideAllPanels();
        }
        else {
            Transform panelTransform = transform.parent;
            while (panelTransform != null) {
                if (panelTransform.name.Contains("Panel") || panelTransform.name.Contains("panel")) {
                    panelTransform.gameObject.SetActive(false);
                    break;
                }
                panelTransform = panelTransform.parent;
            }
        }
    }
}
