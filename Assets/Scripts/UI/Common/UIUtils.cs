using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UIUtils
{
    public static bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);

        foreach (RaycastResult result in results)
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

