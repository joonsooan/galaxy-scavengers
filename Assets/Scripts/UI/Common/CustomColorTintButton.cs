using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CustomColorTintButton : Button
{
    protected override void OnEnable()
    {
        base.OnEnable();
        ForceClearPressedVisual();
    }

    protected override void OnDisable()
    {
        ForceClearPressedVisual();
        base.OnDisable();
    }

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.ColorTint;
    }

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

        if (IsPointerInsideButtonRect(eventData))
        {
            DoStateTransition(SelectionState.Highlighted, false);
        }
        else
        {
            DoStateTransition(SelectionState.Normal, false);
        }
    }

    private bool IsPointerInsideButtonRect(PointerEventData eventData)
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform == null || eventData == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
            eventData.position,
            eventData.pressEventCamera);
    }

    private void ForceClearPressedVisual()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        EventSystem current = EventSystem.current;
        if (current != null && current.currentSelectedGameObject == gameObject)
        {
            current.SetSelectedGameObject(null);
        }

        SelectionState state = IsInteractable() ? SelectionState.Normal : SelectionState.Disabled;
        DoStateTransition(state, false);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        transition = Transition.ColorTint;
    }
#endif
}
