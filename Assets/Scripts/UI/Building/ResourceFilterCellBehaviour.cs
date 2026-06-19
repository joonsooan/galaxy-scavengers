using UnityEngine;
using UnityEngine.EventSystems;

public class ResourceFilterCellBehaviour : MonoBehaviour, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    private bool _pointerInside;

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_pointerInside)
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
