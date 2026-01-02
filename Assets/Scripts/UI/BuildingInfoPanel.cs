using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class BuildingInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text buildingName;
    [SerializeField] private GameObject resourcePanel;
    [SerializeField] private TMP_Text buildingDesc;
    [SerializeField] private GameObject resourceInfoCellPrefab;
    
    [SerializeField] private List<BuildingPieceData> allPieceDatabase;

    private BuildingData _selectedData;

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
    
    public void SelectBuilding(BuildingData data)
    {
        _selectedData = data;
        UpdateUI(data);
    }
    
    public void PreviewInfo(BuildingData data)
    {
        UpdateUI(data);
    }
    
    public void CancelPreview()
    {
        if (_selectedData != null)
        {
            UpdateUI(_selectedData);
        }
        else
        {
            ClearUI();
        }
    }
    
    public void ClearAllInfo()
    {
        _selectedData = null;
        ClearUI();
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
    }

    private void ClearUI()
    {
        if (buildingName != null) buildingName.text = string.Empty;
        if (buildingDesc != null) buildingDesc.text = string.Empty;
        if (resourcePanel != null)
        {
            foreach (Transform child in resourcePanel.transform)
            {
                Destroy(child.gameObject);
            }
            resourcePanel.SetActive(false);
        }
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

        if (buildingName != null)
        {
            buildingName.text = string.Empty;
        }

        if (buildingDesc != null)
        {
            buildingDesc.text = string.Empty;
        }

        if (resourcePanel != null)
        {
            resourcePanel.SetActive(false);
        }
    }
}
