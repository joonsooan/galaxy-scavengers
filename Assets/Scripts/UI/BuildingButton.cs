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

        InitializeBtn();
    }

    private void InitializeBtn()
    {
        icon.sprite = buildingData.icon;
        btnName.text = buildingData.displayName;
    }

    private void OnButtonClicked()
    {
        GameManager.Instance.StartDrag(buildingData);

        if (BuildingInfoPanel.Instance != null)
        {
            BuildingInfoPanel.Instance.FixInfo(buildingData);
        }
        
        if (closePanelOnClick)
        {
            ClosePanel();
        }
    }
    
    private void ClosePanel()
    {
        if (controlPanel != null)
        {
            controlPanel.HideAllPanels();
        }
        else
        {
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

