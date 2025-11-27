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
        // Don't process clicks while dragging a building
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) {
            return;
        }
        
        // Use RaycastAll to get all colliders at the click position, then find the first non-trigger one
        // This ensures we can click buildings even if they have trigger colliders (like VisionProvider) on them
        RaycastHit2D[] hits = Physics2D.RaycastAll(mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        
        // Find the first non-trigger collider (buildings/units should have solid colliders)
        foreach (RaycastHit2D hit in hits) {
            if (hit.collider != null && !hit.collider.isTrigger) {
                // Don't trigger clicks on construction sites
                if (hit.collider.GetComponent<ConstructionSite>() != null) {
                    continue;
                }
                
                IClickable clickableObject = hit.collider.GetComponent<IClickable>();
                if (clickableObject != null) {
                    clickableObject.OnClicked();
                    return; // Only process the first valid clickable object
                }
            }
        }
    }
}
