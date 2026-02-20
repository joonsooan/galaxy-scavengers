using UnityEngine;
using UnityEngine.EventSystems;

public class AlertCellTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private GameAlertType alertType;
    private AlertCellAppearance _appearance;
    private GameAlertUIManager _alertManager;

    private void Awake()
    {
        _appearance = GetComponent<AlertCellAppearance>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if ((_alertManager ??= FindFirstObjectByType<GameAlertUIManager>()) != null)
            _alertManager.ShowTooltip(alertType);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if ((_alertManager ??= FindFirstObjectByType<GameAlertUIManager>()) != null)
            _alertManager.HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if ((_alertManager ??= FindFirstObjectByType<GameAlertUIManager>()) == null)
            return;
        _alertManager.TryFocusAlert(alertType);
    }
}
