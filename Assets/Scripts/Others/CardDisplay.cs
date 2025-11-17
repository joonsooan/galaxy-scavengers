using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDisplay : InfoDisplayTrigger, IPointerClickHandler, IPointerExitHandler
{
    public CardData cardData;

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

    protected override DisplayableData GetData() => cardData;

    protected override void ShowInfo()
    {
        GameManager.Instance?.uiManager.DisplayCardInfo(cardData);
        GameManager.Instance?.uiManager.DisplayCardBtn();
    }

    protected override void HideInfo()
    {
        GameManager.Instance?.uiManager.HideCardInfo();
        GameManager.Instance?.uiManager.HideCardBtn();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GameManager.Instance?.uiManager.PinCardInfo(cardData);
    }

    public new void OnPointerExit(PointerEventData eventData)
    {
        GameManager.Instance?.uiManager.HideCardInfo();
        GameManager.Instance?.uiManager.HideCardBtn();
    }
    
    public void OnClick()
    {
        if (GameManager.Instance != null && cardData != null)
        {
            if (GameManager.Instance.IsDragging() && GameManager.Instance.GetActiveData() == cardData)
            {
                GameManager.Instance.EndDrag();
            }
            else
            {
                GameManager.Instance.StartDrag(cardData);
            }
        }
    }

    private void UpdateButtonState()
    {
        if (cardData != null && ResourceManager.Instance != null && buyButton != null)
        {
            bool canAfford = ResourceManager.Instance.HasEnoughResources(cardData.costs);
            buyButton.interactable = canAfford;
        }
    }

    private void UpdateCardUI()
    {
        nameText.text = cardData.displayName;
        cardIcon.sprite = cardData.icon;
    }
}