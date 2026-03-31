using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IngameStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text aetherStatusText;
    [SerializeField] private TMP_Text storageText;
    [SerializeField] private TMP_Text aetherText;
    [SerializeField] private GameObject creditPanel;
    [SerializeField] private Image creditIconImage;
    [SerializeField] private TMP_Text creditAmountText;

    private ElectricityConsumptionManager _electricityConsumptionManager;
    private Color _originalColor;
    private StorageTrackerManager _storageTrackerManager;
    private bool _isCreditSubscribed;
    private Coroutine _bindCreditManagerCoroutine;

    private void Start()
    {
        _electricityConsumptionManager = FindFirstObjectByType<ElectricityConsumptionManager>();
        _storageTrackerManager = FindFirstObjectByType<StorageTrackerManager>();

        if (_storageTrackerManager != null) {
            _storageTrackerManager.OnStorageChanged += UpdateStorageText;
            _storageTrackerManager.OnElectricityChanged += UpdateAetherText;
        }

        _originalColor = aetherStatusText.color;
        _bindCreditManagerCoroutine = StartCoroutine(BindCreditManagerWhenReady());

        UpdateStorageText();
        UpdateAetherText();
    }

    private void Update()
    {
        if (_electricityConsumptionManager == null || aetherStatusText == null) return;

        float netElectricity = _electricityConsumptionManager.NetElectricityPerSecond;
        if (netElectricity > 0) {
            aetherStatusText.color = Color.green;
            aetherStatusText.text = $"전기 : + {netElectricity:F1}/s";
        }
        else if (netElectricity < 0) {
            netElectricity = Mathf.Abs(netElectricity);
            aetherStatusText.color = Color.red;
            aetherStatusText.text = $"전기 : - {netElectricity:F1}/s";
        }
        else {
            aetherStatusText.color = _originalColor;
            aetherStatusText.text = $"전기 : {netElectricity:F1}/s";
        }
    }

    private void OnDestroy()
    {
        if (_storageTrackerManager != null) {
            _storageTrackerManager.OnStorageChanged -= UpdateStorageText;
            _storageTrackerManager.OnElectricityChanged -= UpdateAetherText;
        }

        if (_isCreditSubscribed && CreditManager.Instance != null)
        {
            CreditManager.Instance.OnCreditsChanged -= UpdateCreditText;
        }
        _isCreditSubscribed = false;
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

        int current = _storageTrackerManager.CurrentElectricityAmount;
        int max = _storageTrackerManager.MaxStorableElectricityAmount;

        if (current >= max && max > 0) {
            aetherText.color = Color.red;
        }
        else {
            aetherText.color = _originalColor;
        }
        aetherText.text = $"전기 저장 : {current} / {max}";
    }

    private IEnumerator BindCreditManagerWhenReady()
    {
        while (CreditManager.Instance == null)
        {
            yield return null;
        }

        if (!_isCreditSubscribed)
        {
            CreditManager.Instance.OnCreditsChanged += UpdateCreditText;
            _isCreditSubscribed = true;
        }
        UpdateCreditText(CreditManager.Instance.GetCredits());
        _bindCreditManagerCoroutine = null;
    }

    private void UpdateCreditText(int credits)
    {
        if (creditAmountText == null)
        {
            return;
        }

        creditAmountText.text = $"보유량 : {credits.ToString()}";
        LayoutRebuilder.ForceRebuildLayoutImmediate(creditAmountText.rectTransform);
        if (creditPanel != null)
        {
            RectTransform panelRect = creditPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            }
        }
    }
}
