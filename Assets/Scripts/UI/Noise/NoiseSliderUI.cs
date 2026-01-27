using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NoiseSliderUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider noiseSlider;
    [SerializeField] private TMP_Text noiseValueText;
    [SerializeField] private TMP_Text noiseZoneText;
    [SerializeField] private Image sliderFillImage;

    [Header("Zone Colors")]
    [SerializeField] private Color safeColor = Color.green;
    [SerializeField] private Color cautionColor = Color.yellow;
    [SerializeField] private Color warningColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color dangerColor = Color.red;

    private void OnEnable()
    {
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseChanged += UpdateNoiseDisplay;
            UpdateNoiseDisplay(NoiseManager.Instance.NoisePercentage);
        }
    }

    private void OnDisable()
    {
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseChanged -= UpdateNoiseDisplay;
        }
    }

    private void UpdateNoiseDisplay(float noisePercentage)
    {
        if (noiseSlider != null)
        {
            noiseSlider.value = noisePercentage;
        }

        if (noiseValueText != null)
        {
            noiseValueText.text = $"{noisePercentage:F1}%";
        }

        if (NoiseManager.Instance == null) return;

        NoiseManager.NoiseZone zone = NoiseManager.Instance.GetCurrentNoiseZone();
        
        if (sliderFillImage != null)
        {
            sliderFillImage.color = zone switch
            {
                NoiseManager.NoiseZone.Safe => safeColor,
                NoiseManager.NoiseZone.Caution => cautionColor,
                NoiseManager.NoiseZone.Warning => warningColor,
                NoiseManager.NoiseZone.Danger => dangerColor,
                _ => safeColor
            };
        }

        if (noiseZoneText != null)
        {
            noiseZoneText.text = zone switch
            {
                NoiseManager.NoiseZone.Safe => "안전",
                NoiseManager.NoiseZone.Caution => "주의",
                NoiseManager.NoiseZone.Warning => "경고",
                NoiseManager.NoiseZone.Danger => "위험",
                _ => "안전"
            };
        }
    }
}
