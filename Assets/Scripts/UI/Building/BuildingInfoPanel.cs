using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class BuildingInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text buildingName;
    [SerializeField] private GameObject resourcePanel;
    [SerializeField] private GameObject postConstructPanel;
    [SerializeField] private TMP_Text buildingDesc;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private GameObject resourceInfoCellPrefab;

    [Header("Aether & Noise")]
    [SerializeField] private GameObject aetherSpendPanel;
    [SerializeField] private TMP_Text electricityConsumptionText;
    [SerializeField] private GameObject noisePanel;
    [SerializeField] private TMP_Text noiseText;

    [Header("Resource generator (post-build)")]
    [SerializeField] private GameObject resourceGeneratorBatteryPanel;
    [SerializeField] private Slider generatorBatterySlider;
    [SerializeField] private TMP_Text generatorBatteryAmountText;
    [SerializeField] private TMP_Text generatorPowerStatusText;
    [SerializeField] private GameObject generatorPowerStatusOkIcon;
    [SerializeField] private GameObject generatorPowerStatusProblemIcon;
    [SerializeField] private Color generatorBatterySliderColorOk = new Color(0.25f, 0.78f, 0.35f, 1f);
    [SerializeField] private Color generatorBatterySliderColorWarn = new Color(1f, 0.75f, 0.2f, 1f);
    [SerializeField] private string generatorStatusTextProducing = "\uC804\uB825 \uC0DD\uC0B0 \uC911";
    [SerializeField] private string generatorStatusTextBufferFull = "\uC804\uB825 \uCDA9\uBD84";
    [SerializeField] private string generatorStatusTextNoFuel = "\uC790\uC6D0 \uBD80\uC871";

    private BuildingData _selectedData;
    private Damageable _currentDamageable;
    private bool _showMaxHealthOnly;
    private Dictionary<BuildingPieceType, BuildingPieceData> _pieceDataByType;
    private ResourceGenerator _generatorForBatteryPanel;

    public static BuildingInfoPanel Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ClearAllInfo();
    }

    private void Update()
    {
        if (resourceGeneratorBatteryPanel == null || !resourceGeneratorBatteryPanel.activeSelf)
        {
            return;
        }
        if (_generatorForBatteryPanel == null || !_generatorForBatteryPanel)
        {
            ClearGeneratorBatteryPanel();
            return;
        }
        RefreshGeneratorBatteryPanel(_generatorForBatteryPanel);
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        ClearGeneratorBatteryPanel();
        SetCurrentDamageable(null, false);
    }

    public void SelectBuilding(BuildingData data)
    {
        _selectedData = data;
        Damageable damageable = data != null && data.buildingPrefab != null ? data.buildingPrefab.GetComponent<Damageable>() : null;
        UpdateUI(data, false, null, damageable);
        SetCurrentDamageable(damageable, true);
        if (damageable != null)
            UpdateHealthDisplay(damageable, true);
    }

    public void PreviewInfo(BuildingData data, Damageable damageable = null, bool showMaxHealthOnly = false)
    {
        PreviewInfo(data, damageable, showMaxHealthOnly, false, null);
    }

    public void PreviewInfo(
        BuildingData data,
        Damageable damageable,
        bool showMaxHealthOnly,
        bool showPostConstructInfo,
        IElectricityConsumer runtimeConsumer = null)
    {
        UpdateUI(data, showPostConstructInfo, runtimeConsumer, damageable);
        SetCurrentDamageable(damageable, showMaxHealthOnly);
        if (damageable != null)
            UpdateHealthDisplay(damageable, showMaxHealthOnly);
    }

    public void CancelPreview()
    {
        if (_selectedData != null)
        {
            Damageable damageable = _selectedData.buildingPrefab != null ? _selectedData.buildingPrefab.GetComponent<Damageable>() : null;
            UpdateUI(_selectedData, false, null, damageable);
            SetCurrentDamageable(damageable, true);
            if (damageable != null)
                UpdateHealthDisplay(damageable, true);
        }
        else
        {
            SetCurrentDamageable(null, false);
            ClearUI();
        }
    }

    public void ClearAllInfo()
    {
        _selectedData = null;
        SetCurrentDamageable(null, false);
        ClearUI();
    }

    private void SetCurrentDamageable(Damageable damageable, bool showMaxHealthOnly)
    {
        if (_currentDamageable != null)
            _currentDamageable.HealthChanged -= OnCurrentDamageableHealthChanged;
        _currentDamageable = damageable;
        _showMaxHealthOnly = showMaxHealthOnly;
        if (_currentDamageable != null)
            _currentDamageable.HealthChanged += OnCurrentDamageableHealthChanged;
    }

    private void OnCurrentDamageableHealthChanged()
    {
        if (_currentDamageable != null)
            UpdateHealthDisplay(_currentDamageable, _showMaxHealthOnly);
    }

    private void UpdateUI(BuildingData data, bool showPostConstructInfo, IElectricityConsumer runtimeConsumer, Damageable runtimeContextDamageable = null)
    {
        if (data == null) return;

        if (buildingName != null) buildingName.text = data.GetDisplayName();
        if (buildingDesc != null) buildingDesc.text = data.GetDescription();
        gameObject.SetActive(true);

        bool shouldShowPostConstructInfo = showPostConstructInfo && data.buildingType != BuildingType.MainStructure;
        if (shouldShowPostConstructInfo)
        {
            SetResourcePanelActive(false);
            SetPostConstructPanelActive(true);
            UpdatePostConstructDisplay(data, runtimeConsumer);
            SyncResourceGeneratorBatteryPanel(data, runtimeContextDamageable);
        }
        else
        {
            ClearGeneratorBatteryPanel();
            SetPostConstructPanelActive(false);
            if (data.buildingType == BuildingType.MainStructure)
            {
                if (resourcePanel != null)
                {
                    foreach (Transform child in resourcePanel.transform)
                    {
                        Destroy(child.gameObject);
                    }
                }
                SetResourcePanelActive(false);
            }
            else
            {
                SetResourcePanelActive(true);
                UpdateResourceDisplay(data);
            }
            ClearPostConstructTexts();
        }
        RebuildPanelLayout();
    }

    private void RebuildPanelLayout()
    {
        RectTransform rt = transform as RectTransform;
        if (rt != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    private void UpdatePostConstructDisplay(BuildingData data, IElectricityConsumer runtimeConsumer)
    {
        if (noisePanel != null) noisePanel.SetActive(true);
        if (aetherSpendPanel != null) aetherSpendPanel.SetActive(true);

        float noiseCoefficient = data != null ? data.noiseCoefficient : 0f;
        if (noiseText != null) noiseText.text = $"{noiseCoefficient:F1}";

        float electricityConsumption = GetElectricityConsumptionPerSecond(data, runtimeConsumer);
        if (electricityConsumptionText != null) electricityConsumptionText.text = $"{electricityConsumption:F1} / s";
    }

    private void UpdateHealthDisplay(Damageable damageable, bool showMaxHealth = false)
    {
        if (healthText == null) return;
        if (damageable == null)
        {
            healthText.text = string.Empty;
            RebuildPanelLayout();
            return;
        }

        if (showMaxHealth)
        {
            healthText.text = $"체력 : {damageable.MaxHealth}";
        }
        else
        {
            healthText.text = $"{damageable.CurrentHealth} / {damageable.MaxHealth}";
        }
        RebuildPanelLayout();
    }

    private void ClearUI()
    {
        ClearGeneratorBatteryPanel();
        if (buildingName != null) buildingName.text = string.Empty;
        if (buildingDesc != null) buildingDesc.text = string.Empty;
        if (healthText != null) healthText.text = string.Empty;
        if (resourcePanel != null)
        {
            foreach (Transform child in resourcePanel.transform)
            {
                Destroy(child.gameObject);
            }
        }
        SetResourcePanelActive(false);
        SetPostConstructPanelActive(false);
        HideAetherAndNoiseDisplay();
        RebuildPanelLayout();
    }

    private void UpdateResourceDisplay(BuildingData data)
    {
        if (resourcePanel == null)
        {
            return;
        }

        foreach (Transform child in resourcePanel.transform)
        {
            Destroy(child.gameObject);
        }

        if (resourceInfoCellPrefab == null)
        {
            Debug.LogError(
                "BuildingInfoPanel: resourceInfoCellPrefab is missing. Assign Assets/Prefabs/UI/Panels/Resource Info Cell.prefab on this component.");
            return;
        }

        if (data.recipe == null || data.recipe.Count == 0)
        {
            return;
        }

        Dictionary<ResourceType, int> totalCosts = new Dictionary<ResourceType, int>();

        foreach (var piece in data.recipe)
        {
            BuildingPieceData pieceData = GetPieceDataByType(piece.buildingPieceType);
            if (pieceData == null)
            {
                continue;
            }

            if (pieceData.costs != null)
            {
                foreach (var cost in pieceData.costs)
                {
                    if (totalCosts.ContainsKey(cost.resourceType))
                    {
                        totalCosts[cost.resourceType] += cost.amount;
                    }
                    else
                    {
                        totalCosts[cost.resourceType] = cost.amount;
                    }
                }
            }
        }

        foreach (var kvp in totalCosts.OrderBy(x => (int)x.Key))
        {
            ResourceType type = kvp.Key;
            int amount = kvp.Value;

            GameObject cellObj = Instantiate(resourceInfoCellPrefab, resourcePanel.transform);
            ResourceInfoCell cell = cellObj.GetComponent<ResourceInfoCell>();

            if (cell != null)
            {
                cell.SetInfo(type, amount, false);
            }
        }
    }

    private BuildingPieceData GetPieceDataByType(BuildingPieceType type)
    {
        EnsurePieceDataCache();
        if (_pieceDataByType != null && _pieceDataByType.TryGetValue(type, out BuildingPieceData data))
        {
            return data;
        }

        return null;
    }

    private void EnsurePieceDataCache()
    {
        if (_pieceDataByType != null)
        {
            return;
        }

        _pieceDataByType = new Dictionary<BuildingPieceType, BuildingPieceData>();
        BuildingPieceData[] allData = Resources.LoadAll<BuildingPieceData>("Building Piece Data");
        foreach (BuildingPieceData data in allData)
        {
            if (data == null || data.buildingPieceType == BuildingPieceType.None)
            {
                continue;
            }

            if (!_pieceDataByType.ContainsKey(data.buildingPieceType))
            {
                _pieceDataByType[data.buildingPieceType] = data;
            }
        }
    }

    public void ClearInfo()
    {
        _selectedData = null;
        SetCurrentDamageable(null, false);
        ClearGeneratorBatteryPanel();

        if (buildingName != null)
        {
            buildingName.text = string.Empty;
        }

        if (buildingDesc != null)
        {
            buildingDesc.text = string.Empty;
        }

        if (healthText != null)
        {
            healthText.text = string.Empty;
        }

        if (resourcePanel != null)
        {
            SetResourcePanelActive(false);
        }
        SetPostConstructPanelActive(false);
        HideAetherAndNoiseDisplay();
        RebuildPanelLayout();
    }

    private void HideAetherAndNoiseDisplay()
    {
        if (aetherSpendPanel != null) aetherSpendPanel.SetActive(false);
        if (noisePanel != null) noisePanel.SetActive(false);
        ClearPostConstructTexts();
    }

    private void SetResourcePanelActive(bool isActive)
    {
        if (resourcePanel != null) resourcePanel.SetActive(isActive);
    }

    private void SetPostConstructPanelActive(bool isActive)
    {
        if (postConstructPanel == null)
        {
            ResolvePostConstructPanelReference();
        }

        if (postConstructPanel != null)
        {
            postConstructPanel.SetActive(isActive);
        }
    }

    private void ResolvePostConstructPanelReference()
    {
        if (aetherSpendPanel != null && aetherSpendPanel.transform.parent != null)
        {
            postConstructPanel = aetherSpendPanel.transform.parent.gameObject;
            return;
        }

        if (noisePanel != null && noisePanel.transform.parent != null)
        {
            postConstructPanel = noisePanel.transform.parent.gameObject;
        }
    }

    private void ClearPostConstructTexts()
    {
        if (electricityConsumptionText != null) electricityConsumptionText.text = string.Empty;
        if (noiseText != null) noiseText.text = string.Empty;
    }

    private static float GetElectricityConsumptionPerSecond(BuildingData data, IElectricityConsumer runtimeConsumer)
    {
        if (runtimeConsumer != null)
        {
            return runtimeConsumer.ElectricityConsumptionPerSecond;
        }

        if (data == null || data.buildingPrefab == null)
        {
            return 0f;
        }

        IElectricityConsumer prefabConsumer = data.buildingPrefab.GetComponent<IElectricityConsumer>();
        if (prefabConsumer == null)
        {
            prefabConsumer = data.buildingPrefab.GetComponentInChildren<IElectricityConsumer>();
        }

        return prefabConsumer != null ? prefabConsumer.ElectricityConsumptionPerSecond : 0f;
    }

    private void SyncResourceGeneratorBatteryPanel(BuildingData data, Damageable runtimeContextDamageable)
    {
        if (resourceGeneratorBatteryPanel == null || data == null || data.buildingType != BuildingType.Generator)
        {
            ClearGeneratorBatteryPanel();
            return;
        }
        if (runtimeContextDamageable == null)
        {
            ClearGeneratorBatteryPanel();
            return;
        }
        ResourceGenerator rg = runtimeContextDamageable.GetComponent<ResourceGenerator>();
        if (rg == null)
        {
            rg = runtimeContextDamageable.GetComponentInChildren<ResourceGenerator>(true);
        }
        if (rg == null || !rg.IsConstructed)
        {
            ClearGeneratorBatteryPanel();
            return;
        }
        _generatorForBatteryPanel = rg;
        resourceGeneratorBatteryPanel.SetActive(true);
        RefreshGeneratorBatteryPanel(rg);
    }

    private void RefreshGeneratorBatteryPanel(ResourceGenerator rg)
    {
        if (rg == null || !rg)
        {
            ClearGeneratorBatteryPanel();
            return;
        }
        int maxCap = Mathf.Max(1, rg.ElectricityBufferMax);
        int current = rg.ElectricityBufferCurrent;
        float ratio = Mathf.Clamp01(current / (float)maxCap);
        if (generatorBatterySlider != null)
        {
            generatorBatterySlider.minValue = 0f;
            generatorBatterySlider.maxValue = 1f;
            generatorBatterySlider.SetValueWithoutNotify(ratio);
            generatorBatterySlider.interactable = false;
        }
        if (generatorBatteryAmountText != null)
        {
            generatorBatteryAmountText.text = BatteryAmountTextFormatter.Format(current, rg.ElectricityBufferMax);
        }
        bool fuelOk = rg.HasFuelAvailableInRange();
        bool bufferFull = current >= rg.ElectricityBufferMax;
        bool showOkIcon;
        bool showProblemIcon;
        string statusText;
        if (!fuelOk)
        {
            showOkIcon = false;
            showProblemIcon = true;
            statusText = generatorStatusTextNoFuel;
        }
        else if (bufferFull)
        {
            showOkIcon = true;
            showProblemIcon = false;
            statusText = generatorStatusTextBufferFull;
        }
        else
        {
            showOkIcon = true;
            showProblemIcon = false;
            statusText = generatorStatusTextProducing;
        }
        if (generatorPowerStatusText != null)
        {
            generatorPowerStatusText.text = statusText;
        }
        if (generatorPowerStatusOkIcon != null)
        {
            generatorPowerStatusOkIcon.SetActive(showOkIcon);
        }
        if (generatorPowerStatusProblemIcon != null)
        {
            generatorPowerStatusProblemIcon.SetActive(showProblemIcon);
        }
        if (generatorBatterySlider != null)
        {
            BatterySliderFillColorUtility.ApplyDiscreteByRatio(
                generatorBatterySlider,
                showProblemIcon ? 0f : 1f,
                generatorBatterySliderColorWarn,
                generatorBatterySliderColorOk);
        }
    }

    private void ClearGeneratorBatteryPanel()
    {
        _generatorForBatteryPanel = null;
        if (resourceGeneratorBatteryPanel != null)
        {
            resourceGeneratorBatteryPanel.SetActive(false);
        }
        if (generatorBatteryAmountText != null)
        {
            generatorBatteryAmountText.text = string.Empty;
        }
        if (generatorPowerStatusText != null)
        {
            generatorPowerStatusText.text = string.Empty;
        }
        if (generatorPowerStatusOkIcon != null)
        {
            generatorPowerStatusOkIcon.SetActive(false);
        }
        if (generatorPowerStatusProblemIcon != null)
        {
            generatorPowerStatusProblemIcon.SetActive(false);
        }
        if (generatorBatterySlider != null)
        {
            generatorBatterySlider.SetValueWithoutNotify(0f);
            BatterySliderFillColorUtility.ApplyDiscreteByRatio(
                generatorBatterySlider,
                0f,
                generatorBatterySliderColorWarn,
                generatorBatterySliderColorOk);
        }
    }

    public void WarmupFirstUse()
    {
        bool wasActive = gameObject.activeSelf;
        gameObject.SetActive(true);
        WarmTouchTmp(buildingName);
        WarmTouchTmp(buildingDesc);
        WarmTouchTmp(healthText);
        WarmTouchTmp(electricityConsumptionText);
        WarmTouchTmp(noiseText);
        WarmTouchTmp(generatorBatteryAmountText);
        WarmTouchTmp(generatorPowerStatusText);
        if (resourcePanel != null && resourceInfoCellPrefab != null)
        {
            GameObject cellObj = Instantiate(resourceInfoCellPrefab, resourcePanel.transform);
            ResourceInfoCell cell = cellObj.GetComponent<ResourceInfoCell>();
            if (cell != null)
            {
                cell.SetInfo(ResourceType.Ferrite, 1, false);
            }
            RectTransform resourceRt = resourcePanel.GetComponent<RectTransform>();
            if (resourceRt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(resourceRt);
            }
            Destroy(cellObj);
        }
        RebuildPanelLayout();
        Canvas.ForceUpdateCanvases();
        ClearAllInfo();
        if (!wasActive)
        {
            gameObject.SetActive(false);
        }
    }

    private static void WarmTouchTmp(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }
        text.text = " ";
        text.ForceMeshUpdate(true);
        text.text = string.Empty;
    }

    private void HandleLocaleChanged(Locale _)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (_selectedData != null)
        {
            Damageable damageable = _selectedData.buildingPrefab != null ? _selectedData.buildingPrefab.GetComponent<Damageable>() : null;
            UpdateUI(_selectedData, false, null, damageable);
            return;
        }

        if (_currentDamageable != null)
        {
            BuildingDataHolder holder = _currentDamageable.GetComponent<BuildingDataHolder>();
            if (holder != null && holder.buildingData != null)
            {
                UpdateUI(holder.buildingData, false, null, _currentDamageable);
            }
        }
    }

}
