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

    [Header("Drone Hub Info Panel")]
    [SerializeField] private GameObject droneHubInfoPanel;

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
    private Processor _currentProcessor;
    private DroneHub _currentDroneHub;

    private CardData _pinnedCardData;
    private ProcessorData _pinnedProcessorData;
    private DroneHubData _pinnedDroneHubData;
    private ComboCardData _pinnedRecipeData;

    // Track which UI panel is currently displayed
    private enum ActiveUIPanel
    {
        None,
        Processor,
        DroneHub
    }
    
    private ActiveUIPanel _activeUIPanel = ActiveUIPanel.None;

    private void Start()
    {
        if (cardInfoPanel != null) cardInfoPanel.SetActive(false);
        if (recipeInfoPanel != null) recipeInfoPanel.SetActive(false);
        if (processorInfoPanel != null) processorInfoPanel.SetActive(false);
        if (droneHubInfoPanel != null) droneHubInfoPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1)) {
            if (EventSystem.current.IsPointerOverGameObject()) {
                return;
            }
            
            // First, check if we're dragging a building - if so, end the drag first
            // The UI will be hidden after the drag ends (handled in GameManager.EndDrag)
            if (GameManager.Instance != null && GameManager.Instance.IsDragging()) {
                // Don't hide UI yet - let the drag end first
                return;
            }
            
            // Only hide UI if we're not dragging
            UnpinAndHideAllPanels();
        }
    }

    private void OnEnable()
    {
        Processor.OnProcessorClicked += HandleProcessorClicked;
        DroneHub.OnDroneHubClicked += HandleDroneHubClicked;
    }

    private void OnDisable()
    {
        Processor.OnProcessorClicked -= HandleProcessorClicked;
        DroneHub.OnDroneHubClicked -= HandleDroneHubClicked;
    }

    private void HandleProcessorClicked(Processor processor)
    {
        if (processor == null) return;

        // Hide any currently displayed IClickable UI
        HideCurrentIClickableUI();
        
        _currentProcessor = processor;
        _activeUIPanel = ActiveUIPanel.Processor;
        DisplayProcessorInfo(processor);
    }

    private void HandleDroneHubClicked(DroneHub droneHub)
    {
        if (droneHub == null) return;

        // Hide any currently displayed IClickable UI
        HideCurrentIClickableUI();
        
        _currentDroneHub = droneHub;
        _activeUIPanel = ActiveUIPanel.DroneHub;
        DisplayDroneHubInfo(droneHub);
    }

    private void HideCurrentIClickableUI()
    {
        // 이전에 보여지던 UI 비활성화
        switch (_activeUIPanel)
        {
            case ActiveUIPanel.Processor:
                if (processorInfoPanel != null) {
                    processorInfoPanel.gameObject.SetActive(false);
                }
                _currentProcessor = null;
                break;
                
            case ActiveUIPanel.DroneHub:
                if (droneHubInfoPanel != null) {
                    droneHubInfoPanel.gameObject.SetActive(false);
                }
                _currentDroneHub = null;
                break;
        }
        
        _activeUIPanel = ActiveUIPanel.None;
    }

    public void UnpinAndHideAllPanels()
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

        _pinnedDroneHubData = null;
        if (droneHubInfoPanel != null) {
            droneHubInfoPanel.SetActive(false);
        }

        // Reset active UI panel tracking
        HideCurrentIClickableUI();
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

    private void DisplayProcessorInfo(Processor processor)
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
            if (_activeUIPanel == ActiveUIPanel.Processor) {
                _activeUIPanel = ActiveUIPanel.None;
            }
        }
    }

    public void PinProcessorInfo(Processor processor)
    {
        if (processor == null) return;

        ProcessorData data = processor.ProcessorData;

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

    private void DisplayDroneHubInfo(DroneHub droneHub)
    {
        if (droneHub == null || droneHubInfoPanel == null) return;
        droneHubInfoPanel.gameObject.SetActive(true);
        DroneProduceUIManager.Instance.ShowDroneHubUI(droneHub);
    }

    public void HideDroneHubInfo()
    {
        if (droneHubInfoPanel == null) return;

        if (_pinnedDroneHubData == null) {
            droneHubInfoPanel.gameObject.SetActive(false);
            _currentDroneHub = null;
            if (_activeUIPanel == ActiveUIPanel.DroneHub) {
                _activeUIPanel = ActiveUIPanel.None;
            }
        }
    }

    public void PinDroneHubInfo(DroneHub droneHub)
    {
        if (droneHub == null) return;

        DroneHubData data = droneHub.DroneHubData;

        if (_pinnedDroneHubData == data) {
            UnpinAndHideAllPanels();
        }
        else {
            UnpinAndHideAllPanels();
            _pinnedDroneHubData = data;
            _currentDroneHub = droneHub;
            DisplayDroneHubInfo(droneHub);
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
