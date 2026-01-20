using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Systems.Jobs;

public class LoadingScreen : MonoBehaviour, IInitializationProgress
{
    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMPro.TextMeshProUGUI loadingText;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private Image loadingImage;
    [SerializeField] private ParticleSystem loadingParticles;
    
    [Header("Progress UI Elements")]
    [SerializeField] private TMPro.TextMeshProUGUI progressText;
    [SerializeField] private Image progressBarFill;
    [SerializeField] private GameObject progressBarContainer;

    [Header("Settings")]
    [SerializeField] private string loadingTextString = "로딩 중...";
    [SerializeField] private Color backgroundColor = new (0f, 0f, 0f, 0.9f);
    [SerializeField] private float particleStartDelay = 1f;
    
    [Header("Shake Settings")]
    [SerializeField] private float imageShakeStrength = 15f;
    [SerializeField] private float imageShakeDuration = 0.1f;
    [SerializeField] private int shakeVibrato = 30;
    
    [Header("Fade Settings")]
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private float contentFadeOutDuration = 0.5f;
    [SerializeField] private float resumeDelay = 0.5f;
    
    [Header("Animation Settings")]
    [SerializeField] private float imageEntryDuration = 0.8f;
    [SerializeField] private float imageExitDuration = 1.0f;
    [SerializeField] private float postInitWaitDuration = 1.0f;
    [SerializeField] private float imageExitDelay = 2.0f;
    [SerializeField] private float maxShakeStrengthMultiplier = 3.0f;
    
    private Tween _shakePositionTween;
    private Tween _shakeRotationTween;
    private Coroutine _particleStartCoroutine;
    private Tween _imageEntryTween;
    private Tween _imageExitTween;
    
    private float _currentProgress = 0f;
    private string _currentStage = "";
    private Vector3 _initialImagePosition;
    private Vector3 _centerImagePosition;
    private bool _isInitializationComplete = false;

    private void Awake()
    {
        InitializeUI();
    }

    private void OnEnable()
    {
        StartEntryAnimation();
    }

    private void OnDisable()
    {
        StopEffects();
    }

    private void InitializeUI()
    {
        if (backgroundImage != null) backgroundImage.color = backgroundColor;
        if (loadingText != null) loadingText.text = loadingTextString;
        if (loadingIndicator != null) loadingIndicator.SetActive(true);
        if (loadingImage != null) 
        {
            loadingImage.color = Color.white;
            _centerImagePosition = loadingImage.transform.localPosition;
            RectTransform rectTransform = loadingImage.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                float canvasHeight = canvas != null && canvas.rootCanvas != null 
                    ? canvas.rootCanvas.GetComponent<RectTransform>().rect.height 
                    : Screen.height;
                _initialImagePosition = _centerImagePosition + Vector3.up * (canvasHeight / 2f + 200f);
            }
            else
            {
                _initialImagePosition = _centerImagePosition + Vector3.up * (Screen.height / 2f + 200f);
            }
            loadingImage.transform.localPosition = _initialImagePosition;
        }
        if (loadingParticles != null) loadingParticles.Stop();
        
        // Initialize progress UI
        _currentProgress = 0f;
        _currentStage = "";
        UpdateProgressUI();
        
        if (progressBarContainer != null)
        {
            progressBarContainer.SetActive(true);
        }
        
        _isInitializationComplete = false;
    }

    private void StartEntryAnimation()
    {
        if (loadingImage != null)
        {
            _imageEntryTween?.Kill();
            loadingImage.transform.localPosition = _initialImagePosition;
            _imageEntryTween = loadingImage.transform.DOLocalMove(_centerImagePosition, imageEntryDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => {
                    StartImageShake();
                    if (loadingParticles != null)
                    {
                        if (_particleStartCoroutine != null) StopCoroutine(_particleStartCoroutine);
                        _particleStartCoroutine = StartCoroutine(StartParticlesDelayed());
                    }
                    
                    OnImageReachedCenter();
                });
        }
    }
    
    private void OnImageReachedCenter()
    {
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
            loadingImage.transform.localPosition = _centerImagePosition;
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
    
    private void StartImageShakeWithStrength(float strength)
    {
        if (loadingImage == null) return;

        _shakePositionTween?.Kill();
        _shakeRotationTween?.Kill();

        DoContinuousShake(strength);
    }

    private void DoContinuousShake(float strength)
    {
        if (loadingImage == null || loadingImage.gameObject == null) return;

        _shakePositionTween = loadingImage.transform.DOShakePosition(
                imageShakeDuration, 
                new Vector3(strength, strength, 0), 
                shakeVibrato, 
                90, 
                false, 
                false 
            )
            .SetLink(loadingImage.gameObject)
            .OnComplete(() => DoContinuousShake(strength));
    }

    private IEnumerator StartParticlesDelayed()
    {
        yield return new WaitForSeconds(particleStartDelay);
        if (loadingParticles != null) loadingParticles.Play();
    }

    public IEnumerator FadeOutSequence()
    {
        // Wait for initialization to complete (1 second after completion)
        while (!_isInitializationComplete)
        {
            yield return null;
        }
        
        yield return new WaitForSecondsRealtime(postInitWaitDuration);

        if (Time.timeScale > 0f) Time.timeScale = 0f;

        // Hide progress text
        if (progressText != null)
        {
            progressText.DOFade(0f, contentFadeOutDuration).SetUpdate(true);
        }
        if (loadingText != null)
        {
            loadingText.DOFade(0f, contentFadeOutDuration).SetUpdate(true);
        }

        if (loadingIndicator != null)
        {
            CanvasGroup indicatorGroup = loadingIndicator.GetComponent<CanvasGroup>();
            if (indicatorGroup == null) indicatorGroup = loadingIndicator.AddComponent<CanvasGroup>();
            indicatorGroup.DOFade(0f, contentFadeOutDuration).SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(contentFadeOutDuration);

        // Move image down with increasing shake strength
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
            Vector3 exitPosition = _centerImagePosition + Vector3.down * (canvasHeight / 2f + 200f);
            float currentShakeStrength = imageShakeStrength;
            float targetShakeStrength = imageShakeStrength * maxShakeStrengthMultiplier;
            
            // Create shake strength tween that increases over time
            float shakeTweenTime = 0f;
            Tween shakeStrengthTween = DOTween.To(() => shakeTweenTime, x => shakeTweenTime = x, 1f, imageExitDuration)
                .OnUpdate(() => {
                    float strength = Mathf.Lerp(currentShakeStrength, targetShakeStrength, shakeTweenTime);
                    // Update shake strength dynamically
                    _shakePositionTween?.Kill();
                    DoContinuousShake(strength);
                });
            
            // Move image down with Ease In
            _imageExitTween?.Kill();
            _imageExitTween = loadingImage.transform.DOLocalMove(exitPosition, imageExitDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() => {
                    StopEffects();
                });
            
            yield return _imageExitTween.WaitForCompletion();
            shakeStrengthTween?.Kill();
        }

        yield return new WaitForSecondsRealtime(imageExitDelay);

        // Fade out background
        if (backgroundImage != null)
        {
            yield return backgroundImage.DOFade(0f, fadeOutDuration).SetUpdate(true).WaitForCompletion();
        }

        yield return new WaitForSecondsRealtime(resumeDelay);

        Time.timeScale = 1f;
        
        gameObject.SetActive(false); 
    }
    
    public void SetInitializationComplete()
    {
        _isInitializationComplete = true;
    }
    
    public void UpdateProgress(float progress, string stage)
    {
        _currentProgress = Mathf.Clamp01(progress);
        _currentStage = stage;
        
        UpdateProgressUI();
    }
    
    private void UpdateProgressUI()
    {
        // Update progress text (message only, no percentage)
        if (progressText != null)
        {
            if (!string.IsNullOrEmpty(_currentStage))
            {
                progressText.text = _currentStage;
            }
            else
            {
                progressText.text = "";
            }
        }
        
        // Update progress bar
        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = _currentProgress;
        }
        
        // Update loading text with stage if provided
        if (loadingText != null && !string.IsNullOrEmpty(_currentStage))
        {
            loadingText.text = _currentStage;
        }
    }
}