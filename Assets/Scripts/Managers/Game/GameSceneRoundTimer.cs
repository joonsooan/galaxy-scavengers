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

    private float _remainingSeconds;
    private bool _gameOverTriggered;

    private void Start()
    {
        _remainingSeconds = Mathf.Max(0f, initialDurationSeconds);
        if (timerSlider != null)
        {
            timerSlider.minValue = 0f;
            timerSlider.maxValue = _remainingSeconds;
            timerSlider.wholeNumbers = false;
        }
        RefreshDisplay();
    }

    private void Update()
    {
        if (_gameOverTriggered || GameManager.Instance == null || !GameManager.Instance.IsGameSceneInitialized)
        {
            return;
        }

        if (!GameManager.IsGameplayReady)
        {
            return;
        }

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _remainingSeconds = Mathf.Max(0f, _remainingSeconds - dt);
        RefreshDisplay();

        if (_remainingSeconds <= 0f)
        {
            TriggerGameOver();
        }
    }

    private void TriggerGameOver()
    {
        if (_gameOverTriggered)
        {
            return;
        }

        _gameOverTriggered = true;
        GameManager.Instance.GameOver();
    }

    private void RefreshDisplay()
    {
        if (timerSlider != null)
        {
            timerSlider.value = _remainingSeconds;
        }

        if (timerText != null)
        {
            timerText.text = FormatMmSs(_remainingSeconds);
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
