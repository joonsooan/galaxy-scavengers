using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjClickManager : MonoBehaviour
{
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Update()
    {
        if (mainCamera == null) return;

        if (IsLoadingScreenActive()) return;

        if (Input.GetMouseButtonDown(0)) {
            HandleClick();
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        mainCamera = Camera.main;
    }

    private void HandleClick()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) {
            return;
        }

        LaunchUIController launchUIController = FindFirstObjectByType<LaunchUIController>(FindObjectsInactive.Include);
        if (launchUIController != null && launchUIController.IsLaunchInputLockActive())
        {
            return;
        }
        
        RaycastHit2D[] hits = Physics2D.RaycastAll(mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        if (IsProducePanelActive())
        {
            BuildingDataHolder clickedBuilding = GetClickedBuildingHolder(hits);
            if (clickedBuilding != null)
            {
                BuildingHoverManager.Instance?.HandleNormalBuildingClick(clickedBuilding);
                return;
            }
        }

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null && hit.collider is BoxCollider2D)
            {
                MainStructure mainStructure = hit.collider.GetComponent<MainStructure>();
                if (mainStructure != null)
                {
                    BuildingHoverManager.Instance?.ClearHoverOnClick();
                    if (BuildingInfoPanel.Instance != null)
                    {
                        BuildingInfoPanel.Instance.gameObject.SetActive(false);
                    }

                    mainStructure.OnClicked();
                    return;
                }

                BuildingDataHolder smelterHolder = hit.collider.GetComponentInParent<BuildingDataHolder>();
                if (smelterHolder != null && smelterHolder.buildingData != null &&
                    smelterHolder.buildingData.buildingType == BuildingType.Smelter)
                {
                    Processor processor = hit.collider.GetComponentInParent<Processor>();
                    if (processor != null)
                    {
                        BuildingHoverManager.Instance?.ClearHoverOnClick();
                        if (BuildingInfoPanel.Instance != null)
                        {
                            BuildingInfoPanel.Instance.gameObject.SetActive(false);
                        }
                        processor.OnClicked();
                        return;
                    }
                }

                DataExtractor dataExtractor = hit.collider.gameObject.GetComponent<DataExtractor>();
                if (dataExtractor != null)
                {
                    BuildingHoverManager.Instance?.ClearHoverOnClick();
                    if (BuildingInfoPanel.Instance != null)
                    {
                        BuildingInfoPanel.Instance.gameObject.SetActive(false);
                    }
                    dataExtractor.OnClicked();
                    return;
                }
            }
        }

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            BuildingDataHolder buildingHolder = hit.collider.GetComponentInParent<BuildingDataHolder>();
            if (buildingHolder != null && buildingHolder.buildingData != null)
            {
                BuildingType t = buildingHolder.buildingData.buildingType;
                if (t == BuildingType.MainStructure || t == BuildingType.DataExtractor || t == BuildingType.Smelter)
                {
                    continue;
                }

                BuildingHoverManager.Instance?.HandleNormalBuildingClick(buildingHolder);
                return;
            }
        }

        BuildingHoverManager.Instance?.ClearHoverOnClick();

        foreach (RaycastHit2D hit in hits) {
            if (hit.collider != null && !hit.collider.isTrigger) {
                if (hit.collider.GetComponent<ConstructionSite>() != null) {
                    continue;
                }
                
                IClickable clickableObject = hit.collider.GetComponent<IClickable>();
                if (clickableObject != null) {
                    clickableObject.OnClicked();
                    return;
                }
            }
        }
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

    private static bool IsProducePanelActive()
    {
        if (GameManager.Instance == null || GameManager.Instance.uiManager == null)
        {
            return false;
        }

        UIManager uiManager = GameManager.Instance.uiManager;
        return uiManager.IsProcessorPanelActive() || uiManager.IsDroneHubPanelActive();
    }

    private static BuildingDataHolder GetClickedBuildingHolder(RaycastHit2D[] hits)
    {
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            BuildingDataHolder buildingHolder = hit.collider.GetComponentInParent<BuildingDataHolder>();
            if (buildingHolder != null && buildingHolder.buildingData != null)
            {
                return buildingHolder;
            }
        }

        return null;
    }
}


