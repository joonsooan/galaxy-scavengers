using System.Collections;
using DG.Tweening;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverLoadingScreen : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private RectTransform gameOverImage;
    [SerializeField] private ParticleSystem gameOverParticles;
    [SerializeField] private Button continueButton;

    [Header("Visual Settings")]
    [SerializeField] private string gameOverTextString = "게임 오버";
    [SerializeField] private Color backgroundColor = new (0f, 0f, 0f, 0.95f);

    [Header("Timing")]
    [SerializeField] private float fadeInStartDelay = 0f;
    [SerializeField] private float fadeInDuration = 1.0f;
    [SerializeField] private float fadeOutDuration = 1.0f;
    [SerializeField] private float particleStartDelay = 0.5f;

    [Header("Audio")]
    [SerializeField] private EventReference gameOverEnterSound;
    [SerializeField] private EventReference gameOverHideSound;
    [SerializeField] private float loadingBgmFadeInTime = 1.0f;
    [SerializeField] private float loadingBgmFadeOutTime = 1.0f;
    
    private Coroutine _particleStartCoroutine;
    private WaitForSecondsRealtime _fadeInStartDelayWait;
    private WaitForSecondsRealtime _particleStartDelayWait;
    private CanvasGroup _continueButtonCanvasGroup;
    private bool _isExiting;
    public bool IsEntryAnimationComplete { get; private set; }
    
    private void Awake()
    {
        gameOverTextString = GameLocalization.GetOrDefault("UI_Common", "loading.gameOver", gameOverTextString);
        InitializeUI();
        _fadeInStartDelayWait = CoroutineCache.GetWaitForSecondsRealtime(fadeInStartDelay);
        _particleStartDelayWait = CoroutineCache.GetWaitForSecondsRealtime(particleStartDelay);
        
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }
    }

    private void OnEnable()
    {
        _isExiting = false;
        IsEntryAnimationComplete = false;
        StartCoroutine(StartAnimation());
    }

    private void OnDisable()
    {
        StopEffects();
    }

    private void InitializeUI()
    {
        if (backgroundImage != null)
        {
            Color bgColor = backgroundColor;
            bgColor.a = 0f;
            backgroundImage.color = bgColor;
        }
        if (gameOverText != null)
        {
            gameOverText.text = gameOverTextString;
            Color textColor = gameOverText.color;
            textColor.a = 0f;
            gameOverText.color = textColor;
        }
        if (gameOverImage != null)
        {
            CanvasGroup imageGroup = gameOverImage.GetComponent<CanvasGroup>();
            if (imageGroup == null)
            {
                imageGroup = gameOverImage.gameObject.AddComponent<CanvasGroup>();
            }
            imageGroup.alpha = 0f;
        }
        if (gameOverParticles != null)
        {
            var main = gameOverParticles.main;
            main.useUnscaledTime = true;
            gameOverParticles.Stop();
        }
        if (continueButton != null)
        {
            _continueButtonCanvasGroup = continueButton.GetComponent<CanvasGroup>();
            if (_continueButtonCanvasGroup == null)
            {
                _continueButtonCanvasGroup = continueButton.gameObject.AddComponent<CanvasGroup>();
            }
            _continueButtonCanvasGroup.alpha = 0f;
            _continueButtonCanvasGroup.interactable = false;
            continueButton.interactable = false;
        }
    }

    private IEnumerator StartAnimation()
    {
        Time.timeScale = 0f;

        if (fadeInStartDelay > 0f)
        {
            yield return _fadeInStartDelayWait;
        }

        if (!gameOverEnterSound.IsNull)
        {
            RuntimeManager.PlayOneShot(gameOverEnterSound);
        }
        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.PlayFailureLoadingBgm(loadingBgmFadeInTime);
        }

        if (backgroundImage != null)
        {
            backgroundImage.DOFade(backgroundColor.a, fadeInDuration).SetUpdate(true);
        }

        if (gameOverText != null)
        {
            gameOverText.DOFade(1f, fadeInDuration).SetUpdate(true);
        }

        if (gameOverImage != null)
        {
            CanvasGroup imageGroup = gameOverImage.GetComponent<CanvasGroup>();
            if (imageGroup != null)
            {
                imageGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
            }
        }
        if (_continueButtonCanvasGroup != null)
        {
            _continueButtonCanvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
        }

        if (gameOverParticles != null)
        {
            if (_particleStartCoroutine != null) StopCoroutine(_particleStartCoroutine);
            _particleStartCoroutine = StartCoroutine(StartParticlesDelayed());
        }

        yield return new WaitForSecondsRealtime(fadeInDuration);

        if (continueButton != null)
        {
            continueButton.interactable = true;
        }
        if (_continueButtonCanvasGroup != null)
        {
            _continueButtonCanvasGroup.interactable = true;
        }
        IsEntryAnimationComplete = true;
    }

    private IEnumerator StartParticlesDelayed()
    {
        yield return _particleStartDelayWait;
        if (gameOverParticles != null) gameOverParticles.Play();
    }

    private void StopEffects()
    {
        if (_particleStartCoroutine != null)
        {
            StopCoroutine(_particleStartCoroutine);
            _particleStartCoroutine = null;
        }

        if (gameOverParticles != null)
        {
            gameOverParticles.Stop();
        }
    }

    private void OnContinueClicked()
    {
        if (_isExiting) return;
        _isExiting = true;
        
        if (!gameOverHideSound.IsNull)
        {
            RuntimeManager.PlayOneShot(gameOverHideSound);
        }
        
        if (continueButton != null)
        {
            continueButton.interactable = false;
        }
        if (_continueButtonCanvasGroup != null)
        {
            _continueButtonCanvasGroup.interactable = false;
        }
        
        StartCoroutine(ExitSequence());
    }

    private IEnumerator ExitSequence()
    {
        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.StopFailureLoadingBgm(loadingBgmFadeOutTime);
        }

        if (gameOverText != null)
        {
            gameOverText.DOFade(0f, fadeOutDuration).SetUpdate(true);
        }

        if (gameOverImage != null)
        {
            CanvasGroup imageGroup = gameOverImage.GetComponent<CanvasGroup>();
            if (imageGroup != null)
            {
                imageGroup.DOFade(0f, fadeOutDuration).SetUpdate(true);
            }
        }
        if (_continueButtonCanvasGroup != null)
        {
            _continueButtonCanvasGroup.DOFade(0f, fadeOutDuration).SetUpdate(true);
        }

        if (backgroundImage != null)
        {
            yield return backgroundImage.DOFade(1f, fadeOutDuration).SetUpdate(true).WaitForCompletion();
        }

        Time.timeScale = 1f;
        
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.CompleteBaseSceneLoad();
        }
    }
}
