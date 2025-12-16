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
        
        RaycastHit2D[] hits = Physics2D.RaycastAll(mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        
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
