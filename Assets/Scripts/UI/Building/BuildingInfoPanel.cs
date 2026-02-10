using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BuildingInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text buildingName;
    [SerializeField] private GameObject resourcePanel;
    [SerializeField] private TMP_Text buildingDesc;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private GameObject resourceInfoCellPrefab;
    [SerializeField] private Transform processorResourceIconParent;
    [SerializeField] private GameObject processorResourceIconPrefab;
    [SerializeField] private TMP_Text producibleResourceLabel;
    [SerializeField] private GameObject processorResourceGroup;

    [SerializeField] private List<BuildingPieceData> allPieceDatabase;

    private BuildingData _selectedData;
    private Damageable _currentDamageable;
    private bool _showMaxHealthOnly;

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
        UpdateUI(data);
        Damageable damageable = data != null && data.buildingPrefab != null ? data.buildingPrefab.GetComponent<Damageable>() : null;
        SetCurrentDamageable(damageable, true);
        if (damageable != null)
            UpdateHealthDisplay(damageable, true);
    }

    public void PreviewInfo(BuildingData data, Damageable damageable = null, bool showMaxHealthOnly = false)
    {
        UpdateUI(data);
        SetCurrentDamageable(damageable, showMaxHealthOnly);
        if (damageable != null)
            UpdateHealthDisplay(damageable, showMaxHealthOnly);
    }

    public void CancelPreview()
    {
        if (_selectedData != null)
        {
            UpdateUI(_selectedData);
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
    
    private void UpdateUI(BuildingData data)
    {
        if (data == null) return;

        if (buildingName != null) buildingName.text = data.displayName;
        if (buildingDesc != null) buildingDesc.text = data.description;
        if (resourcePanel != null)
        {
            gameObject.SetActive(true);
            resourcePanel.SetActive(true);
            UpdateResourceDisplay(data);
        }

        if (IsProcessorBuilding(data))
        {
            ProcessorData processorData = GetProcessorData(data);
            UpdateProcessorResourceDisplay(processorData);
        }
        else
        {
            ClearProcessorResourceDisplay();
        }
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
            healthText.text = $"현재 체력 : {damageable.CurrentHealth} / {damageable.MaxHealth}";
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
            resourcePanel.SetActive(false);
        }
        ClearProcessorResourceDisplay();
    }
    
    private void UpdateResourceDisplay(BuildingData data)
    {
        foreach (Transform child in resourcePanel.transform)
        {
            Destroy(child.gameObject);
        }

        if (data.recipe == null || data.recipe.Count == 0)
        {
            if (data.buildingType != BuildingType.MainStructure)
            {
                return;
            }
        }

        Dictionary<ResourceType, int> totalCosts = new Dictionary<ResourceType, int>();

        foreach (var piece in data.recipe)
        {
            BuildingPieceData pieceData = GetPieceDataByType(piece.buildingPieceType);

            if (pieceData != null && pieceData.costs != null)
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

        foreach (var kvp in totalCosts)
        {
            ResourceType type = kvp.Key;
            int amount = kvp.Value;

            GameObject cellObj = Instantiate(resourceInfoCellPrefab, resourcePanel.transform);
            ResourceInfoCell cell = cellObj.GetComponent<ResourceInfoCell>();
            
            if (cell != null)
            {
                cell.SetInfo(type, amount);
            }
        }
    }
    
    private BuildingPieceData GetPieceDataByType(BuildingPieceType type)
    {
        foreach (var data in allPieceDatabase)
        {
            if (data.buildingPieceType == type)
            {
                return data;
            }
        }
        return null;
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
            resourcePanel.SetActive(false);
        }
        ClearProcessorResourceDisplay();
    }

    private bool IsProcessorBuilding(BuildingData data)
    {
        if (data == null)
        {
            return false;
        }
        return data.buildingType == BuildingType.Smelter ||
               data.buildingType == BuildingType.Assembler ||
               data.buildingType == BuildingType.Reactor;
    }

    private ProcessorData GetProcessorData(BuildingData data)
    {
        if (data == null)
        {
            return null;
        }
        if (data.buildingPrefab == null)
        {
            return null;
        }
        Processor processor = data.buildingPrefab.GetComponent<Processor>();
        if (processor == null)
        {
            return null;
        }
        return processor.ProcessorData;
    }

    private void UpdateProcessorResourceDisplay(ProcessorData processorData)
    {
        if (processorResourceGroup != null)
        {
            processorResourceGroup.SetActive(false);
        }

        if (processorResourceIconParent == null)
        {
            return;
        }

        foreach (Transform child in processorResourceIconParent)
        {
            Destroy(child.gameObject);
        }

        if (producibleResourceLabel != null)
        {
            producibleResourceLabel.gameObject.SetActive(false);
        }

        if (processorData == null)
        {
            processorResourceIconParent.gameObject.SetActive(false);
            return;
        }

        List<ProcessorRecipe> recipes = processorData.Recipes;
        if (recipes == null || recipes.Count == 0)
        {
            processorResourceIconParent.gameObject.SetActive(false);
            return;
        }

        HashSet<ResourceType> resourceTypes = new HashSet<ResourceType>();
        for (int i = 0; i < recipes.Count; i++)
        {
            ProcessorRecipe recipe = recipes[i];
            if (recipe != null)
            {
                resourceTypes.Add(recipe.resourceType);
            }
        }

        if (resourceTypes.Count == 0)
        {
            processorResourceIconParent.gameObject.SetActive(false);
            return;
        }

        foreach (ResourceType type in resourceTypes)
        {
            if (processorResourceIconPrefab == null)
            {
                continue;
            }

            GameObject iconObj = Instantiate(processorResourceIconPrefab, processorResourceIconParent);
            Image image = iconObj.GetComponentInChildren<Image>();

            if (image != null)
            {
                Sprite icon = null;
                if (ResourceManager.Instance != null)
                {
                    icon = ResourceManager.Instance.GetResourceIcon(type);
                }

                if (icon != null)
                {
                    image.sprite = icon;
                }
            }
        }

        processorResourceIconParent.gameObject.SetActive(true);

        if (producibleResourceLabel != null)
        {
            producibleResourceLabel.gameObject.SetActive(true);
        }

        if (processorResourceGroup != null)
        {
            processorResourceGroup.SetActive(true);
        }
    }

    private void ClearProcessorResourceDisplay()
    {
        if (processorResourceIconParent != null)
        {
            foreach (Transform child in processorResourceIconParent)
            {
                Destroy(child.gameObject);
            }
            processorResourceIconParent.gameObject.SetActive(false);
        }

        if (producibleResourceLabel != null)
        {
            producibleResourceLabel.gameObject.SetActive(false);
        }

        if (processorResourceGroup != null)
        {
            processorResourceGroup.SetActive(false);
        }
    }
}
