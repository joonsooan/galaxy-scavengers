using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LoadingScreen : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMPro.TextMeshProUGUI loadingText;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private Image loadingImage;
    [SerializeField] private ParticleSystem loadingParticles;

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

    private Tween _shakePositionTween;
    private Tween _shakeRotationTween;
    private Coroutine _particleStartCoroutine;

    private void Awake()
    {
        InitializeUI();
    }

    private void OnEnable()
    {
        StartEffects();
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
        if (loadingImage != null) loadingImage.color = Color.white;
        if (loadingParticles != null) loadingParticles.Stop();
    }

    private void StartEffects()
    {
        if (loadingImage != null)
        {
            StartImageShake();
        }

        if (loadingParticles != null)
        {
            if (_particleStartCoroutine != null) StopCoroutine(_particleStartCoroutine);
            _particleStartCoroutine = StartCoroutine(StartParticlesDelayed());
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
            loadingImage.transform.localPosition = Vector3.zero;
            loadingImage.transform.localRotation = Quaternion.identity;
        }
    }

    private void StartImageShake()
    {
        if (loadingImage == null) return;

        _shakePositionTween?.Kill();
        _shakeRotationTween?.Kill();

        DoContinuousShake();
    }

    private void DoContinuousShake()
    {
        if (loadingImage == null || loadingImage.gameObject == null) return;

        _shakePositionTween = loadingImage.transform.DOShakePosition(
                imageShakeDuration, 
                new Vector3(imageShakeStrength, imageShakeStrength, 0), 
                shakeVibrato, 
                90, 
                false, 
                false 
            )
            .SetLink(loadingImage.gameObject)
            .OnComplete(DoContinuousShake);
    }

    private IEnumerator StartParticlesDelayed()
    {
        yield return new WaitForSeconds(particleStartDelay);
        if (loadingParticles != null) loadingParticles.Play();
    }

    public IEnumerator FadeOutSequence()
    {
        StopEffects();

        if (Time.timeScale > 0f) Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(contentFadeOutDuration);

        if (loadingImage != null) loadingImage.DOFade(0f, contentFadeOutDuration).SetUpdate(true);
        if (loadingText != null) loadingText.DOFade(0f, contentFadeOutDuration).SetUpdate(true);

        if (loadingIndicator != null)
        {
            CanvasGroup indicatorGroup = loadingIndicator.GetComponent<CanvasGroup>();
            if (indicatorGroup == null) indicatorGroup = loadingIndicator.AddComponent<CanvasGroup>();
            indicatorGroup.DOFade(0f, contentFadeOutDuration).SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(contentFadeOutDuration);

        if (backgroundImage != null)
        {
            yield return backgroundImage.DOFade(0f, fadeOutDuration).SetUpdate(true).WaitForCompletion();
        }

        yield return new WaitForSecondsRealtime(resumeDelay);

        Time.timeScale = 1f;
        
        gameObject.SetActive(false); 
    }
}