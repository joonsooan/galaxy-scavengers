using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CoreCustomizationSlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image moduleIconImage;
    [SerializeField] private TMP_Text moduleNameText;
    [SerializeField] private GameObject emptySlotIndicator;
    [SerializeField] private Button slotButton;
    private CoreCustomizationManager _customizationManager;
    private EventTrigger _eventTrigger;

    private int SlotIndex { get; set; }
    private Module CurrentModule { get; set; }

    private void Awake()
    {
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
        entry.callback.AddListener(data => {
            PointerEventData pointerData = (PointerEventData)data;
            if (pointerData.button == PointerEventData.InputButton.Right) {
                OnRightClick();
            }
        });
        _eventTrigger.triggers.Add(entry);
    }

    public void Initialize(int slotIndex, CoreCustomizationManager manager, CoreCustomUIManager uiManager)
    {
        SlotIndex = slotIndex;
        _customizationManager = manager;
        
        RefreshSlot();
    }

    public void RefreshSlot()
    {
        CurrentModule = _customizationManager != null ? _customizationManager.GetModuleInSlot(SlotIndex) : null;
        bool hasModule = CurrentModule != null;

        if (moduleIconImage != null) {
            if (hasModule && CurrentModule != null && CurrentModule.moduleIcon != null) {
                moduleIconImage.sprite = CurrentModule.moduleIcon;
                moduleIconImage.enabled = true;
            }
            else {
                moduleIconImage.sprite = null;
                moduleIconImage.enabled = false;
                moduleIconImage.color = Color.white;
            }
        }

        if (moduleNameText != null) {
            moduleNameText.text = hasModule && CurrentModule != null ? CurrentModule.moduleName : "";
        }

        if (emptySlotIndicator != null) {
            emptySlotIndicator.SetActive(!hasModule);
        }

        if (slotButton != null) {
            slotButton.interactable = true;
        }
    }

    private void OnRightClick()
    {
        if (CurrentModule != null && _customizationManager != null) {
            _customizationManager.RemoveModuleFromSlot(SlotIndex);
        }
    }

}
