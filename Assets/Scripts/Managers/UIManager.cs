using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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

    [Header("Processor Info Panel")]
    [SerializeField] private GameObject processorInfoPanel;

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
    private ResourceProcessor _currentProcessor;

    private CardData _pinnedCardData;
    private ResourceProcessorData _pinnedProcessorData;
    private ComboCardData _pinnedRecipeData;

    private void Start()
    {
        if (cardInfoPanel != null) cardInfoPanel.SetActive(false);
        if (recipeInfoPanel != null) recipeInfoPanel.SetActive(false);
        if (processorInfoPanel != null) processorInfoPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1)) {
            if (EventSystem.current.IsPointerOverGameObject()) {
                return;
            }
            UnpinAndHideAllPanels();
        }
    }

    private void OnEnable()
    {
        ResourceProcessor.OnProcessorClicked += HandleProcessorClicked;
    }

    private void OnDisable()
    {
        ResourceProcessor.OnProcessorClicked -= HandleProcessorClicked;
    }

    private void HandleProcessorClicked(ResourceProcessor processor)
    {
        if (processor == null) return;

        _currentProcessor = processor;
        DisplayProcessorInfo(processor);
    }

    private void UnpinAndHideAllPanels()
    {
        _pinnedCardData = null;
        if (cardInfoPanel != null) {
            cardInfoPanel.SetActive(false);
        }

        _pinnedRecipeData = null;
        if (recipeInfoPanel != null) {
            recipeInfoPanel.SetActive(false);
        }

        _pinnedProcessorData = null;
        if (processorInfoPanel != null) {
            processorInfoPanel.SetActive(false);
        }
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
            // _pinnedCardData = null;
            // cardInfoPanel.SetActive(false);
            UnpinAndHideAllPanels();
        }
        else {
            // UnpinAndHideAllPanels();
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
        recipeInfoPanel.SetActive(true);
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

    private void DisplayProcessorInfo(ResourceProcessor processor)
    {
        if (processor == null || processorInfoPanel == null) return;
        processorInfoPanel.gameObject.SetActive(true);
        ProcessorUIManager.Instance.ShowProcessorUI(processor);
    }

    public void HideProcessorInfo()
    {
        if (processorInfoPanel == null) return;

        if (_pinnedProcessorData == null) {
            processorInfoPanel.gameObject.SetActive(false);
            _currentProcessor = null;
        }
    }

    public void PinProcessorInfo(ResourceProcessor processor)
    {
        if (processor == null) return;

        ResourceProcessorData data = processor.ProcessorData;

        if (_pinnedProcessorData == data) {
            UnpinAndHideAllPanels();
        }
        else {
            UnpinAndHideAllPanels();
            _pinnedProcessorData = data;
            _currentProcessor = processor;
            DisplayProcessorInfo(processor);
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
