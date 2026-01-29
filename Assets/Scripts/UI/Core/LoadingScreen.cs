using System.Collections;
using DG.Tweening;
using FMODUnity;
using Systems.Jobs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour, IInitializationProgress
{
    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private Image loadingImage;
    [SerializeField] private ParticleSystem loadingParticles;

    [Header("Progress UI Elements")]
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Image progressBarFill;
    [SerializeField] private GameObject progressBarContainer;

    [Header("Visual Settings")]
    [SerializeField] private string loadingTextString = "로딩 중...";
    [SerializeField] private Color backgroundColor = new (0f, 0f, 0f, 0.9f);

    [Header("Timing - Entry & Particles")]
    [SerializeField] private float imageEntryDelay;
    [SerializeField] private float imageEntryDuration = 0.8f;
    [SerializeField] private float particleStartDelay = 1f;

    [Header("Timing - Fade Sequence")]
    [SerializeField] private float postInitWaitDuration = 1.0f;
    [SerializeField] private float imageExitDuration = 1.0f;
    [SerializeField] private float contentFadeOutDuration = 0.5f;
    [SerializeField] private float imageExitDelay = 2.0f;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private float resumeDelay = 0.5f;

    [Header("Shake Settings")]
    [SerializeField] private float imageShakeStrength = 15f;
    [SerializeField] private float imageShakeDuration = 0.1f;
    [SerializeField] private int shakeVibrato = 30;
    [SerializeField] private float maxShakeStrengthMultiplier = 3.0f;

    [Header("Audio")]
    [SerializeField] private EventReference loadingEnterSound;
    [SerializeField] private EventReference loadingHideSound;
    
    private Tween _shakePositionTween;
    private Tween _shakeRotationTween;
    private Coroutine _particleStartCoroutine;
    private Tween _imageEntryTween;
    private Tween _imageExitTween;
    
    private float _currentProgress;
    private string _currentStage = "";
    private Vector3 _initialImagePosition;
    private Vector3 _centerImagePosition;
    private bool _isInitializationComplete;
    private float _currentShakeStrength;
    private Tween _shakeStrengthTween;
    private bool _hasStartedEntryAnimation;
    private bool _isEntryAnimationComplete;
    
    public bool IsEntryAnimationComplete => _isEntryAnimationComplete;

    private void Awake()
    {
        InitializeUI();
        _hasStartedEntryAnimation = false;
    }

    private void OnEnable()
    {
        _hasStartedEntryAnimation = false;
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
        if (loadingText != null) loadingText.text = loadingTextString;
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
            loadingImage.transform.localRotation = Quaternion.identity;
        }
        if (loadingParticles != null)
        {
            var main = loadingParticles.main;
            main.useUnscaledTime = true;
            loadingParticles.Stop();
        }
        
        _currentProgress = 0f;
        _currentStage = "";
        UpdateProgressUI();

        LoadingStagePanelAnimator stageAnimator = GetComponentInChildren<LoadingStagePanelAnimator>(true);
        if (stageAnimator != null)
        {
            RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform rt in rects)
            {
                if (rt != null && rt.name == "_Parent")
                {
                    stageAnimator.SetPanelParent(rt);
                    break;
                }
            }
        }
        
        if (progressBarContainer != null)
        {
            progressBarContainer.SetActive(true);
        }
        
        _isInitializationComplete = false;
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
                _initialImagePosition = Vector3.up * (canvasHeight / 2f + 200f);
            }
            else
            {
                _centerImagePosition = Vector3.zero;
                _initialImagePosition = Vector3.up * (Screen.height / 2f + 200f);
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
        yield return new WaitForSeconds(imageEntryDelay);
        StartEntryAnimationInternal();
    }
    
    private void StartEntryAnimationInternal()
    {
        if (loadingImage == null) return;

        Time.timeScale = 0f;

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
        
        if (imageRect != null)
        {
            _imageEntryTween = imageRect.DOAnchorPos(Vector2.zero, imageEntryDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnStart(() => {
                    if (!loadingEnterSound.IsNull)
                    {
                        RuntimeManager.PlayOneShot(loadingEnterSound);
                    }
                })
                .OnComplete(() => {
                    _isEntryAnimationComplete = true;
                });
        }
        else
        {
            _imageEntryTween = loadingImage.transform.DOLocalMove(_centerImagePosition, imageEntryDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnStart(() => {
                    if (!loadingEnterSound.IsNull)
                    {
                        RuntimeManager.PlayOneShot(loadingEnterSound);
                    }
                })
                .OnComplete(() => {
                    _isEntryAnimationComplete = true;
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
        
        if (_shakeStrengthTween != null && _shakeStrengthTween.IsActive())
        {
            _shakeStrengthTween.Kill();
        }
        _shakeStrengthTween = null;

        if (_particleStartCoroutine != null)
        {
            StopCoroutine(_particleStartCoroutine);
            _particleStartCoroutine = null;
        }

        if (loadingParticles != null)
        {
            loadingParticles.Stop();
        }
    
        // Don't reset position - let it stay where it is (at exit position)
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
        
        _currentShakeStrength = strength;

        _shakePositionTween = loadingImage.transform.DOShakePosition(
                imageShakeDuration, 
                new Vector3(strength, strength, 0), 
                shakeVibrato, 
                90, 
                false, 
                false 
            )
            .SetLink(loadingImage.gameObject)
            .SetUpdate(true)
            .OnComplete(() => DoContinuousShake(_currentShakeStrength));
    }

    private IEnumerator StartParticlesDelayed()
    {
        yield return new WaitForSecondsRealtime(particleStartDelay);
        if (loadingParticles != null) loadingParticles.Play();
    }

    public IEnumerator FadeOutSequence()
    {
        while (!_isInitializationComplete)
        {
            yield return null;
        }

        yield return new WaitForSecondsRealtime(postInitWaitDuration);

        if (!loadingHideSound.IsNull)
        {
            RuntimeManager.PlayOneShot(loadingHideSound);
        }

        if (progressText != null)
        {
            progressText.DOFade(0f, contentFadeOutDuration).SetUpdate(true);
        }
        if (loadingText != null)
        {
            loadingText.DOFade(0f, contentFadeOutDuration).SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(contentFadeOutDuration);

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
            Vector3 exitPosition = _centerImagePosition + Vector3.down * (canvasHeight / 2f + 400f);
            float currentShakeStrength = imageShakeStrength;
            float targetShakeStrength = imageShakeStrength * maxShakeStrengthMultiplier;
            
            float shakeTweenTime = 0f;
            _shakeStrengthTween?.Kill();
            _shakeStrengthTween = DOTween.To(() => shakeTweenTime, x => shakeTweenTime = x, 1f, imageExitDuration)
                .SetUpdate(true)
                .OnUpdate(() => {
                    float strength = Mathf.Lerp(currentShakeStrength, targetShakeStrength, shakeTweenTime);
                    _currentShakeStrength = strength;
                    if (_shakePositionTween != null && _shakePositionTween.IsActive())
                    {
                        _shakePositionTween.Kill();
                    }
                    DoContinuousShake(strength);
                });
            
            LoadingStagePanelAnimator stageAnimator = GetComponentInChildren<LoadingStagePanelAnimator>(true);
            if (stageAnimator != null)
            {
                CanvasGroup[] groups = stageAnimator.GetComponentsInChildren<CanvasGroup>(true);
                foreach (CanvasGroup group in groups)
                {
                    if (group == null) continue;
                    group.DOFade(0f, imageExitDuration).SetUpdate(true);
                }
            }

            _imageExitTween?.Kill();
            _imageExitTween = loadingImage.transform.DOLocalMove(exitPosition, imageExitDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true);
        }

        if (_imageExitTween != null && _imageExitTween.IsActive())
        {
            yield return _imageExitTween.WaitForCompletion();
        }
        if (_shakeStrengthTween != null && _shakeStrengthTween.IsActive())
        {
            _shakeStrengthTween.Kill();
        }
        _shakeStrengthTween = null;

        yield return new WaitForSecondsRealtime(imageExitDelay);

        if (backgroundImage != null)
        {
            yield return backgroundImage.DOFade(0f, fadeOutDuration).SetUpdate(true).WaitForCompletion();
        }

        yield return new WaitForSecondsRealtime(resumeDelay);
        
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
        
        if (!string.IsNullOrEmpty(_currentStage))
        {
            LoadingStagePanelAnimator.InvokeLoadingStageChanged(_currentStage);
        }
        
        UpdateProgressUI();
    }
    
    private void UpdateProgressUI()
    {
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
        
        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = _currentProgress;
        }
        
        if (loadingText != null && !string.IsNullOrEmpty(_currentStage))
        {
            loadingText.text = _currentStage;
        }
    }
}