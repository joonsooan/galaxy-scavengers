using UnityEngine;
using UnityEngine.EventSystems;

public class AlertCellTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
        if (_appearance != null)
            _appearance.SetHovered(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (GameAlertUIManager.Instance != null)
            GameAlertUIManager.Instance.HideTooltip();
        if (_appearance != null)
            _appearance.SetHovered(false);
    }
}
