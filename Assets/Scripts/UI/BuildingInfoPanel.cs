using TMPro;
using UnityEngine;

public class BuildingInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text buildingName;
    [SerializeField] private GameObject resourcePanel;
    [SerializeField] private TMP_Text buildingDesc;

    private BuildingData _currentData;

    public static BuildingInfoPanel Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ClearPanel();
    }

    public void FixInfo(BuildingData data)
    {
        ShowInfo(data);
    }

    private void ShowInfo(BuildingData data)
    {
        if (data == null)
        {
            ClearPanel();
            return;
        }

        _currentData = data;

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

    private void ClearPanel()
    {
        _currentData = null;

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
