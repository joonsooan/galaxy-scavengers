using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class ResourceStatsUIController : MonoBehaviour
{
    private const int TabCount = 2;
    private static int s_lastSelectedTabIndex;

    [Header("Tab Setup")]
    [SerializeField] private Button resourceTabButton;
    [SerializeField] private Button electricityTabButton;
    [SerializeField] private GameObject resourceTabPanel;
    [SerializeField] private GameObject electricityTabPanel;

    [Header("Resource Cell Setup")]
    [SerializeField] private UnitProcessorActivityCellView resourceSliderCellPrefab;
    [SerializeField] private Transform produceContentParent;
    [SerializeField] private Transform spendContentParent;

    [Header("Electricity Cell Setup")]
    [SerializeField] private ElectricityInfoCellView electricityInfoCellPrefab;
    [SerializeField] private Transform electricityProduceContentParent;
    [SerializeField] private Transform electricitySpendContentParent;

    [Header("Sampling")]
    [SerializeField] private float movingAverageWindowMinutes = 5f;
    [SerializeField] private float refreshInterval = 0.5f;

    [Header("Resource Filter")]
    [SerializeField] private ResourceType maxVisibleResourceType = ResourceType.AlloyPlate;

    private float _nextRefresh;
    private readonly List<UnitProcessorActivityCellView> _spawnedCells = new List<UnitProcessorActivityCellView>();
    private readonly List<ResourceType> _produceTypes = new List<ResourceType>();
    private readonly List<ResourceType> _spendTypes = new List<ResourceType>();
    private readonly Dictionary<ResourceType, UnitProcessorActivityCellView> _produceCells = new Dictionary<ResourceType, UnitProcessorActivityCellView>();
    private readonly Dictionary<ResourceType, UnitProcessorActivityCellView> _spendCells = new Dictionary<ResourceType, UnitProcessorActivityCellView>();
    private readonly List<ElectricityInfoCellView> _spawnedElectricityCells = new List<ElectricityInfoCellView>();
    private readonly Dictionary<BuildingType, ElectricityInfoCellView> _electricityProduceCells = new Dictionary<BuildingType, ElectricityInfoCellView>();
    private readonly Dictionary<BuildingType, ElectricityInfoCellView> _electricitySpendCells = new Dictionary<BuildingType, ElectricityInfoCellView>();

    private struct ElectricityAggregate
    {
        public int count;
        public float perMinute;
        public Sprite icon;
    }

    protected virtual void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        _nextRefresh = 0f;
        BuildResourceCellListsIfNeeded();
        WireTabButtons(true);
        ApplyLocalizedStaticTexts();
        ShowTab(GetValidTabIndex(s_lastSelectedTabIndex));
    }

    protected virtual void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        WireTabButtons(false);
        ClearSpawnedCells();
        ClearSpawnedElectricityCells();
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyLocalizedStaticTexts();
    }

    private void ApplyLocalizedStaticTexts()
    {
        SetTextByName("stats_title_text", "resourceStats.title", "자원 통계");
        SetTextByName("resource_stats_text", "resourceStats.resourceTab", "자원");
        SetTextByName("elec_stats_text", "resourceStats.electricityTab", "전력");
        SetTextByName("resource_produce_text", "resourceStats.resourceProduce", "자원 생산량");
        SetTextByName("resource_spend_text", "resourceStats.resourceSpend", "자원 소비량");
        SetTextByName("elec_produce_text", "resourceStats.electricityProduce", "전력 생산량");
        SetTextByName("elec_spend_text", "resourceStats.electricitySpend", "전력 소비량");
    }

    private void SetTextByName(string objectName, string key, string fallback)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text != null && text.gameObject.name == objectName)
            {
                text.text = GameLocalization.GetOrDefault("UI_Common", key, fallback);
                return;
            }
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh)
        {
            return;
        }

        _nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    public void Refresh()
    {
        if (GetValidTabIndex(s_lastSelectedTabIndex) == 0)
        {
            RefreshResourceTab();
        }
        else
        {
            RefreshElectricityTab();
        }
    }

    private void RefreshResourceTab()
    {
        if (resourceSliderCellPrefab == null || produceContentParent == null || spendContentParent == null)
        {
            return;
        }

        if (_produceCells.Count == 0 || _spendCells.Count == 0)
        {
            BuildResourceCellListsIfNeeded();
        }

        float safeWindow = Mathf.Max(0.01f, movingAverageWindowMinutes);
        ResourceManager rm = ResourceManager.Instance;
        UpdatePanel(
            _produceTypes,
            _produceCells,
            UnitProcessStatKind.Produce,
            true,
            safeWindow,
            rm);
        UpdatePanel(
            _spendTypes,
            _spendCells,
            UnitProcessStatKind.Spend,
            true,
            safeWindow,
            rm);
    }

    private void RefreshElectricityTab()
    {
        if (electricityInfoCellPrefab == null || electricityProduceContentParent == null || electricitySpendContentParent == null)
        {
            return;
        }

        Dictionary<BuildingType, ElectricityAggregate> produce = BuildElectricityProduceAggregates();
        Dictionary<BuildingType, ElectricityAggregate> spend = BuildElectricitySpendAggregates();

        EnsureElectricityCells(produce.Keys, electricityProduceContentParent, _electricityProduceCells);
        EnsureElectricityCells(spend.Keys, electricitySpendContentParent, _electricitySpendCells);

        UpdateElectricityPanel(produce, _electricityProduceCells);
        UpdateElectricityPanel(spend, _electricitySpendCells);
    }

    private void BuildResourceCellListsIfNeeded()
    {
        if (resourceSliderCellPrefab == null || produceContentParent == null || spendContentParent == null)
        {
            return;
        }

        ClearSpawnedCells();
        ClearExistingResourceCellsUnderParent(produceContentParent);
        ClearExistingResourceCellsUnderParent(spendContentParent);
        _produceCells.Clear();
        _spendCells.Clear();
        _produceTypes.Clear();
        _spendTypes.Clear();

        List<ResourceType> allTypes = Enum.GetValues(typeof(ResourceType))
            .Cast<ResourceType>()
            .OrderBy(type => (int)type)
            .ToList();

        foreach (ResourceType type in allTypes)
        {
            if (IsVisibleResourceType(type))
            {
                _spendTypes.Add(type);
            }

            if (IsVisibleResourceType(type))
            {
                _produceTypes.Add(type);
            }
        }

        SpawnCells(_produceTypes, produceContentParent, _produceCells);
        SpawnCells(_spendTypes, spendContentParent, _spendCells);
    }

    private void SpawnCells(
        List<ResourceType> types,
        Transform parent,
        Dictionary<ResourceType, UnitProcessorActivityCellView> destination)
    {
        foreach (ResourceType type in types)
        {
            UnitProcessorActivityCellView cell = Instantiate(resourceSliderCellPrefab, parent);
            cell.SetVisible(true);
            destination[type] = cell;
            _spawnedCells.Add(cell);
        }
    }

    private void UpdatePanel(
        List<ResourceType> types,
        Dictionary<ResourceType, UnitProcessorActivityCellView> cellsByType,
        UnitProcessStatKind statKind,
        bool excludePowerFuelSpend,
        float windowMinutes,
        ResourceManager resourceManager)
    {
        Dictionary<ResourceType, float> perMinuteByType = new Dictionary<ResourceType, float>(types.Count);
        float maxPerMinute = 0f;

        foreach (ResourceType type in types)
        {
            float perMinute = UnitProcessResourceStatTracker.GetPerMinuteAverage(
                type,
                statKind,
                windowMinutes,
                excludePowerFuelSpend);
            perMinuteByType[type] = perMinute;
            if (perMinute > maxPerMinute)
            {
                maxPerMinute = perMinute;
            }
        }

        List<ResourceType> sortedTypes = types
            .OrderByDescending(type => perMinuteByType[type])
            .ThenBy(type => (int)type)
            .ToList();

        for (int index = 0; index < sortedTypes.Count; index++)
        {
            ResourceType type = sortedTypes[index];
            if (cellsByType.TryGetValue(type, out UnitProcessorActivityCellView cell) && cell != null)
            {
                cell.transform.SetSiblingIndex(index);
            }
        }

        foreach (ResourceType type in sortedTypes)
        {
            if (!cellsByType.TryGetValue(type, out UnitProcessorActivityCellView cell) || cell == null)
            {
                continue;
            }

            float value = perMinuteByType[type];
            float normalized = maxPerMinute > 0f ? value / maxPerMinute : 0f;
            Sprite icon = resourceManager != null ? resourceManager.GetResourceIcon(type) : null;
            cell.SetData(icon, normalized, value);
        }
    }

    private Dictionary<BuildingType, ElectricityAggregate> BuildElectricityProduceAggregates()
    {
        Dictionary<BuildingType, ElectricityAggregate> aggregates = new Dictionary<BuildingType, ElectricityAggregate>();
        ResourceGenerator[] generators = FindObjectsByType<ResourceGenerator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < generators.Length; i++)
        {
            ResourceGenerator generator = generators[i];
            if (generator == null || !generator.isActiveAndEnabled || !generator.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!BuildingManager.IsBuildingProperlyPlaced(generator.transform) || !generator.IsConstructed)
            {
                continue;
            }

            if (!TryGetBuildingInfo(generator, out BuildingType type, out Sprite icon))
            {
                continue;
            }
            if (type == BuildingType.MainStructure)
            {
                continue;
            }

            float perSecond = GetGeneratorProducePerSecond(generator);
            ElectricityAggregate current = aggregates.TryGetValue(type, out ElectricityAggregate existing)
                ? existing
                : new ElectricityAggregate { icon = icon };

            current.icon = current.icon != null ? current.icon : icon;
            current.count += 1;
            current.perMinute += Mathf.Max(0f, perSecond) * 60f;
            aggregates[type] = current;
        }

        return aggregates;
    }

    private Dictionary<BuildingType, ElectricityAggregate> BuildElectricitySpendAggregates()
    {
        Dictionary<BuildingType, ElectricityAggregate> aggregates = new Dictionary<BuildingType, ElectricityAggregate>();
        Damageable[] damageables = FindObjectsByType<Damageable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < damageables.Length; i++)
        {
            Damageable damageable = damageables[i];
            if (damageable is not IElectricityConsumer consumer)
            {
                continue;
            }

            if (!damageable.isActiveAndEnabled || !damageable.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!BuildingManager.IsBuildingProperlyPlaced(damageable.transform))
            {
                continue;
            }

            if (!TryGetBuildingInfo(damageable, out BuildingType type, out Sprite icon))
            {
                continue;
            }
            if (type == BuildingType.MainStructure)
            {
                continue;
            }

            float perSecond = Mathf.Max(0f, consumer.ElectricityConsumptionPerSecond);
            ElectricityAggregate current = aggregates.TryGetValue(type, out ElectricityAggregate existing)
                ? existing
                : new ElectricityAggregate { icon = icon };

            current.icon = current.icon != null ? current.icon : icon;
            current.count += 1;
            current.perMinute += perSecond * 60f;
            aggregates[type] = current;
        }

        return aggregates;
    }

    private static float GetGeneratorProducePerSecond(ResourceGenerator generator)
    {
        if (generator == null)
        {
            return 0f;
        }

        if (!generator.isActiveAndEnabled || !generator.gameObject.activeInHierarchy || !generator.IsConstructed)
        {
            return 0f;
        }

        if (generator.GenerationInterval <= 0f || generator.ElectricityBufferCurrent >= generator.ElectricityBufferMax)
        {
            return 0f;
        }

        if (!generator.HasFuelAvailableInRange())
        {
            return 0f;
        }

        return generator.ResourceAmount / generator.GenerationInterval;
    }

    private void EnsureElectricityCells(
        IEnumerable<BuildingType> targetTypes,
        Transform parent,
        Dictionary<BuildingType, ElectricityInfoCellView> destination)
    {
        if (parent == null || electricityInfoCellPrefab == null)
        {
            return;
        }

        HashSet<BuildingType> targetSet = new HashSet<BuildingType>(targetTypes);
        List<BuildingType> staleTypes = destination.Keys.Where(type => !targetSet.Contains(type)).ToList();
        for (int i = 0; i < staleTypes.Count; i++)
        {
            BuildingType staleType = staleTypes[i];
            if (destination.TryGetValue(staleType, out ElectricityInfoCellView staleCell) && staleCell != null)
            {
                _spawnedElectricityCells.Remove(staleCell);
                Destroy(staleCell.gameObject);
            }
            destination.Remove(staleType);
        }

        foreach (BuildingType type in targetSet)
        {
            if (destination.ContainsKey(type) && destination[type] != null)
            {
                continue;
            }

            ElectricityInfoCellView cell = Instantiate(electricityInfoCellPrefab, parent);
            cell.SetVisible(true);
            destination[type] = cell;
            _spawnedElectricityCells.Add(cell);
        }
    }

    private static void UpdateElectricityPanel(
        Dictionary<BuildingType, ElectricityAggregate> aggregates,
        Dictionary<BuildingType, ElectricityInfoCellView> cellsByType)
    {
        float maxPerMinute = 0f;
        foreach (KeyValuePair<BuildingType, ElectricityAggregate> pair in aggregates)
        {
            if (pair.Value.perMinute > maxPerMinute)
            {
                maxPerMinute = pair.Value.perMinute;
            }
        }

        List<BuildingType> sortedTypes = aggregates.Keys
            .OrderByDescending(type => aggregates[type].perMinute)
            .ThenBy(type => (int)type)
            .ToList();

        for (int i = 0; i < sortedTypes.Count; i++)
        {
            BuildingType type = sortedTypes[i];
            if (cellsByType.TryGetValue(type, out ElectricityInfoCellView cell) && cell != null)
            {
                cell.transform.SetSiblingIndex(i);
            }
        }

        foreach (BuildingType type in sortedTypes)
        {
            if (!cellsByType.TryGetValue(type, out ElectricityInfoCellView cell) || cell == null)
            {
                continue;
            }

            ElectricityAggregate aggregate = aggregates[type];
            float normalized = maxPerMinute > 0f ? aggregate.perMinute / maxPerMinute : 0f;
            cell.SetVisible(true);
            cell.SetData(aggregate.icon, aggregate.count, normalized, aggregate.perMinute);
        }
    }

    private static bool TryGetBuildingInfo(Component component, out BuildingType type, out Sprite icon)
    {
        type = default;
        icon = null;

        if (component == null)
        {
            return false;
        }

        BuildingDataHolder dataHolder = component.GetComponentInParent<BuildingDataHolder>();
        if (dataHolder == null || dataHolder.buildingData == null)
        {
            return false;
        }

        type = dataHolder.buildingData.buildingType;
        icon = dataHolder.buildingData.icon;
        return true;
    }

    private bool IsVisibleResourceType(ResourceType type)
    {
        return type != ResourceType.None &&
               type != ResourceType.Electricity &&
               type <= maxVisibleResourceType;
    }

    private void WireTabButtons(bool add)
    {
        if (resourceTabButton != null)
        {
            resourceTabButton.onClick.RemoveListener(OnResourceTabClicked);
            if (add)
            {
                resourceTabButton.onClick.AddListener(OnResourceTabClicked);
            }
        }

        if (electricityTabButton != null)
        {
            electricityTabButton.onClick.RemoveListener(OnElectricityTabClicked);
            if (add)
            {
                electricityTabButton.onClick.AddListener(OnElectricityTabClicked);
            }
        }
    }

    private void OnResourceTabClicked()
    {
        ShowTab(0);
    }

    private void OnElectricityTabClicked()
    {
        ShowTab(1);
    }

    private void ShowTab(int index)
    {
        int safeIndex = GetValidTabIndex(index);
        s_lastSelectedTabIndex = safeIndex;

        if (resourceTabPanel != null)
        {
            resourceTabPanel.SetActive(safeIndex == 0);
        }

        if (electricityTabPanel != null)
        {
            electricityTabPanel.SetActive(safeIndex == 1);
        }

        Refresh();
    }

    private static int GetValidTabIndex(int index)
    {
        return Mathf.Clamp(index, 0, TabCount - 1);
    }

    private void ClearSpawnedCells()
    {
        for (int i = 0; i < _spawnedCells.Count; i++)
        {
            UnitProcessorActivityCellView cell = _spawnedCells[i];
            if (cell != null)
            {
                Destroy(cell.gameObject);
            }
        }

        _spawnedCells.Clear();
    }

    private void ClearSpawnedElectricityCells()
    {
        for (int i = 0; i < _spawnedElectricityCells.Count; i++)
        {
            ElectricityInfoCellView cell = _spawnedElectricityCells[i];
            if (cell != null)
            {
                Destroy(cell.gameObject);
            }
        }

        _spawnedElectricityCells.Clear();
        _electricityProduceCells.Clear();
        _electricitySpendCells.Clear();
    }

    private static void ClearExistingResourceCellsUnderParent(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        List<UnitProcessorActivityCellView> existingCells = new List<UnitProcessorActivityCellView>();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            UnitProcessorActivityCellView cell = child.GetComponent<UnitProcessorActivityCellView>();
            if (cell != null)
            {
                existingCells.Add(cell);
            }
        }

        for (int i = 0; i < existingCells.Count; i++)
        {
            if (existingCells[i] != null)
            {
                Destroy(existingCells[i].gameObject);
            }
        }
    }
}
