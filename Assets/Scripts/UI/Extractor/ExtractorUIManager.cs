using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExtractorUIManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject inventoryCellIngamePrefab;

    [Header("Extractor Basic Info")]
    [SerializeField] private TMP_Text extractorTitleText;
    [SerializeField] private Slider overallDataSlider;
    [SerializeField] private TMP_Text dataPercentageText;

    [Header("Extractor Stats Storage")]
    [SerializeField] private TMP_Text connectedStorageTitleText;
    [SerializeField] private Transform connectedStorageContent;

    [Header("Extractor Stats Resources")]
    [SerializeField] private TMP_Text inputResourcesTitleText;
    [SerializeField] private Transform inputResourcesContent;
    [SerializeField] private Transform outputDataContent;
    [SerializeField] private Slider cycleProgressSlider;

    [Header("Extractor Stats Buffs (deferred)")]
    [SerializeField] private GameObject buffSectionRoot;

    [Header("Copy (optional overrides)")]
    [SerializeField] private string connectedStorageTitle = "\uC5F0\uACB0\uB41C \uC800\uC7A5\uACE0 / \uC790\uC6D0";
    [SerializeField] private string inputResourcesTitle = "\uD22C\uC785 \uAC00\uB2A5\uD55C \uC790\uC6D0";

    [Header("Toggle visuals")]
    [SerializeField] private Color selectedTierColor = new Color(0.4f, 0.75f, 1f, 1f);
    [SerializeField] private Color normalTierColor = new Color(1f, 1f, 1f, 1f);

    private DataExtractor _currentExtractor;
    private InventoryCell _outputDataCell;
    private readonly List<InventoryCell> _storageCells = new List<InventoryCell>();
    private readonly List<TierToggleBinding> _tierToggles = new List<TierToggleBinding>();

    private static class CellPool
    {
        public static void Clear(Transform parent, List<InventoryCell> track)
        {
            track.Clear();
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) {
                Destroy(parent.GetChild(i).gameObject);
            }
        }
    }

    private sealed class TierToggleBinding
    {
        public int TierIndex;
        public Image TargetGraphic;
        public Color NormalColor;
    }

    public static ExtractorUIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (buffSectionRoot != null) {
            buffSectionRoot.SetActive(false);
        }

        if (connectedStorageTitleText != null && !string.IsNullOrEmpty(connectedStorageTitle)) {
            connectedStorageTitleText.text = connectedStorageTitle;
        }

        if (inputResourcesTitleText != null && !string.IsNullOrEmpty(inputResourcesTitle)) {
            inputResourcesTitleText.text = inputResourcesTitle;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) {
            Instance = null;
        }

        UnbindExtractor();
    }

    public void ShowExtractorUI(DataExtractor extractor)
    {
        UnbindExtractor();
        _currentExtractor = extractor;

        if (_currentExtractor == null) {
            return;
        }

        gameObject.SetActive(true);

        _currentExtractor.OnExtractorStateChanged += OnExtractorStateChanged;
        ResourceManager.OnResourceAmountChanged += OnGlobalResourceChanged;

        RebuildStaticUi();
        RefreshTierSelectionVisuals();
        RefreshOutputCell();
        RefreshOverallUi();
        RefreshCycleUi();
    }

    public void HideExtractorUI()
    {
        gameObject.SetActive(false);
        UnbindExtractor();
    }

    private void UnbindExtractor()
    {
        ResourceManager.OnResourceAmountChanged -= OnGlobalResourceChanged;

        if (_currentExtractor != null) {
            _currentExtractor.OnExtractorStateChanged -= OnExtractorStateChanged;
            _currentExtractor = null;
        }

        CellPool.Clear(connectedStorageContent, _storageCells);
        CellPool.Clear(inputResourcesContent, new List<InventoryCell>());
        _tierToggles.Clear();

        if (outputDataContent != null) {
            for (int i = outputDataContent.childCount - 1; i >= 0; i--) {
                Destroy(outputDataContent.GetChild(i).gameObject);
            }
        }

        _outputDataCell = null;
    }

    private void OnExtractorStateChanged()
    {
        RefreshConnectedStorageCells();
        RefreshTierAmounts();
        RefreshTierSelectionVisuals();
        RefreshOverallUi();
        RefreshOutputCell();
    }

    private void OnGlobalResourceChanged(ResourceType type, int amount)
    {
        if (_currentExtractor == null || !gameObject.activeSelf) {
            return;
        }

        RefreshConnectedStorageCells();
        RefreshTierAmounts();
        RefreshOutputCell();
    }

    private void Update()
    {
        if (_currentExtractor == null || !gameObject.activeSelf) {
            return;
        }

        RefreshCycleUi();
        RefreshOverallUi();
    }

    private void RebuildStaticUi()
    {
        ExtractorData data = _currentExtractor.ExtractorDataAsset;
        if (extractorTitleText != null) {
            extractorTitleText.text = data != null ? data.displayName : string.Empty;
        }

        CellPool.Clear(connectedStorageContent, _storageCells);
        _tierToggles.Clear();
        if (inputResourcesContent != null) {
            for (int i = inputResourcesContent.childCount - 1; i >= 0; i--) {
                Destroy(inputResourcesContent.GetChild(i).gameObject);
            }
        }

        if (outputDataContent != null) {
            for (int i = outputDataContent.childCount - 1; i >= 0; i--) {
                Destroy(outputDataContent.GetChild(i).gameObject);
            }
        }

        _outputDataCell = null;

        if (data == null || inventoryCellIngamePrefab == null) {
            return;
        }

        RefreshConnectedStorageCells();
        BuildInputTierCells(data);
        BuildOutputCell(data);
    }

    private void RefreshConnectedStorageCells()
    {
        ExtractorData data = _currentExtractor.ExtractorDataAsset;
        if (data == null || connectedStorageContent == null || inventoryCellIngamePrefab == null) {
            return;
        }

        HashSet<ResourceType> typesWithStock = new HashSet<ResourceType>();
        if (data.inputTiers != null) {
            foreach (ExtractorInputTier tier in data.inputTiers) {
                if (tier == null) continue;
                int avail = _currentExtractor.GetAvailableInConnectedStorages(tier.inputResource);
                if (avail > 0) {
                    typesWithStock.Add(tier.inputResource);
                }
            }
        }

        List<ResourceType> sortedTypes = new List<ResourceType>(typesWithStock);
        sortedTypes.Sort((a, b) => a.CompareTo(b));

        bool needRebuild = _storageCells.Count != sortedTypes.Count;
        if (!needRebuild) {
            for (int i = 0; i < sortedTypes.Count; i++) {
                if (i >= _storageCells.Count || _storageCells[i].ResourceType != sortedTypes[i]) {
                    needRebuild = true;
                    break;
                }
            }
        }

        if (!needRebuild) {
            foreach (InventoryCell cell in _storageCells) {
                int amt = _currentExtractor.GetAvailableInConnectedStorages(cell.ResourceType);
                cell.SetResource(cell.ResourceType, amt);
            }
            return;
        }

        CellPool.Clear(connectedStorageContent, _storageCells);
        foreach (ResourceType t in sortedTypes) {
            GameObject go = Instantiate(inventoryCellIngamePrefab, connectedStorageContent);
            InventoryCell cell = go.GetComponent<InventoryCell>();
            if (cell != null) {
                int amt = _currentExtractor.GetAvailableInConnectedStorages(t);
                cell.SetResource(t, amt);
                _storageCells.Add(cell);
            }
            SetCellDisplayOnly(go);
        }
    }

    private void BuildInputTierCells(ExtractorData data)
    {
        if (data.inputTiers == null || inputResourcesContent == null) {
            return;
        }

        for (int i = 0; i < data.inputTiers.Count; i++) {
            ExtractorInputTier tier = data.inputTiers[i];
            if (tier == null) continue;

            GameObject go = Instantiate(inventoryCellIngamePrefab, inputResourcesContent);
            InventoryCell cell = go.GetComponent<InventoryCell>();
            if (cell != null) {
                cell.SetResource(tier.inputResource, Mathf.Max(0, tier.amountConsumedPerCycle));
            }

            Button btn = go.GetComponent<Button>();
            Image img = btn != null ? btn.targetGraphic as Image : null;
            int tierIndex = i;
            if (btn != null) {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnTierButtonClicked(tierIndex));
                TierToggleBinding binding = new TierToggleBinding {
                    TierIndex = tierIndex,
                    TargetGraphic = img,
                    NormalColor = img != null ? img.color : Color.white
                };
                _tierToggles.Add(binding);
            }
        }
    }

    private void OnTierButtonClicked(int tierIndex)
    {
        if (_currentExtractor == null) {
            return;
        }

        _currentExtractor.ToggleTierSelection(tierIndex);
        RefreshTierSelectionVisuals();
        RefreshTierAmounts();
        RefreshOutputCell();
    }

    private void RefreshTierSelectionVisuals()
    {
        if (_currentExtractor == null) {
            return;
        }

        foreach (TierToggleBinding b in _tierToggles) {
            if (b?.TargetGraphic == null) continue;
            bool on = _currentExtractor.IsTierSelected(b.TierIndex);
            b.TargetGraphic.color = on ? selectedTierColor : b.NormalColor;
        }
    }

    private void RefreshTierAmounts()
    {
        if (_currentExtractor == null || inputResourcesContent == null) {
            return;
        }

        ExtractorData data = _currentExtractor.ExtractorDataAsset;
        if (data?.inputTiers == null) {
            return;
        }

        int child = 0;
        for (int i = 0; i < data.inputTiers.Count; i++) {
            ExtractorInputTier tier = data.inputTiers[i];
            if (tier == null) continue;
            if (child >= inputResourcesContent.childCount) break;
            InventoryCell cell = inputResourcesContent.GetChild(child).GetComponent<InventoryCell>();
            if (cell != null) {
                cell.SetResource(tier.inputResource, Mathf.Max(0, tier.amountConsumedPerCycle));
            }
            child++;
        }
    }

    private void BuildOutputCell(ExtractorData data)
    {
        if (outputDataContent == null || inventoryCellIngamePrefab == null) {
            return;
        }

        GameObject go = Instantiate(inventoryCellIngamePrefab, outputDataContent);
        _outputDataCell = go.GetComponent<InventoryCell>();
        SetCellDisplayOnly(go);
        RefreshOutputCell();
    }

    private void RefreshOutputCell()
    {
        if (_outputDataCell == null || _currentExtractor == null) {
            return;
        }

        ExtractorData data = _currentExtractor.ExtractorDataAsset;
        if (data == null) {
            return;
        }

        int perCycleOutput = _currentExtractor.GetExpectedOutputPerCycle();
        _outputDataCell.SetResource(data.outputResourceType, perCycleOutput);
    }

    private static void SetCellDisplayOnly(GameObject cellGo)
    {
        if (cellGo == null) {
            return;
        }

        Button button = cellGo.GetComponent<Button>();
        if (button != null) {
            button.interactable = false;
        }

        InventoryCell cell = cellGo.GetComponent<InventoryCell>();
        if (cell != null) {
            cell.enabled = false;
        }
    }

    private void RefreshOverallUi()
    {
        if (_currentExtractor == null) {
            return;
        }

        float cur = _currentExtractor.CurrentExtractedPercent;
        float max = Mathf.Max(0.0001f, _currentExtractor.MaxExtractablePercent);

        if (overallDataSlider != null) {
            overallDataSlider.minValue = 0f;
            overallDataSlider.maxValue = max;
            overallDataSlider.value = Mathf.Clamp(cur, 0f, max);
        }

        if (dataPercentageText != null) {
            dataPercentageText.text = string.Format("{0:F1}% / {1:F1}%", cur, max);
        }
    }

    private void RefreshCycleUi()
    {
        if (_currentExtractor == null) {
            return;
        }

        if (cycleProgressSlider != null) {
            cycleProgressSlider.minValue = 0f;
            cycleProgressSlider.maxValue = 1f;
            cycleProgressSlider.value = Mathf.Clamp01(_currentExtractor.CycleProgress01);
        }
    }
}


