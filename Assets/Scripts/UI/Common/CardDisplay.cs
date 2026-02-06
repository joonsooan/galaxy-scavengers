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
        GameManager.Instance?.uiManager.DisplayCardInfo(buildingPieceData);
    }

    protected override void HideInfo()
    {
        GameManager.Instance?.uiManager.HideCardInfo();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GameManager.Instance?.uiManager.PinCardInfo(buildingPieceData);
    }

    public new void OnPointerExit(PointerEventData eventData)
    {
        GameManager.Instance?.uiManager.HideCardInfo();
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
        nameText.text = buildingPieceData.displayName;
        cardIcon.sprite = buildingPieceData.icon;
    }
}