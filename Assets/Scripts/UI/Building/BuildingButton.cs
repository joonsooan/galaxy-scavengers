using System.Collections;
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

    [Header("Tutorial Settings")]
    [SerializeField] private string tutorialKey;
    [SerializeField] private Material glowMaterial;

    private Button _button;
    private bool _hasStarted;
    private bool _isUnlockSubscribed;

    private void Awake()
    {
        _button = GetComponent<Button>();

        if (_button != null) {
            _button.onClick.AddListener(OnButtonClicked);
        }

        InitializeBtn();
        SubscribeToUnlockEvent();
    }

    private void Start()
    {
        _hasStarted = true;

        SubscribeToUnlockEvent();

        if (!string.IsNullOrEmpty(tutorialKey) && glowMaterial != null && TutorialManager.Instance != null) {
            TutorialManager.Instance.RegisterRuntimeUI(tutorialKey, gameObject, glowMaterial);
        }

        StartCoroutine(DelayedUnlockCheck());
    }

    public void RefreshUnlockStatus()
    {
        SubscribeToUnlockEvent();
        UpdateUnlockStatus();
    }

    private void SubscribeToUnlockEvent()
    {
        if (_isUnlockSubscribed) return;
        if (BuildingUnlockManager.Instance == null) return;

        BuildingUnlockManager.Instance.OnBuildingUnlocked += OnBuildingUnlocked;
        _isUnlockSubscribed = true;
    }

    private IEnumerator DelayedUnlockCheck()
    {
        yield return null;
        UpdateUnlockStatus();
    }

    public void ApplyPassiveLocaleRefresh()
    {
        InitializeBtn();
    }

    private void OnEnable()
    {
        if (_hasStarted)
            UpdateUnlockStatus();
    }

    private void OnDestroy()
    {
        if (_button != null) {
            _button.onClick.RemoveListener(OnButtonClicked);
        }

        if (_isUnlockSubscribed && BuildingUnlockManager.HasInstance) {
            BuildingUnlockManager.Instance.OnBuildingUnlocked -= OnBuildingUnlocked;
            _isUnlockSubscribed = false;
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
        
        if (buildingData != null && BuildingInfoPanel.Instance != null)
        {
            if (BuildingInfoPanel.Instance.gameObject != null)
            {
                BuildingInfoPanel.Instance.gameObject.SetActive(true);
            }
            Damageable damageable = null;
            if (buildingData.buildingPrefab != null)
            {
                damageable = buildingData.buildingPrefab.GetComponent<Damageable>();
            }
            BuildingInfoPanel.Instance.PreviewInfo(buildingData, damageable, true, false, null);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (BuildingInfoPanel.Instance != null) {
            BuildingInfoPanel.Instance.CancelPreview();
        }
    }

    private void InitializeBtn()
    {
        if (buildingData == null) return;

        if (icon != null) {
            icon.sprite = buildingData.icon;
        }

        if (btnName != null) {
            btnName.text = buildingData.GetDisplayName();
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
            gameObject.SetActive(false);
            return;
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

