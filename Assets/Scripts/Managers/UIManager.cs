using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("Card Info Panel")]
    [SerializeField] private GameObject cardInfoPanel;
    [SerializeField] private TMP_Text cardNameText;
    [SerializeField] private TMP_Text cardDescriptionText;
    [SerializeField] private GameObject cardResourcePanel;
    [SerializeField] private GameObject resourceInfoCellPrefab;

    [Header("Recipe Info Panel")]
    [SerializeField] private GameObject recipeInfoPanel;
    [SerializeField] private RecipeInfo recipeInfoComponent;

    [Header("Tips Reference")]
    [SerializeField] private GameObject tipPanel;
    [SerializeField] private GameObject tipResourcePanel1;
    [SerializeField] private GameObject tipResourcePanel2;
    [SerializeField] private GameObject tipRecipeBtn;
    [SerializeField] private GameObject tipCardBtn;
    [SerializeField] private GameObject tipSolanaRequiredPanel;
    [SerializeField] private GameObject tipRemainTimePanel;
    [SerializeField] private GameObject tipMineTypePanel;
    [SerializeField] private GameObject tipUnitMakeBtn;
    
    private CardData _pinnedCardData;
    private ComboCardData _pinnedRecipeData;

    private void Start()
    {
        if (cardInfoPanel != null) cardInfoPanel.SetActive(false);
        if (recipeInfoPanel != null) recipeInfoPanel.SetActive(false);
    }

    public void DisplayCardInfo(CardData data)
    {
        if (data == null) return;
        cardInfoPanel.SetActive(true);
        cardNameText.text = data.displayName;
        cardDescriptionText.text = data.description;

        foreach (Transform child in cardResourcePanel.transform) Destroy(child.gameObject);
        foreach (ResourceCost cost in data.costs) {
            GameObject cell = Instantiate(resourceInfoCellPrefab, cardResourcePanel.transform);
            cell.GetComponent<ResourceInfoCell>().SetInfo(cost.resourceType, cost.amount);
        }
    }

    public void HideCardInfo()
    {
        if (_pinnedCardData == null) {
            cardInfoPanel.SetActive(false);
        }
        else {
            DisplayCardInfo(_pinnedCardData);
        }
    }

    public void PinCardInfo(CardData data)
    {
        if (_pinnedCardData == data) {
            _pinnedCardData = null;
            cardInfoPanel.SetActive(false);
        }
        else {
            _pinnedCardData = data;
            DisplayCardInfo(data);
        }
    }

    public void UnpinAndHideCardPanel()
    {
        _pinnedCardData = null;
        if (cardInfoPanel != null) {
            cardInfoPanel.SetActive(false);
        }
    }

    public void DisplayRecipeInfo(ComboCardData data)
    {
        if (data == null || recipeInfoComponent == null) return;
        recipeInfoComponent.gameObject.SetActive(true);
        recipeInfoComponent.UpdateRecipeInfo(data);
    }

    public void HideRecipeInfo()
    {
        if (recipeInfoComponent == null) return;
        if (_pinnedRecipeData == null) {
            recipeInfoComponent.gameObject.SetActive(false);
        }
        else {
            recipeInfoComponent.UpdateRecipeInfo(_pinnedRecipeData);
        }
    }

    public void PinRecipeInfo(ComboCardData data)
    {
        if (_pinnedRecipeData == data) {
            _pinnedRecipeData = null;
            recipeInfoComponent.gameObject.SetActive(false);
        }
        else {
            _pinnedRecipeData = data;
            DisplayRecipeInfo(data);
        }
    }

    public void ToggleRecipePanel()
    {
        if (recipeInfoPanel == null) return;
        bool isActive = !recipeInfoPanel.activeSelf;
        recipeInfoPanel.SetActive(isActive);
        if (!isActive) {
            _pinnedRecipeData = null;
        }
        else {
            if (recipeInfoComponent != null) {
                recipeInfoComponent.gameObject.SetActive(false);
            }
        }
    }

    public void ToggleTipPanel()
    {
        if (tipPanel == null) return;
        bool isActive = !tipPanel.activeSelf;
        tipPanel.SetActive(isActive);
    }
    
    // Tips UI : Display

    public void DisplayResourcePanel()
    {
        tipResourcePanel1.SetActive(true);
        tipResourcePanel2.SetActive(true);
    }

    public void DisplayRecipeBtn()
    {
        tipRecipeBtn.SetActive(true);
    }

    public void DisplayCardBtn()
    {
        tipCardBtn.SetActive(true);
    }

    public void DisplaySolanaRequiredPanel()
    {
        tipSolanaRequiredPanel.SetActive(true);
    }

    public void DisplayRemainTimePanel()
    {
        tipRemainTimePanel.SetActive(true);
    }

    public void DisplayMineTypePanel()
    {
        tipMineTypePanel.SetActive(true);
    }

    public void DisplayUnitMakeBtn()
    {
        tipUnitMakeBtn.SetActive(true);
    }
    
    // Tips UI : Hide
    
    public void HideResourcePanel()
    {
        tipResourcePanel1.SetActive(false);
        tipResourcePanel2.SetActive(false);
    }
    
    public void HideRecipeBtn()
    {
        tipRecipeBtn.SetActive(false);
    }

    public void HideCardBtn()
    {
        tipCardBtn.SetActive(false);
    }

    public void HideSolanaRequiredPanel()
    {
        tipSolanaRequiredPanel.SetActive(false);
    }

    public void HideRemainTimePanel()
    {
        tipRemainTimePanel.SetActive(false);
    }

    public void HideMineTypePanel()
    {
        tipMineTypePanel.SetActive(false);
    }

    public void HideUnitMakeBtn()
    {
        tipUnitMakeBtn.SetActive(false);
    }
}
