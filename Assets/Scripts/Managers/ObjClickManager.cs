using UnityEngine;

public class ObjClickManager : MonoBehaviour
{
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
    }

    private void HandleClick()
    {
        RaycastHit2D hit2D = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        if (hit2D.collider != null)
        {
            IClickable clickableObject = hit2D.collider.GetComponent<IClickable>();
            if (clickableObject != null)
            {
                clickableObject.OnClicked();
            }
        }
    }
}
