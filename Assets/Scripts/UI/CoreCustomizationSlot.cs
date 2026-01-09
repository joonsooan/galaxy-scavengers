using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class CoreCustomizationSlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image moduleIconImage;
    [SerializeField] private TMP_Text moduleNameText;
    [SerializeField] private GameObject emptySlotIndicator;
    [SerializeField] private Button slotButton;

    private int _slotIndex;
    private Module _currentModule;
    private CoreCustomizationManager _customizationManager;
    private CoreCustomUIManager _uiManager;
    private EventTrigger _eventTrigger;

    public int SlotIndex => _slotIndex;
    public Module CurrentModule => _currentModule;

    private void Awake()
    {
        if (slotButton != null) {
            slotButton.onClick.AddListener(OnSlotButtonClicked);
        }

        SetupRightClickHandler();
    }

    private void SetupRightClickHandler()
    {
        _eventTrigger = GetComponent<EventTrigger>();
        if (_eventTrigger == null) {
            _eventTrigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback.AddListener((data) => {
            PointerEventData pointerData = (PointerEventData)data;
            if (pointerData.button == PointerEventData.InputButton.Right) {
                OnRightClick();
            }
        });
        _eventTrigger.triggers.Add(entry);
    }

    public void Initialize(int slotIndex, CoreCustomizationManager manager, CoreCustomUIManager uiManager)
    {
        _slotIndex = slotIndex;
        _customizationManager = manager;
        _uiManager = uiManager;
        RefreshSlot();
    }

    public void RefreshSlot()
    {
        _currentModule = _customizationManager != null ? _customizationManager.GetModuleInSlot(_slotIndex) : null;

        bool hasModule = _currentModule != null;

        if (moduleIconImage != null) {
            if (hasModule && _currentModule.moduleIcon != null) {
                moduleIconImage.sprite = _currentModule.moduleIcon;
                moduleIconImage.enabled = true;
            } else {
                moduleIconImage.sprite = null;
                moduleIconImage.enabled = false;
            }
        }

        if (moduleNameText != null) {
            moduleNameText.text = hasModule ? _currentModule.moduleName : "";
        }

        if (emptySlotIndicator != null) {
            emptySlotIndicator.SetActive(!hasModule);
        }
    }

    private void OnSlotButtonClicked()
    {
        if (_uiManager != null) {
            _uiManager.OnSlotClicked(_slotIndex);
        }
    }

    private void OnRightClick()
    {
        if (_currentModule != null && _customizationManager != null) {
            _customizationManager.RemoveModuleFromSlot(_slotIndex);
        }
    }
}
