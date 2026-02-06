using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSpeedUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image pausePlayImage;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private Button pausePlayButton;

    [Header("Sprites")]
    [SerializeField] private Sprite pauseSprite;
    [SerializeField] private Sprite playSprite;

    private GameManager _gameManager;
    private bool _lastPausedState;
    private float _lastTimeScale;

    private void Start()
    {
        _gameManager = GameManager.Instance;
        _lastPausedState = _gameManager != null && _gameManager.IsPaused;
        _lastTimeScale = _gameManager != null ? _gameManager.GetTimeScale() : 1f;
        
        if (pausePlayButton != null)
        {
            pausePlayButton.onClick.AddListener(OnPausePlayButtonClicked);
            if (pausePlayButton.transition == Selectable.Transition.SpriteSwap)
            {
                pausePlayButton.transition = Selectable.Transition.None;
            }
        }
        
        UpdateDisplay();
    }

    private void OnPausePlayButtonClicked()
    {
        if (_gameManager != null)
        {
            _gameManager.TogglePause();
        }
    }

    private void OnDestroy()
    {
        if (pausePlayButton != null)
        {
            pausePlayButton.onClick.RemoveAllListeners();
        }
    }

    private void Update()
    {
        if (_gameManager == null) {
            _gameManager = GameManager.Instance;
            if (_gameManager == null) return;
        }

        bool currentPaused = _gameManager.IsPaused;
        float currentTimeScale = _gameManager.GetTimeScale();

        if (currentPaused != _lastPausedState || Mathf.Abs(currentTimeScale - _lastTimeScale) > 0.01f) {
            _lastPausedState = currentPaused;
            _lastTimeScale = currentTimeScale;
            UpdateDisplay();
        }
    }

    public void UpdateDisplay()
    {
        if (_gameManager == null) return;

        if (pausePlayImage != null) {
            if (_gameManager.IsPaused) {
                if (pauseSprite != null) {
                    pausePlayImage.sprite = pauseSprite;
                }
            }
            else {
                if (playSprite != null) {
                    pausePlayImage.sprite = playSprite;
                }
            }
        }

        if (speedText != null) {
            float timeScale = _gameManager.GetTimeScale();
            int speed = Mathf.RoundToInt(timeScale);
            speedText.text = $"x{speed}";
        }
    }
}
