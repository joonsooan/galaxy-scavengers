using UnityEngine;
using UnityEngine.UI;

public class GameSpeedUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image pausePlayImage;
    [SerializeField] private Button pausePlayButton;

    [Header("Sprites")]
    [SerializeField] private Sprite pauseSprite;
    [SerializeField] private Sprite playSprite;

    private GameManager _gameManager;
    private bool _lastPausedState;

    private void Start()
    {
        _gameManager = GameManager.Instance;
        _lastPausedState = _gameManager != null && _gameManager.IsPaused;

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

        if (currentPaused != _lastPausedState) {
            _lastPausedState = currentPaused;
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
    }
}
