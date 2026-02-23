using System;
using System.Collections;
using UnityEngine;

public class EnemyDaySinkingEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Transform spriteRoot;
    [SerializeField] private ParticleSystem sinkParticle;

    [Header("Sinking")]
    [SerializeField] private float sinkDuration = 0.6f;
    [SerializeField] private float sinkDistance = 0.7f;

    [Header("Rise")]
    [SerializeField] private bool playRiseOnEnable = true;
    [SerializeField] private float riseDuration = 0.45f;
    [SerializeField] private float riseDistance = 0.7f;

    [Header("Shake")]
    [SerializeField] private float shakeAmplitude = 0.05f;
    [SerializeField] private float shakeFrequency = 28f;

    [Header("Particle")]
    [SerializeField] private bool waitForParticleFadeOut = true;
    [SerializeField] private float particleFadeOutMaxWait = 0.35f;

    private Coroutine _sinkingCoroutine;
    private Coroutine _risingCoroutine;
    private Action _onComplete;
    private UnitMovement _unitMovement;
    private Rigidbody2D _rigidbody2D;
    private EnemyUnitBase _enemyUnitBase;
    private SpriteRenderer[] _spriteRenderers;
    private Color[] _spriteColorsBeforeHide;
    private bool _isSpriteAlphaHidden;
    private Vector3 _startLocalPosition;

    public bool IsSinking { get; private set; }
    public bool IsRising { get; private set; }

    private void Awake()
    {
        _unitMovement = GetComponent<UnitMovement>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _enemyUnitBase = GetComponent<EnemyUnitBase>();
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _spriteColorsBeforeHide = new Color[_spriteRenderers.Length];

        if (spriteRoot == null && targetRenderer != null)
        {
            spriteRoot = targetRenderer.transform;
        }

        if (spriteRoot != null)
        {
            _startLocalPosition = spriteRoot.localPosition;
        }
    }

    private void OnEnable()
    {
        StopAllRunningRoutines();
        StopParticleImmediate();
        RestoreSpriteAlpha();

        if (spriteRoot != null)
        {
            spriteRoot.localPosition = _startLocalPosition;
        }

        if (playRiseOnEnable)
        {
            PreSetupRisingState();
            _risingCoroutine = StartCoroutine(RisingRoutine());
        }
        else
        {
            ResetVisualState();
            _unitMovement?.ResumeMovement();
        }
    }

    private void OnDisable()
    {
        StopAllRunningRoutines();
        _enemyUnitBase?.SetBehaviorPaused(false);
        ResetVisualState();
    }

    private void PreSetupRisingState()
    {
        IsRising = true;
        _enemyUnitBase?.SetBehaviorPaused(true);
        _unitMovement?.ForceStopAllMovement();

        if (spriteRoot != null)
        {
            spriteRoot.localPosition = new Vector3(_startLocalPosition.x, _startLocalPosition.y - riseDistance, _startLocalPosition.z);
        }
    }

    public void StartSinking(Action onComplete = null)
    {
        if (!isActiveAndEnabled || IsSinking)
        {
            return;
        }

        if (_risingCoroutine != null)
        {
            StopCoroutine(_risingCoroutine);
            _risingCoroutine = null;
            IsRising = false;
        }

        _enemyUnitBase?.ForceResetStateForSinking();
        _enemyUnitBase?.SetBehaviorPaused(true);
        _unitMovement?.ForceStopAllMovement();
        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
        }

        if (spriteRoot != null)
        {
            spriteRoot.localPosition = _startLocalPosition;
        }

        _onComplete = onComplete;
        _sinkingCoroutine = StartCoroutine(SinkingRoutine());
    }

    private IEnumerator SinkingRoutine()
    {
        IsSinking = true;

        if (spriteRoot != null)
        {
            _startLocalPosition = spriteRoot.localPosition;
        }

        PlayParticle();
        yield return AnimateSinking();
        HideSpriteAlphaForParticleOnly();
        yield return StopParticleWithFadeOut();

        IsSinking = false;
        _sinkingCoroutine = null;

        Action onComplete = _onComplete;
        _onComplete = null;
        if (onComplete != null)
        {
            onComplete.Invoke();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator RisingRoutine()
    {
        IsRising = true;
        _enemyUnitBase?.SetBehaviorPaused(true);
        _unitMovement?.ForceStopAllMovement();

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
        }

        if (spriteRoot != null)
        {
            spriteRoot.localPosition = new Vector3(_startLocalPosition.x, _startLocalPosition.y - riseDistance, _startLocalPosition.z);
        }

        PlayParticle();
        yield return AnimateRising();

        if (spriteRoot != null)
        {
            spriteRoot.localPosition = _startLocalPosition;
        }

        yield return StopParticleWithFadeOut();

        _unitMovement?.ResumeMovement();
        _enemyUnitBase?.SetBehaviorPaused(false);

        IsRising = false;
        _risingCoroutine = null;
    }

    private IEnumerator AnimateSinking()
    {
        if (sinkDuration <= 0f)
        {
            ApplySinkingFrame(1f, 0f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / sinkDuration);
            ApplySinkingFrame(t, elapsed);
            yield return null;
        }
    }

    private IEnumerator AnimateRising()
    {
        if (riseDuration <= 0f)
        {
            ApplyRisingFrame(1f, 0f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / riseDuration);
            ApplyRisingFrame(t, elapsed);
            yield return null;
        }
    }

    private void ApplySinkingFrame(float t, float elapsed)
    {
        if (spriteRoot == null)
        {
            return;
        }

        float shakeOffsetX = Mathf.Sin(elapsed * shakeFrequency) * shakeAmplitude * (1f - t);
        Vector3 pos = _startLocalPosition;
        pos.x += shakeOffsetX;
        pos.y -= sinkDistance * t;
        spriteRoot.localPosition = pos;
    }

    private void ApplyRisingFrame(float t, float elapsed)
    {
        if (spriteRoot == null)
        {
            return;
        }

        float shakeOffsetX = Mathf.Sin(elapsed * shakeFrequency) * shakeAmplitude * (1f - t);
        Vector3 pos = _startLocalPosition;
        pos.x += shakeOffsetX;
        pos.y -= riseDistance * (1f - t);
        spriteRoot.localPosition = pos;
    }

    private void ResetVisualState()
    {
        RestoreSpriteAlpha();

        if (spriteRoot != null)
        {
            spriteRoot.localPosition = _startLocalPosition;
        }

        StopParticleImmediate();
    }

    private void EnsureSpriteRendererCache()
    {
        if (_spriteRenderers == null || _spriteRenderers.Length == 0)
        {
            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        if (_spriteColorsBeforeHide == null || _spriteColorsBeforeHide.Length != _spriteRenderers.Length)
        {
            _spriteColorsBeforeHide = new Color[_spriteRenderers.Length];
        }
    }

    private void HideSpriteAlphaForParticleOnly()
    {
        if (_isSpriteAlphaHidden)
        {
            return;
        }

        EnsureSpriteRendererCache();

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr != null)
            {
                _spriteColorsBeforeHide[i] = sr.color;
                Color c = sr.color;
                c.a = 0f;
                sr.color = c;
            }
        }

        _isSpriteAlphaHidden = true;
    }

    private void RestoreSpriteAlpha()
    {
        if (!_isSpriteAlphaHidden)
        {
            return;
        }

        EnsureSpriteRendererCache();

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr != null)
            {
                sr.color = _spriteColorsBeforeHide[i];
            }
        }

        _isSpriteAlphaHidden = false;
    }

    private IEnumerator StopParticleWithFadeOut()
    {
        if (sinkParticle == null)
        {
            yield break;
        }

        sinkParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (!waitForParticleFadeOut)
        {
            yield break;
        }

        float wait = 0f;
        while (wait < particleFadeOutMaxWait && sinkParticle.IsAlive(true))
        {
            wait += Time.deltaTime;
            yield return null;
        }
    }

    private void PlayParticle()
    {
        if (sinkParticle != null)
        {
            sinkParticle.Play();
        }
    }

    private void StopParticleImmediate()
    {
        if (sinkParticle == null)
        {
            return;
        }

        sinkParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void StopAllRunningRoutines()
    {
        if (_sinkingCoroutine != null)
        {
            StopCoroutine(_sinkingCoroutine);
            _sinkingCoroutine = null;
        }

        if (_risingCoroutine != null)
        {
            StopCoroutine(_risingCoroutine);
            _risingCoroutine = null;
        }

        IsSinking = false;
        IsRising = false;
        _onComplete = null;
    }
}
