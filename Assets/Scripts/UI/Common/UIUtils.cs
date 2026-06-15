using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UIUtils
{
    private static PointerEventData _cachedPointerEventData;
    private static readonly List<RaycastResult> _cachedRaycastResults = new List<RaycastResult>();

    public static bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (_cachedPointerEventData == null)
        {
            _cachedPointerEventData = new PointerEventData(EventSystem.current);
        }
        _cachedPointerEventData.position = Input.mousePosition;

        _cachedRaycastResults.Clear();
        EventSystem.current.RaycastAll(_cachedPointerEventData, _cachedRaycastResults);

        foreach (RaycastResult result in _cachedRaycastResults)
        {
            if (result.module != null && result.module is GraphicRaycaster)
            {
                return true;
            }

            if (result.gameObject != null)
            {
                if (result.gameObject.GetComponent<Graphic>() != null ||
                    result.gameObject.GetComponent<Canvas>() != null ||
                    result.gameObject.layer == LayerMask.NameToLayer("UI"))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

