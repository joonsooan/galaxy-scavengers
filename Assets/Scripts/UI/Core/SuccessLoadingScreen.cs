using System.Collections;
using DG.Tweening;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SuccessLoadingScreen : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private RectTransform loadingImage;
    [SerializeField] private Transform visualChild;
    [SerializeField] private ParticleSystem loadingParticles;
    [SerializeField] private Button continueButton;

    [Header("Visual Settings")]
    [SerializeField] private string loadingTextString = "탈출 성공!";
    [SerializeField] private Color backgroundColor = new (0f, 0f, 0f, 0.9f);

    [Header("Timing - Entry & Particles")]
    [SerializeField] private float imageEntryDelay;
    [SerializeField] private float imageEntryDuration = 0.8f;
    [SerializeField] private float particleStartDelay = 1f;
    [SerializeField] private float buttonFadeInDuration = 0.5f;

    [Header("Timing - Exit")]
    [SerializeField] private float imageExitDuration = 1.0f;
    [SerializeField] private float postImageExitWaitDuration = 1f;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private float textButtonFadeOutDuration = 0.5f;

    [Header("Shake Settings")]
    [SerializeField] private float imageShakeStrength = 15f;
    [SerializeField] private float imageShakeDuration = 0.1f;
    [SerializeField] private int shakeVibrato = 30;

    [Header("Audio")]
    [SerializeField] private EventReference loadingEnterSound;
    [SerializeField] private EventReference loadingHideSound;
    [SerializeField] private float loadingBgmFadeInTime = 1.0f;
    [SerializeField] private float loadingBgmFadeOutTime = 1.0f;
    
    private Tween _shakePositionTween;
    private Tween _shakeRotationTween;
    private Coroutine _particleStartCoroutine;
    private Tween _imageEntryTween;
    private Tween _imageExitTween;
    private Tween _buttonFadeInTween;
    private Tween _textFadeOutTween;
    private Tween _buttonFadeOutTween;
    private WaitForSeconds _imageEntryDelayWait;
    private WaitForSecondsRealtime _particleStartDelayWait;
    private WaitForSecondsRealtime _postImageExitWaitWait;
    private CanvasGroup _continueButtonCanvasGroup;
    
    private Vector3 _initialImagePosition;
    private Vector3 _centerImagePosition;
    private float _currentShakeStrength;
    private bool _hasStartedEntryAnimation;
    private bool _isEntryAnimationComplete;
    private bool _isExiting;
    
    public bool IsEntryAnimationComplete => _isEntryAnimationComplete;

    public Image GetBackgroundImage()
    {
        return backgroundImage;
    }

    public float GetFadeOutDuration()
    {
        return fadeOutDuration;
    }

    private void Awake()
    {
        loadingTextString = GameLocalization.GetOrDefault("UI_Common", "loading.success", loadingTextString);
        InitializeUI();
        _hasStartedEntryAnimation = false;
        _imageEntryDelayWait = CoroutineCache.GetWaitForSeconds(imageEntryDelay);
        _particleStartDelayWait = CoroutineCache.GetWaitForSecondsRealtime(particleStartDelay);
        _postImageExitWaitWait = CoroutineCache.GetWaitForSecondsRealtime(postImageExitWaitDuration);
        
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
            continueButton.interactable = false;
            _continueButtonCanvasGroup = continueButton.GetComponent<CanvasGroup>();
            if (_continueButtonCanvasGroup == null)
            {
                _continueButtonCanvasGroup = continueButton.gameObject.AddComponent<CanvasGroup>();
            }
            _continueButtonCanvasGroup.alpha = 0f;
            _continueButtonCanvasGroup.interactable = false;
        }
    }

    private void OnEnable()
    {
        _hasStartedEntryAnimation = false;
        _isExiting = false;
        StartCoroutine(StartEntryAnimationAfterSceneLoad());
    }
    
    private IEnumerator StartEntryAnimationAfterSceneLoad()
    {
        yield return null;
        yield return null;
        
        if (!_hasStartedEntryAnimation)
        {
            InitializeUI();
            StartEntryAnimation();
            _hasStartedEntryAnimation = true;
        }
    }

    private void OnDisable()
    {
        StopEffects();
    }

    private void InitializeUI()
    {
        if (backgroundImage != null) backgroundImage.color = backgroundColor;
        if (loadingText != null)
        {
            loadingText.text = loadingTextString;
            Color textColor = loadingText.color;
            textColor.a = 0f;
            loadingText.color = textColor;
        }
        if (loadingImage != null) 
        {
            _centerImagePosition = loadingImage.transform.localPosition;
            RectTransform rectTransform = loadingImage.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                float canvasHeight = canvas != null && canvas.rootCanvas != null 
                    ? canvas.rootCanvas.GetComponent<RectTransform>().rect.height 
                    : Screen.height;
                _initialImagePosition = _centerImagePosition + Vector3.down * (canvasHeight / 2f + 200f);
            }
            else
            {
                _initialImagePosition = _centerImagePosition + Vector3.down * (Screen.height / 2f + 200f);
            }
            loadingImage.transform.localPosition = _initialImagePosition;
            loadingImage.transform.localRotation = Quaternion.identity;
        }
        if (loadingParticles != null)
        {
            var main = loadingParticles.main;
            main.useUnscaledTime = true;
            loadingParticles.Stop();
        }
        
        _isEntryAnimationComplete = false;
    }

    private void StartEntryAnimation()
    {
        if (loadingImage != null)
        {
            _imageEntryTween?.Kill();
            
            RectTransform imageRect = loadingImage.GetComponent<RectTransform>();
            Canvas canvas = GetComponentInParent<Canvas>();
            
            if (canvas != null && canvas.rootCanvas != null)
            {
                RectTransform canvasRect = canvas.rootCanvas.GetComponent<RectTransform>();
                _centerImagePosition = Vector3.zero;
                
                float canvasHeight = canvasRect.rect.height;
                _initialImagePosition = Vector3.down * (canvasHeight / 2f + 200f);
            }
            else
            {
                _centerImagePosition = Vector3.zero;
                _initialImagePosition = Vector3.down * (Screen.height / 2f + 200f);
            }
            
            if (imageRect != null)
            {
                imageRect.anchoredPosition = new Vector2(0, _initialImagePosition.y);
            }
            else
            {
                loadingImage.transform.localPosition = _initialImagePosition;
            }
            
            if (imageEntryDelay > 0f)
            {
                StartCoroutine(StartEntryAnimationDelayed());
            }
            else
            {
                StartEntryAnimationInternal();
            }
        }
    }
    
    private IEnumerator StartEntryAnimationDelayed()
    {
        yield return _imageEntryDelayWait;
        StartEntryAnimationInternal();
    }
    
    private void StartEntryAnimationInternal()
    {
        if (loadingImage == null) return;

        Time.timeScale = 0f;

        GameObject fadeOverlay = LoadingUIManager.Instance != null
            ? LoadingUIManager.Instance.GetFadeOverlay()
            : GameObject.Find("FadeOverlay");
        if (fadeOverlay != null)
        {
            Image fadeImage = fadeOverlay.GetComponent<Image>();
            if (fadeImage != null)
            {
                fadeImage.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() => {
                    fadeOverlay.SetActive(false);
                });
            }
            else
            {
                fadeOverlay.SetActive(false);
            }
        }

        _isEntryAnimationComplete = false;
        
        RectTransform imageRect = loadingImage.GetComponent<RectTransform>();
        Canvas canvas = GetComponentInParent<Canvas>();
        
        if (canvas != null && canvas.rootCanvas != null)
        {
            _centerImagePosition = Vector3.zero;
        }
        else
        {
            _centerImagePosition = Vector3.zero;
        }
        
        StartImageShake();

        if (loadingParticles != null)
        {
            if (_particleStartCoroutine != null) StopCoroutine(_particleStartCoroutine);
            _particleStartCoroutine = StartCoroutine(StartParticlesDelayed());
        }
        
        if (!loadingEnterSound.IsNull)
        {
            RuntimeManager.PlayOneShot(loadingEnterSound);
        }
        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.PlaySuccessLoadingBgm(loadingBgmFadeInTime);
        }
        
        if (loadingText != null)
        {
            Color textColor = loadingText.color;
            textColor.a = 0f;
            loadingText.color = textColor;
            loadingText.DOFade(1f, imageEntryDuration).SetUpdate(true);
        }

        if (imageRect != null)
        {
            _imageEntryTween = imageRect.DOAnchorPos(Vector2.zero, imageEntryDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => {
                    _isEntryAnimationComplete = true;
                    StartCoroutine(FadeInButton());
                });
        }
        else
        {
            _imageEntryTween = loadingImage.transform.DOLocalMove(_centerImagePosition, imageEntryDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => {
                    _isEntryAnimationComplete = true;
                    StartCoroutine(FadeInButton());
                });
        }
    }
    
    private void StopEffects()
    {
        if (_shakePositionTween != null && _shakePositionTween.IsActive())
        {
            _shakePositionTween.Kill();
        }
        _shakePositionTween = null;

        if (_shakeRotationTween != null && _shakeRotationTween.IsActive())
        {
            _shakeRotationTween.Kill();
        }
        _shakeRotationTween = null;
        
        if (_imageEntryTween != null && _imageEntryTween.IsActive())
        {
            _imageEntryTween.Kill();
        }
        _imageEntryTween = null;
        
        if (_imageExitTween != null && _imageExitTween.IsActive())
        {
            _imageExitTween.Kill();
        }
        _imageExitTween = null;

        if (_buttonFadeInTween != null && _buttonFadeInTween.IsActive())
        {
            _buttonFadeInTween.Kill();
        }
        _buttonFadeInTween = null;

        if (_textFadeOutTween != null && _textFadeOutTween.IsActive())
        {
            _textFadeOutTween.Kill();
        }
        _textFadeOutTween = null;

        if (_buttonFadeOutTween != null && _buttonFadeOutTween.IsActive())
        {
            _buttonFadeOutTween.Kill();
        }
        _buttonFadeOutTween = null;

        if (_particleStartCoroutine != null)
        {
            StopCoroutine(_particleStartCoroutine);
            _particleStartCoroutine = null;
        }

        if (loadingParticles != null)
        {
            loadingParticles.Stop();
        }
    
        if (loadingImage != null)
        {
            loadingImage.transform.localRotation = Quaternion.identity;
        }
    }

    private void StartImageShake()
    {
        if (loadingImage == null) return;

        _shakePositionTween?.Kill();
        _shakeRotationTween?.Kill();

        DoContinuousShake(imageShakeStrength);
    }

    private void DoContinuousShake(float strength)
    {
        if (visualChild == null) return;
        
        _currentShakeStrength = strength;

        _shakePositionTween = visualChild.DOShakePosition(
                imageShakeDuration, 
                new Vector3(strength, strength, 0), 
                shakeVibrato, 
                90, 
                false, 
                false 
            )
            .SetLink(visualChild.gameObject)
            .SetUpdate(true)
            .OnComplete(() => DoContinuousShake(_currentShakeStrength));
    }

    private IEnumerator StartParticlesDelayed()
    {
        yield return _particleStartDelayWait;
        if (loadingParticles != null) loadingParticles.Play();
    }

    private IEnumerator FadeInButton()
    {
        if (continueButton == null || _continueButtonCanvasGroup == null)
        {
            yield break;
        }

        yield return new WaitForSecondsRealtime(0.3f);

        _buttonFadeInTween?.Kill();
        _buttonFadeInTween = _continueButtonCanvasGroup.DOFade(1f, buttonFadeInDuration)
            .SetUpdate(true)
            .OnComplete(() => {
                if (continueButton != null)
                {
                    continueButton.interactable = true;
                    _continueButtonCanvasGroup.interactable = true;
                }
            });
    }

    private void OnContinueClicked()
    {
        if (_isExiting) return;
        _isExiting = true;
        
        if (!loadingHideSound.IsNull)
        {
            RuntimeManager.PlayOneShot(loadingHideSound);
        }
        
        if (continueButton != null)
        {
            continueButton.interactable = false;
        }
        
        StartCoroutine(ExitSequence());
    }

    private IEnumerator ExitSequence()
    {
        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.StopSuccessLoadingBgm(loadingBgmFadeOutTime);
        }

        if (loadingText != null)
        {
            _textFadeOutTween?.Kill();
            _textFadeOutTween = loadingText.DOFade(0f, textButtonFadeOutDuration).SetUpdate(true);
        }

        if (_continueButtonCanvasGroup != null)
        {
            _buttonFadeOutTween?.Kill();
            _buttonFadeOutTween = _continueButtonCanvasGroup.DOFade(0f, textButtonFadeOutDuration).SetUpdate(true);
        }

        if (loadingImage != null)
        {
            RectTransform rectTransform = loadingImage.GetComponent<RectTransform>();
            float canvasHeight = Screen.height;
            if (rectTransform != null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                canvasHeight = canvas != null && canvas.rootCanvas != null 
                    ? canvas.rootCanvas.GetComponent<RectTransform>().rect.height 
                    : Screen.height;
            }
            Vector3 exitPosition = _centerImagePosition + Vector3.up * (canvasHeight / 2f + 400f);

            _imageExitTween?.Kill();
            _imageExitTween = loadingImage.transform.DOLocalMove(exitPosition, imageExitDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true);
        }

        if (_imageExitTween != null && _imageExitTween.IsActive())
        {
            yield return _imageExitTween.WaitForCompletion();
        }

        yield return _postImageExitWaitWait;

        Time.timeScale = 1f;
        
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.CompleteBaseSceneLoad();
        }
    }
}
