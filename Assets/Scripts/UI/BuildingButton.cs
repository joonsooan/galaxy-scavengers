using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class BuildingButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
        BuildingInfoPanel.Instance.SelectBuilding(buildingData);
        
        if (closePanelOnClick)
        {
            ClosePanel();
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (buildingData != null && BuildingInfoPanel.Instance != null)
        {
            BuildingInfoPanel.Instance.PreviewInfo(buildingData);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (BuildingInfoPanel.Instance != null)
        {
            BuildingInfoPanel.Instance.CancelPreview();
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
    
    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }
    }
}

