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
    [SerializeField] private GameObject lockIndicator;
    private CoreCustomizationManager _customizationManager;
    private EventTrigger _eventTrigger;

    private CoreCustomUIManager _uiManager;

    public int SlotIndex { get; private set; }

    public Module CurrentModule { get; private set; }

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
        _uiManager = uiManager;
        
        // Subscribe to unlocked slot count changes
        if (_customizationManager != null) {
            _customizationManager.OnUnlockedSlotCountChanged += OnUnlockedSlotCountChanged;
        }
        
        RefreshSlot();
    }

    private void OnDestroy()
    {
        if (_customizationManager != null) {
            _customizationManager.OnUnlockedSlotCountChanged -= OnUnlockedSlotCountChanged;
        }
    }

    private void OnUnlockedSlotCountChanged(int unlockedSlotCount)
    {
        // Update UI when unlocked slot count changes
        UpdateLockUI();
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

        UpdateLockUI();
    }

    private void UpdateLockUI()
    {
        bool isLocked = _customizationManager != null && _customizationManager.IsSlotLocked(SlotIndex);
        
        if (lockIndicator != null) {
            lockIndicator.SetActive(isLocked);
        }

        // Optionally disable slot button when locked
        if (slotButton != null) {
            slotButton.interactable = !isLocked;
        }
    }

    private void OnSlotButtonClicked()
    {
        if (_uiManager != null) {
            _uiManager.OnSlotClicked(SlotIndex);
        }
    }

    private void OnRightClick()
    {
        if (CurrentModule != null && _customizationManager != null) {
            // Check if slot is locked before removing
            if (_customizationManager.IsSlotLocked(SlotIndex)) {
                Debug.LogWarning($"CoreCustomizationSlot: Cannot remove module from slot {SlotIndex} - slot is locked");
                return;
            }
            _customizationManager.RemoveModuleFromSlot(SlotIndex);
        }
    }

}
