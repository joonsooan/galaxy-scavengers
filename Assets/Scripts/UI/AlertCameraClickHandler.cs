using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class AlertCameraClickHandler : MonoBehaviour
{
    [SerializeField] private CameraTargetController cameraTargetController;

    private void Awake()
    {
        if (cameraTargetController == null)
            cameraTargetController = FindFirstObjectByType<CameraTargetController>();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;
        if (EventSystem.current == null)
            return;
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = Input.mousePosition;
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        bool clickedAlert = false;
        for (int i = 0; i < results.Count; i++)
        {
            GameObject hitObject = results[i].gameObject;
            if (hitObject == null)
                continue;
            if (hitObject.GetComponentInParent<AlertCellTooltipTrigger>() != null)
            {
                clickedAlert = true;
                break;
            }
        }
        if (clickedAlert)
            return;
        if (cameraTargetController == null)
            cameraTargetController = FindFirstObjectByType<CameraTargetController>();
        if (cameraTargetController == null)
            return;
        cameraTargetController.ResetFollowTargetToPlayer();
    }
}

