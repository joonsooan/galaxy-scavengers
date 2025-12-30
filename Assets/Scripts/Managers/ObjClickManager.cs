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
        
        // Clear hover when clicking
        if (BuildingHoverManager.Instance != null)
        {
            BuildingHoverManager.Instance.ClearHoverOnClick();
        }
        
        RaycastHit2D[] hits = Physics2D.RaycastAll(mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        
        foreach (RaycastHit2D hit in hits) {
            if (hit.collider != null && !hit.collider.isTrigger) {
                if (hit.collider.GetComponent<ConstructionSite>() != null) {
                    continue;
                }
                
                // Check if it's a processor first - if so, hide buildingInfoPanel and show processor UI
                Processor processor = hit.collider.GetComponent<Processor>();
                if (processor != null) {
                    // Hide buildingInfoPanel if it's showing
                    if (BuildingInfoPanel.Instance != null) {
                        BuildingInfoPanel.Instance.gameObject.SetActive(false);
                    }
                    processor.OnClicked();
                    return;
                }
                
                IClickable clickableObject = hit.collider.GetComponent<IClickable>();
                if (clickableObject != null) {
                    clickableObject.OnClicked();
                    return;
                }
            }
        }
    }
}
