using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDisplay : InfoDisplayTrigger, IPointerClickHandler, IPointerExitHandler
{
    public BuildingPieceData buildingPieceData;

    [Header("UI References")]
    [SerializeField] private Image cardIcon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Button buyButton;

    private void Awake()
    {
        UpdateCardUI();
        buyButton.onClick.AddListener(OnClick);
    }
    
    private void Update()
    {
        UpdateButtonState();
    }

    protected override DisplayableData GetData() => buildingPieceData;

    protected override void ShowInfo()
    {
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
            GameManager.Instance.uiManager.DisplayCardInfo(buildingPieceData);
    }

    protected override void HideInfo()
    {
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
            GameManager.Instance.uiManager.HideCardInfo();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
            GameManager.Instance.uiManager.PinCardInfo(buildingPieceData);
    }

    public new void OnPointerExit(PointerEventData eventData)
    {
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
            GameManager.Instance.uiManager.HideCardInfo();
    }
    
    public void OnClick()
    {
        if (GameManager.Instance != null && buildingPieceData != null)
        {
            if (GameManager.Instance.IsDragging() && GameManager.Instance.GetActiveData() == buildingPieceData)
            {
                GameManager.Instance.EndDrag();
            }
            else
            {
                GameManager.Instance.StartDrag(buildingPieceData);
            }
        }
    }

    private void UpdateButtonState()
    {
        if (buildingPieceData != null && ResourceManager.Instance != null && buyButton != null)
        {
            bool canAfford = ResourceManager.Instance.HasEnoughResources(buildingPieceData.costs);
            buyButton.interactable = canAfford;
        }
    }

    private void UpdateCardUI()
    {
        nameText.text = buildingPieceData.GetDisplayName();
        cardIcon.sprite = buildingPieceData.icon;
    }
}