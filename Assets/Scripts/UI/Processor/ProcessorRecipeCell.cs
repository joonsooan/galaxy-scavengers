using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProcessorRecipeCell : MonoBehaviour
{
    private const int MaxProduceAmount = 999;
    private const float ButtonClickCooldown = 0.1f;
    [Header("UI References")]
    [SerializeField] private Image recipeIcon;
    [SerializeField] private TMP_Text recipeName;
    [SerializeField] private TMP_Text recipeProcessTime;
    [SerializeField] private TMP_Text produceInfoText;
    [SerializeField] private GameObject recipeCellPrefab;
    [SerializeField] private RectTransform contentParent;

    [Header("Sound Effects")]
    [SerializeField] private EventReference plusButtonSound;
    [SerializeField] private EventReference minusButtonSound;

    [Header("Tutorial Settings")]
    [SerializeField] private Material glowMaterial;
    private ActiveRecipe _activeRecipe;

    private int _currentStorageAmount;
    private ResourceCost[] _ingredients;

    private float _lastButtonClickTime;
    private int _produceMaxAmount;

    private ResourceCost _product;

    private ProcessorRecipe _recipeData;
    private string _tutorialID;

    private void OnEnable()
    {
        ResourceManager.OnResourceAmountChanged += HandleResourceChange;
    }

    private void OnDisable()
    {
        ResourceManager.OnResourceAmountChanged -= HandleResourceChange;
    }

    public void Initialize(ActiveRecipe activeRecipe)
    {
        _activeRecipe = activeRecipe;
        _recipeData = _activeRecipe.recipeData;

        recipeName.text = $"{GetKoreanResourceType(_recipeData.resourceType)}";
        recipeIcon.sprite = _recipeData.recipeIcon;
        _ingredients = _recipeData.ingredients;
        _produceMaxAmount = _activeRecipe.maxProductionLimit;

        _tutorialID = $"processor_{_recipeData.resourceType}";
        if (TutorialManager.Instance != null) {
            TutorialManager.Instance.RegisterRuntimeUI(_tutorialID, gameObject, glowMaterial);
        }

        UpdateCurrentAmount();
        UpdateUI();

        foreach (ResourceCost ingredient in _ingredients) {
            GameObject newCellObject = Instantiate(recipeCellPrefab, contentParent);
            ResourceInfoCell newCell = newCellObject.GetComponent<ResourceInfoCell>();

            if (newCell != null) {
                newCell.SetInfo(ingredient.resourceType, ingredient.amount, false);
            }
        }

        foreach (Transform child in contentParent) {
            ResourceInfoCell cell = child.GetComponent<ResourceInfoCell>();
            if (cell != null) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
    }

    private string GetKoreanResourceType(ResourceType resourceType)
    {
        return resourceType switch {
            ResourceType.Ferrite => "복합 철재",
            ResourceType.Aether => "에테르 가스",
            ResourceType.Biomass => "바이오매스",
            ResourceType.CryoCrystal => "극저온 결정",
            ResourceType.AlloyPlate => "합금 판",
            ResourceType.CompositeFrame => "복합 골조",
            ResourceType.EChip => "전자 칩",
            ResourceType.BioCable => "바이오 케이블",
            ResourceType.PowerCube => "동력 큐브",
            ResourceType.BioFuel => "바이오 연료",
            ResourceType.CryoGel => "극저온 용액",
            ResourceType.Solana => "솔라나 정수",
            ResourceType.Core => "코어 프로세서",
            ResourceType.Ammunition => "표준 탄약",
            ResourceType.HeavyPlating => "중장갑 판",
            ResourceType.Actuator => "액추에이터",
            ResourceType.GenomeChip => "유전자 데이터 칩",
            ResourceType.PatchKit => "수리 키트",
            ResourceType.SensorUnit => "센서 유닛",
            ResourceType.PlasmaCube => "플라즈마 큐브",
            ResourceType.CryoConduit => "초전도 도관",
            ResourceType.SeekerMissile => "추적 탄두",
            ResourceType.NexusData => "넥서스 데이터",
            ResourceType.NeuralMatrix => "신경 매트릭스",
            _ => resourceType.ToString()
        };
    }

    private void HandleResourceChange(ResourceType type, int newAmount)
    {
        if (_recipeData != null && type == _recipeData.resourceType) {
            _currentStorageAmount = newAmount;
            UpdateUI();

            _activeRecipe.SetProductionLimit(_produceMaxAmount);
        }
    }

    private void UpdateCurrentAmount()
    {
        if (_recipeData == null) return;
        _currentStorageAmount = ResourceManager.Instance.GetResourceAmount(_recipeData.resourceType);
        Debug.Log(_recipeData.resourceType + " Current Storage Amount: " + _currentStorageAmount);
    }

    private void UpdateUI()
    {
        produceInfoText.text = $"{_currentStorageAmount} / {_produceMaxAmount}";
    }

    private int GetAmountChange()
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
            return 100;
        }
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) {
            return 10;
        }
        return 1;
    }

    public void OnPlusBtnClick()
    {
        float currentTime = Time.unscaledTime;
        if (currentTime - _lastButtonClickTime < ButtonClickCooldown) {
            return;
        }
        _lastButtonClickTime = currentTime;

        int amountToAdd = GetAmountChange();
        int newAmount = Mathf.Min(_produceMaxAmount + amountToAdd, MaxProduceAmount);

        if (_produceMaxAmount != newAmount) {
            _produceMaxAmount = newAmount;
            UpdateUI();

            _activeRecipe.SetProductionLimit(_produceMaxAmount);
        }

        if (TutorialManager.Instance != null) {
            Transform panelParent = transform.parent;
            while (panelParent != null && !panelParent.name.Contains("Panel") && !panelParent.name.Contains("panel")) {
                panelParent = panelParent.parent;
            }
            if (panelParent != null) {
                TutorialManager.Instance.DisableHighlightForTarget(panelParent.gameObject);
            }
        }

        if (!plusButtonSound.IsNull) {
            RuntimeManager.PlayOneShot(plusButtonSound);
        }

        if (TutorialManager.Instance != null) {
            TutorialManager.Instance.DisableHighlightForTarget(gameObject);
        }
    }

    public void OnMinusBtnClick()
    {
        float currentTime = Time.unscaledTime;
        if (currentTime - _lastButtonClickTime < ButtonClickCooldown) {
            return;
        }
        _lastButtonClickTime = currentTime;

        int amountToSubtract = GetAmountChange();
        int newAmount = Mathf.Max(_produceMaxAmount - amountToSubtract, 0);

        if (_produceMaxAmount != newAmount) {
            _produceMaxAmount = newAmount;
            UpdateUI();

            _activeRecipe.SetProductionLimit(_produceMaxAmount);
        }

        if (TutorialManager.Instance != null) {
            Transform panelParent = transform.parent;
            while (panelParent != null && !panelParent.name.Contains("Panel") && !panelParent.name.Contains("panel")) {
                panelParent = panelParent.parent;
            }
            if (panelParent != null) {
                TutorialManager.Instance.DisableHighlightForTarget(panelParent.gameObject);
            }
        }

        if (!minusButtonSound.IsNull) {
            RuntimeManager.PlayOneShot(minusButtonSound);
        }
    }
}
