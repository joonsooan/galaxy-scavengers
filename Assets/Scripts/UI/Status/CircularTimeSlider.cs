using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CircularTimeSlider : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider timeSlider;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private Image periodImage;

    [Header("Sprites")]
    [SerializeField] private Sprite daySprite;
    [SerializeField] private Sprite nightSprite;

    [Header("Settings")]
    [SerializeField] private bool showTimeText = true;
    [SerializeField] private string timeFormat = "F0";
    private bool _currentIsDay = true;

    private DayNightCycleManager _dayNightCycleManager;

    private void Start()
    {
        _dayNightCycleManager = DayNightCycleManager.Instance;

        if (timeSlider == null) {
            timeSlider = GetComponent<Slider>();
        }

        if (timeSlider != null) {
            timeSlider.minValue = 0f;
            timeSlider.wholeNumbers = false;
        }

        if (timeText != null) {
            timeText.gameObject.SetActive(showTimeText);
        }

        UpdatePeriodDisplay();
    }

    private void Update()
    {
        if (_dayNightCycleManager == null) {
            _dayNightCycleManager = DayNightCycleManager.Instance;
            if (_dayNightCycleManager == null) return;
        }

        bool isDay = _dayNightCycleManager.IsDay();

        if (isDay != _currentIsDay) {
            _currentIsDay = isDay;
            UpdatePeriodDisplay();
        }

        if (timeSlider != null) {
            float periodTime = _dayNightCycleManager.GetCurrentPeriodTime();
            timeSlider.value = periodTime;
        }

        if (timeText != null && showTimeText) {
            float periodTime = _dayNightCycleManager.GetCurrentPeriodTime();
            timeText.text = periodTime.ToString(timeFormat);
        }
    }

    private void UpdatePeriodDisplay()
    {
        if (_dayNightCycleManager == null) return;

        bool isDay = _dayNightCycleManager.IsDay();

        if (periodImage != null) {
            if (isDay && daySprite != null) {
                periodImage.sprite = daySprite;
            }
            else if (!isDay && nightSprite != null) {
                periodImage.sprite = nightSprite;
            }
        }

        if (timeSlider != null) {
            if (isDay) {
                timeSlider.maxValue = _dayNightCycleManager.GetDayMaxTime();
            }
            else {
                timeSlider.maxValue = _dayNightCycleManager.GetNightMaxTime();
            }
        }
    }
}
