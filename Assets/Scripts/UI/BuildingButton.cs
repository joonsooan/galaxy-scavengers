using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;

public class BuildingButton : MonoBehaviour
{
    [Header("Building Data")]
    [SerializeField] private BuildingData buildingData;

    [Header("Optional Settings")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text btnName;
    [SerializeField] private bool closePanelOnClick;
    [SerializeField] private MainControlPanel controlPanel;
    
    private Button _button;
    
    private void Awake()
    {
        _button = GetComponent<Button>();
        
        if (_button != null)
        {
            _button.onClick.AddListener(OnButtonClicked);
        }
        else
        {
            Debug.LogWarning($"[BuildingButton] No Button component found on {gameObject.name}");
        }

        InitializeBtn();
    }

    private void InitializeBtn()
    {
        icon.sprite = buildingData.icon;
        btnName.text = buildingData.displayName;
    }

    private void OnButtonClicked()
    {
        if (buildingData == null)
        {
            Debug.LogWarning($"[BuildingButton] No ComboCardData assigned to button on {gameObject.name}");
            return;
        }
        
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[BuildingButton] GameManager.Instance is null");
            return;
        }
        
        // Start the building construction process for combo buildings
        GameManager.Instance.StartDrag(buildingData);
        
        // Optionally close the panel
        if (closePanelOnClick)
        {
            ClosePanel();
        }
    }
    
    private void ClosePanel()
    {
        // Try to find MainControlPanel if not assigned
        if (controlPanel == null)
        {
            controlPanel = FindObjectsByType<MainControlPanel>(FindObjectsSortMode.None).FirstOrDefault();
        }
        
        if (controlPanel != null)
        {
            controlPanel.HideAllPanels();
        }
        else
        {
            // Fallback: hide the parent panel GameObject
            Transform panelTransform = transform.parent;
            while (panelTransform != null)
            {
                if (panelTransform.name.Contains("Panel") || panelTransform.name.Contains("panel"))
                {
                    panelTransform.gameObject.SetActive(false);
                    break;
                }
                panelTransform = panelTransform.parent;
            }
        }
    }
    
    public void SetComboCardData(BuildingData data)
    {
        buildingData = data;
    }
    
    public BuildingData GetComboCardData()
    {
        return buildingData;
    }
    
    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }
    }
}

