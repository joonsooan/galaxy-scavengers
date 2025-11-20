using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;

public class BuildingButton : MonoBehaviour
{
    [Header("Building Data")]
    [SerializeField] private ComboCardData comboCardData;

    [Header("Optional Settings")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text btnName;
    [SerializeField] private bool closePanelOnClick = false;
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
        icon.sprite = comboCardData.icon;
        btnName.text = comboCardData.displayName;
    }

    private void OnButtonClicked()
    {
        if (comboCardData == null)
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
        GameManager.Instance.StartDrag(comboCardData);
        
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
    
    public void SetComboCardData(ComboCardData data)
    {
        comboCardData = data;
    }
    
    public ComboCardData GetComboCardData()
    {
        return comboCardData;
    }
    
    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }
    }
}

