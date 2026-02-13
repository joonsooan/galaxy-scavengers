using TMPro;
using UnityEngine;

public class IngameStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text aetherStatusText;
    [SerializeField] private TMP_Text storageText;
    [SerializeField] private TMP_Text aetherText;

    private AetherConsumptionManager _aetherConsumptionManager;
    private Color _originalColor;
    private StorageTrackerManager _storageTrackerManager;

    private void Start()
    {
        _aetherConsumptionManager = FindFirstObjectByType<AetherConsumptionManager>();
        _storageTrackerManager = FindFirstObjectByType<StorageTrackerManager>();

        if (_storageTrackerManager != null) {
            _storageTrackerManager.OnStorageChanged += UpdateStorageText;
            _storageTrackerManager.OnAetherChanged += UpdateAetherText;
        }

        _originalColor = aetherStatusText.color;

        UpdateStorageText();
        UpdateAetherText();
    }

    private void Update()
    {
        if (_aetherConsumptionManager == null || aetherStatusText == null) return;

        float netAether = _aetherConsumptionManager.NetAetherPerSecond;
        if (netAether > 0) {
            aetherStatusText.color = Color.green;
            aetherStatusText.text = $"변화량 : + {netAether:F1}/s";
        }
        else if (netAether < 0) {
            netAether = Mathf.Abs(netAether);
            aetherStatusText.color = Color.red;
            aetherStatusText.text = $"변화량 : - {netAether:F1}/s";
        }
        else {
            aetherStatusText.color = _originalColor;
            aetherStatusText.text = $"변화량 : {netAether:F1}/s";
        }
    }

    private void OnDestroy()
    {
        if (_storageTrackerManager != null) {
            _storageTrackerManager.OnStorageChanged -= UpdateStorageText;
            _storageTrackerManager.OnAetherChanged -= UpdateAetherText;
        }
    }

    private void UpdateStorageText()
    {
        if (storageText == null || _storageTrackerManager == null) return;

        int current = _storageTrackerManager.CurrentTotalStoredResourceAmount;
        int max = _storageTrackerManager.MaxStorableResourceAmount;

        storageText.text = $"자원 저장량 : {current} / {max}";
    }

    private void UpdateAetherText()
    {
        if (aetherText == null || _storageTrackerManager == null) return;

        int current = _storageTrackerManager.CurrentAetherAmount;
        int max = _storageTrackerManager.MaxStorableAetherAmount;

        if (current >= max) {
            aetherText.color = Color.red;
        }
        else {
            aetherText.color = _originalColor;
        }
        aetherText.text = $"저장량 : {current} / {max}";
    }
}
