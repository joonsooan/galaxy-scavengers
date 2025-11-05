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
        RaycastHit2D hit2D = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        if (hit2D.collider != null) {
            IClickable clickableObject = hit2D.collider.GetComponent<IClickable>();
            if (clickableObject != null) {
                clickableObject.OnClicked();
            }
        }
    }
}
