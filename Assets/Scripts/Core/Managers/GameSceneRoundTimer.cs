using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneRoundTimer : MonoBehaviour
{
    [SerializeField] private float initialDurationSeconds = 600f;
    [Tooltip("If true, timer uses unscaled delta time (keeps running while game is paused).")]
    [SerializeField] private bool useUnscaledTime;
    [SerializeField] private Slider timerSlider;
    [SerializeField] private TMP_Text timerText;
    [Tooltip("Assign the Slider Fill Rect's Image, or leave empty to use the slider's fillRect if present.")]
    [SerializeField] private Image sliderFillImage;

    [Header("Zone colors (same defaults as NoiseSliderUI fill)")]
    [SerializeField] private Color safeColor = Color.green;
    [SerializeField] private Color dangerColor = Color.red;

    private float _remainingSeconds;
    private float _durationTotal;
    private bool _gameOverTriggered;
    private int _lastDisplayedTotalSeconds = -1;

    private void Start()
    {
        _durationTotal = Mathf.Max(0.01f, initialDurationSeconds);
        _remainingSeconds = _durationTotal;
        if (timerSlider != null)
        {
            timerSlider.minValue = 0f;
            timerSlider.maxValue = _remainingSeconds;
            timerSlider.wholeNumbers = false;
        }

        if (sliderFillImage == null && timerSlider != null && timerSlider.fillRect != null)
        {
            sliderFillImage = timerSlider.fillRect.GetComponent<Image>();
        }

        RefreshDisplay();
        enabled = false;
    }

    private void RefreshDisplay()
    {
        if (timerSlider != null)
        {
            timerSlider.value = _remainingSeconds;
        }

        if (timerText != null)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(_remainingSeconds));
            if (totalSeconds != _lastDisplayedTotalSeconds)
            {
                _lastDisplayedTotalSeconds = totalSeconds;
                timerText.text = FormatMmSs(_remainingSeconds);
            }
        }

        ApplyUrgencyColors();
    }

    private void ApplyUrgencyColors()
    {
        float t = 1f - Mathf.Clamp01(_remainingSeconds / _durationTotal);
        Color c = Color.Lerp(safeColor, dangerColor, t);

        if (sliderFillImage != null)
        {
            sliderFillImage.color = c;
        }
    }

    private static string FormatMmSs(float secondsRemaining)
    {
        int t = Mathf.Max(0, Mathf.FloorToInt(secondsRemaining));
        int m = t / 60;
        int s = t % 60;
        return $"{m:00}:{s:00}";
    }
}
