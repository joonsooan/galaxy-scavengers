using TMPro;
using UnityEngine;

public class AetherStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text aetherStatusText;
    [SerializeField] private TMP_Text storageText;
    [SerializeField] private TMP_Text aetherText;
    
    private ElectricityConsumptionManager _electricityConsumptionManager;
    private StorageTrackerManager _storageTrackerManager;
    
    private void Start()
    {
        _electricityConsumptionManager = ElectricityConsumptionManager.Instance;
        _storageTrackerManager = FindFirstObjectByType<StorageTrackerManager>();
        
        if (_storageTrackerManager != null)
        {
            _storageTrackerManager.OnStorageChanged += UpdateStorageText;
            _storageTrackerManager.OnElectricityChanged += UpdateElectricityStorageText;
        }
        
        UpdateStorageText();
        UpdateElectricityStorageText();
    }
    
    private void OnDestroy()
    {
        if (_storageTrackerManager != null)
        {
            _storageTrackerManager.OnStorageChanged -= UpdateStorageText;
            _storageTrackerManager.OnElectricityChanged -= UpdateElectricityStorageText;
        }
    }
    
    private void Update()
    {
        if (_electricityConsumptionManager == null || aetherStatusText == null) return;
        
        float netElectricity = _electricityConsumptionManager.NetElectricityPerSecond;
        int maxCapacity = _electricityConsumptionManager.MaxElectricityStorageCapacity;
        
        aetherStatusText.text = $" 초당 전기 변화량 : {netElectricity:F1} / {maxCapacity}";
    }
    
    private void UpdateStorageText()
    {
        if (storageText == null || _storageTrackerManager == null) return;
        
        int current = _storageTrackerManager.CurrentTotalStoredResourceAmount;
        int max = _storageTrackerManager.MaxStorableResourceAmount;
        
        storageText.text = $"{current} / {max}";
    }
    
    private void UpdateElectricityStorageText()
    {
        if (aetherText == null || _storageTrackerManager == null) return;
        
        int current = _storageTrackerManager.CurrentElectricityAmount;
        int max = _storageTrackerManager.MaxStorableElectricityAmount;
        
        aetherText.text = $"{current} / {max}";
    }
}
