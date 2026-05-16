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
        if (noiseSlider != null)
        {
            noiseSlider.minValue = 0f;
            noiseSlider.maxValue = 100f;
            noiseSlider.value = 0f;
        }

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
            float clampedNoise = Mathf.Clamp(noisePercentage, 0f, 100f);
            noiseValueText.text = $"{clampedNoise:F1} / 100.0";
        }

        if (NoiseManager.Instance == null) return;

        NoiseManager.NoiseZone zone = NoiseManager.Instance.GetCurrentNoiseZone();

        float t = Mathf.Clamp01(noisePercentage / 100f);
        Color targetColor = Color.Lerp(safeColor, dangerColor, t);

        if (sliderFillImage != null)
        {
            sliderFillImage.color = targetColor;
        }

        if (noiseZoneText != null)
        {
            noiseZoneText.color = targetColor;
        }
    }
}
