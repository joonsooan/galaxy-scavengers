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
        
        RaycastHit2D hit2D = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        if (hit2D.collider != null) {
            // Don't trigger clicks on construction sites
            if (hit2D.collider.GetComponent<ConstructionSite>() != null) {
                return;
            }
            
            IClickable clickableObject = hit2D.collider.GetComponent<IClickable>();
            if (clickableObject != null) {
                clickableObject.OnClicked();
            }
        }
    }
}
