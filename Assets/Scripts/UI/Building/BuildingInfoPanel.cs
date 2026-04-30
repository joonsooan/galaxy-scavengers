using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
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

    private BuildingData _selectedData;
    private Damageable _currentDamageable;
    private bool _showMaxHealthOnly;
    private Dictionary<BuildingPieceType, BuildingPieceData> _pieceDataByType;

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

    private void OnDisable()
    {
        SetCurrentDamageable(null, false);
    }
    
    public void SelectBuilding(BuildingData data)
    {
        _selectedData = data;
        UpdateUI(data, false, null);
        Damageable damageable = data != null && data.buildingPrefab != null ? data.buildingPrefab.GetComponent<Damageable>() : null;
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
        UpdateUI(data, showPostConstructInfo, runtimeConsumer);
        SetCurrentDamageable(damageable, showMaxHealthOnly);
        if (damageable != null)
            UpdateHealthDisplay(damageable, showMaxHealthOnly);
    }

    public void CancelPreview()
    {
        if (_selectedData != null)
        {
            UpdateUI(_selectedData, false, null);
            Damageable damageable = _selectedData.buildingPrefab != null ? _selectedData.buildingPrefab.GetComponent<Damageable>() : null;
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
    
    private void UpdateUI(BuildingData data, bool showPostConstructInfo, IElectricityConsumer runtimeConsumer)
    {
        if (data == null) return;

        if (buildingName != null) buildingName.text = data.displayName;
        if (buildingDesc != null) buildingDesc.text = data.description;
        gameObject.SetActive(true);

        if (showPostConstructInfo)
        {
            SetResourcePanelActive(false);
            SetPostConstructPanelActive(true);
            UpdatePostConstructDisplay(data, runtimeConsumer);
        }
        else
        {
            SetPostConstructPanelActive(false);
            SetResourcePanelActive(true);
            UpdateResourceDisplay(data);
            ClearPostConstructTexts();
        }
    }

    private void UpdatePostConstructDisplay(BuildingData data, IElectricityConsumer runtimeConsumer)
    {
        if (noisePanel != null) noisePanel.SetActive(true);
        if (aetherSpendPanel != null) aetherSpendPanel.SetActive(true);

        float noiseCoefficient = data != null ? data.noiseCoefficient : 0f;
        if (noiseText != null) noiseText.text = $": {noiseCoefficient:F1}";

        float electricityConsumption = GetElectricityConsumptionPerSecond(data, runtimeConsumer);
        if (electricityConsumptionText != null) electricityConsumptionText.text = $": {electricityConsumption:F1} / s";
    }

    private void UpdateHealthDisplay(Damageable damageable, bool showMaxHealth = false)
    {
        if (healthText == null) return;
        if (damageable == null)
        {
            healthText.text = string.Empty;
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
    }

    private void ClearUI()
    {
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

        RectTransform panelRect = resourcePanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
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

}
