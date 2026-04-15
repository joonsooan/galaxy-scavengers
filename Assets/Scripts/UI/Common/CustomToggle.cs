using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CustomToggle : Toggle
{
    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        if (!IsInteractable() || IsPressed())
        {
            return;
        }

        DoStateTransition(SelectionState.Highlighted, false);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        if (!IsInteractable())
        {
            return;
        }

        if (IsPressed())
        {
            return;
        }

        if (eventData.pointerEnter == gameObject || eventData.pointerEnter != null && eventData.pointerEnter.transform.IsChildOf(transform))
        {
            DoStateTransition(SelectionState.Highlighted, false);
        }
    }
}
