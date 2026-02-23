using UnityEngine;
using UnityEngine.EventSystems;

public class AlertCellTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private GameAlertType alertType;
    private GameAlertUIManager _alertManager;

    private GameAlertType GetResolvedAlertType()
    {
        string objectName = gameObject.name.ToLowerInvariant();

        if (alertType == GameAlertType.DroneIsNotAssigned && objectName.Contains("noresource"))
        {
            return GameAlertType.DroneNoResource;
        }

        if (alertType == GameAlertType.DroneNoResource &&
            (objectName.Contains("notassigned") || objectName.Contains("not_assigned")))
        {
            return GameAlertType.DroneIsNotAssigned;
        }

        return alertType;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        GameAlertType resolvedType = GetResolvedAlertType();
        if ((_alertManager ??= FindFirstObjectByType<GameAlertUIManager>()) != null)
            _alertManager.ShowTooltip(resolvedType);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if ((_alertManager ??= FindFirstObjectByType<GameAlertUIManager>()) != null)
            _alertManager.HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GameAlertType resolvedType = GetResolvedAlertType();
        if ((_alertManager ??= FindFirstObjectByType<GameAlertUIManager>()) == null)
            return;
        _alertManager.TryFocusAlert(resolvedType);
    }
}
