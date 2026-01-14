using TMPro;
using UnityEngine;

public class AetherStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text statusText;
    
    private AetherConsumptionManager _aetherConsumptionManager;
    
    private void Start()
    {
        _aetherConsumptionManager = FindFirstObjectByType<AetherConsumptionManager>();
    }
    
    private void Update()
    {
        if (_aetherConsumptionManager == null || statusText == null) return;
        
        float netAether = _aetherConsumptionManager.NetAetherPerSecond;
        int maxCapacity = _aetherConsumptionManager.MaxAetherCapacity;
        
        statusText.text = $" 초당 에테르 변화량 : {netAether:F1} / {maxCapacity}";
    }
}
