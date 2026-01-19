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
        
        if (BuildingHoverManager.Instance != null)
        {
            BuildingHoverManager.Instance.ClearHoverOnClick();
        }
        
        RaycastHit2D[] hits = Physics2D.RaycastAll(mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        
        foreach (RaycastHit2D hit in hits) 
        {
            if (hit.collider != null && hit.collider is BoxCollider2D) 
            {
                MainStructure mainStructure = hit.collider.GetComponent<MainStructure>();
                if (mainStructure != null)
                {
                    mainStructure.OnClicked();
                    return;
                }
                
                Processor processor = hit.collider.gameObject.GetComponent<Processor>();
                if (processor != null) 
                {
                    if (BuildingInfoPanel.Instance != null) 
                    {
                        BuildingInfoPanel.Instance.gameObject.SetActive(false);
                    }
                    processor.OnClicked();
                    return;
                }
                
                DroneHub droneHub = hit.collider.gameObject.GetComponent<DroneHub>();
                if (droneHub != null)
                {
                    if (BuildingInfoPanel.Instance != null)
                    {
                        BuildingInfoPanel.Instance.gameObject.SetActive(false);
                    }

                    droneHub.OnClicked();
                    return;
                }
            }
        }
        
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
}
