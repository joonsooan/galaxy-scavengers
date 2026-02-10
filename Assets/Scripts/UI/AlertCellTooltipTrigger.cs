using UnityEngine;
using UnityEngine.EventSystems;

public class AlertCellTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private GameAlertType alertType;
    private AlertCellAppearance _appearance;

    private void Awake()
    {
        _appearance = GetComponent<AlertCellAppearance>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (GameAlertUIManager.Instance != null)
            GameAlertUIManager.Instance.ShowTooltip(alertType);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (GameAlertUIManager.Instance != null)
            GameAlertUIManager.Instance.HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameAlertUIManager.Instance == null)
            return;
        GameAlertUIManager.Instance.TryFocusAlert(alertType);
    }
}
