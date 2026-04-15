using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResourceStatsUIController : MonoBehaviour
{
    [Header("Cell Setup")]
    [SerializeField] private UnitProcessorActivityCellView resourceSliderCellPrefab;
    [SerializeField] private Transform produceContentParent;
    [SerializeField] private Transform spendContentParent;

    [Header("Sampling")]
    [SerializeField] private float movingAverageWindowMinutes = 5f;
    [SerializeField] private float refreshInterval = 0.5f;

    private float _nextRefresh;
    private readonly List<UnitProcessorActivityCellView> _spawnedCells = new List<UnitProcessorActivityCellView>();
    private readonly List<ResourceType> _produceTypes = new List<ResourceType>();
    private readonly List<ResourceType> _spendTypes = new List<ResourceType>();
    private readonly Dictionary<ResourceType, UnitProcessorActivityCellView> _produceCells = new Dictionary<ResourceType, UnitProcessorActivityCellView>();
    private readonly Dictionary<ResourceType, UnitProcessorActivityCellView> _spendCells = new Dictionary<ResourceType, UnitProcessorActivityCellView>();

    protected virtual void OnEnable()
    {
        _nextRefresh = 0f;
        BuildCellListsIfNeeded();
        Refresh();
    }

    protected virtual void OnDisable()
    {
        ClearSpawnedCells();
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
        if (resourceSliderCellPrefab == null || produceContentParent == null || spendContentParent == null)
        {
            return;
        }

        if (_produceCells.Count == 0 || _spendCells.Count == 0)
        {
            BuildCellListsIfNeeded();
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

    private void BuildCellListsIfNeeded()
    {
        if (resourceSliderCellPrefab == null || produceContentParent == null || spendContentParent == null)
        {
            return;
        }

        ClearSpawnedCells();
        ClearExistingCellsUnderParent(produceContentParent);
        ClearExistingCellsUnderParent(spendContentParent);
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
            if (type != ResourceType.Electricity)
            {
                _spendTypes.Add(type);
            }

            if (type != ResourceType.Electricity)
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

    private static void ClearExistingCellsUnderParent(Transform parent)
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
