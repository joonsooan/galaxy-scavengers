using UnityEngine;
using UnityEngine.EventSystems;

public class DebuffIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CorePart _part;
    private CoreDebuffUIPanel _panel;

    public void Initialize(CorePart part, CoreDebuffUIPanel panel)
    {
        _part = part;
        _panel = panel;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_panel != null)
            _panel.ShowDebuffInfo(_part);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_panel != null)
            _panel.HideDebuffInfo();
    }
}
