using TMPro;
using UnityEngine;

public class AetherStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text aetherStatusText;
    [SerializeField] private TMP_Text storageText;
    [SerializeField] private TMP_Text aetherText;
    
    private AetherConsumptionManager _aetherConsumptionManager;
    private StorageTrackerManager _storageTrackerManager;
    
    private void Start()
    {
        _aetherConsumptionManager = FindFirstObjectByType<AetherConsumptionManager>();
        _storageTrackerManager = FindFirstObjectByType<StorageTrackerManager>();
        
        if (_storageTrackerManager != null)
        {
            _storageTrackerManager.OnStorageChanged += UpdateStorageText;
            _storageTrackerManager.OnAetherChanged += UpdateAetherText;
        }
        
        UpdateStorageText();
        UpdateAetherText();
    }
    
    private void OnDestroy()
    {
        if (_storageTrackerManager != null)
        {
            _storageTrackerManager.OnStorageChanged -= UpdateStorageText;
            _storageTrackerManager.OnAetherChanged -= UpdateAetherText;
        }
    }
    
    private void Update()
    {
        if (_aetherConsumptionManager == null || aetherStatusText == null) return;
        
        float netAether = _aetherConsumptionManager.NetAetherPerSecond;
        int maxCapacity = _aetherConsumptionManager.MaxAetherCapacity;
        
        aetherStatusText.text = $" 초당 에테르 변화량 : {netAether:F1} / {maxCapacity}";
    }
    
    private void UpdateStorageText()
    {
        if (storageText == null || _storageTrackerManager == null) return;
        
        int current = _storageTrackerManager.CurrentTotalStoredResourceAmount;
        int max = _storageTrackerManager.MaxStorableResourceAmount;
        
        storageText.text = $"{current} / {max}";
    }
    
    private void UpdateAetherText()
    {
        if (aetherText == null || _storageTrackerManager == null) return;
        
        int current = _storageTrackerManager.CurrentAetherAmount;
        int max = _storageTrackerManager.MaxStorableAetherAmount;
        
        aetherText.text = $"{current} / {max}";
    }
}
