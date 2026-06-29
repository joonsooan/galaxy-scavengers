using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class AlertCameraClickHandler : MonoBehaviour
{
    [SerializeField] private CameraTargetController cameraTargetController;

    private PointerEventData _pointerEventData;
    private readonly List<RaycastResult> _raycastResultsCache = new List<RaycastResult>();

    private void Awake()
    {
        if (cameraTargetController == null)
            cameraTargetController = FindFirstObjectByType<CameraTargetController>();
    }

    private void Start()
    {
        if (EventSystem.current != null)
            _pointerEventData = new PointerEventData(EventSystem.current);
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.IsGameplayReady)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;
        if (EventSystem.current == null)
            return;

        if (_pointerEventData == null)
            _pointerEventData = new PointerEventData(EventSystem.current);
        _pointerEventData.position = Input.mousePosition;

        _raycastResultsCache.Clear();
        EventSystem.current.RaycastAll(_pointerEventData, _raycastResultsCache);

        bool clickedAlert = false;
        for (int i = 0; i < _raycastResultsCache.Count; i++)
        {
            GameObject hitObject = _raycastResultsCache[i].gameObject;
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
            return;
        cameraTargetController.ResetFollowTargetToPlayer();
    }
}

