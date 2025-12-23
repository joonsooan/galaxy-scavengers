using TMPro;
using UnityEngine;

public class BuildingInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text buildingName;
    [SerializeField] private GameObject resourcePanel;
    [SerializeField] private TMP_Text buildingDesc;

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
        if (resourcePanel != null) resourcePanel.SetActive(true);
    }

    private void ClearUI()
    {
        if (buildingName != null) buildingName.text = string.Empty;
        if (buildingDesc != null) buildingDesc.text = string.Empty;
        if (resourcePanel != null) resourcePanel.SetActive(false);
    }

    public void FixInfo(BuildingData data)
    {
        ShowInfo(data);
    }

    private void ShowInfo(BuildingData data)
    {
        if (data == null)
        {
            ClearInfo();
            return;
        }

        _selectedData = data;

        if (buildingName != null)
        {
            buildingName.text = data.displayName;
        }

        if (buildingDesc != null)
        {
            buildingDesc.text = data.description;
        }

        if (resourcePanel != null)
        {
            resourcePanel.SetActive(true);
        }
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
