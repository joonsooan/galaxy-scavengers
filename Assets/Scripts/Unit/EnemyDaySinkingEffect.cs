using System;
using System.Collections;
using UnityEngine;

public class EnemyDaySinkingEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Transform spriteRoot;
    [SerializeField] private ParticleSystem sinkParticle;

    [Header("Clip")]
    [SerializeField] private float clipStart = -1f;
    [SerializeField] private float clipEnd = 1f;
    [SerializeField] private float idleClipHeight = -1f;

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

    private static readonly int ClipHeightId = Shader.PropertyToID("_ClipHeight");

    private MaterialPropertyBlock _propertyBlock;
    private Coroutine _sinkingCoroutine;
    private Coroutine _risingCoroutine;
    private Action _onComplete;
    private UnitMovement _unitMovement;
    private Rigidbody2D _rigidbody2D;
    private EnemyUnitBase _enemyUnitBase;
    private Vector3 _startLocalPosition;

    public bool IsSinking { get; private set; }
    public bool IsRising { get; private set; }

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
        _unitMovement = GetComponent<UnitMovement>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _enemyUnitBase = GetComponent<EnemyUnitBase>();

        targetRenderer = ResolveTargetRenderer(targetRenderer);
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
        targetRenderer = ResolveTargetRenderer(targetRenderer);
        ResetVisualState();

        if (playRiseOnEnable)
        {
            _risingCoroutine = StartCoroutine(RisingRoutine());
        }
        else if (_unitMovement != null)
        {
            _unitMovement.ResumeMovement();
        }
    }

    private void OnDisable()
    {
        StopAllRunningRoutines();
        _enemyUnitBase?.SetBehaviorPaused(false);
        ResetVisualState();
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
        _unitMovement?.ForceStopAllMovement();
        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
        }

        SetClipHeight(clipStart);
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
        SetClipHeight(clipEnd);
        yield return AnimateRising();
        yield return StopParticleWithFadeOut();

        SetClipHeight(idleClipHeight);
        if (spriteRoot != null)
        {
            spriteRoot.localPosition = _startLocalPosition;
        }

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
            ApplyRisingFrame(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / riseDuration);
            ApplyRisingFrame(t);
            yield return null;
        }
    }

    private void ApplySinkingFrame(float t, float elapsed)
    {
        SetClipHeight(Mathf.Lerp(clipStart, clipEnd, t));
        if (spriteRoot == null) return;

        float shakeOffsetX = Mathf.Sin(elapsed * shakeFrequency) * shakeAmplitude * (1f - t);
        Vector3 pos = _startLocalPosition;
        pos.x += shakeOffsetX;
        pos.y -= sinkDistance * t;
        spriteRoot.localPosition = pos;
    }

    private void ApplyRisingFrame(float t)
    {
        SetClipHeight(Mathf.Lerp(clipEnd, clipStart, t));
        if (spriteRoot == null) return;

        Vector3 pos = _startLocalPosition;
        pos.y -= riseDistance * (1f - t);
        spriteRoot.localPosition = pos;
    }

    private void ResetVisualState()
    {
        SetClipHeight(idleClipHeight);
        if (spriteRoot != null)
        {
            spriteRoot.localPosition = _startLocalPosition;
        }

        if (sinkParticle != null)
        {
            sinkParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void SetClipHeight(float value)
    {
        if (targetRenderer == null || _propertyBlock == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(ClipHeightId, value);
        targetRenderer.SetPropertyBlock(_propertyBlock);
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

    private Renderer ResolveTargetRenderer(Renderer current)
    {
        if (HasClipHeightProperty(current))
        {
            return current;
        }

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (HasClipHeightProperty(sprites[i]))
            {
                return sprites[i];
            }
        }

        return null;
    }

    private bool HasClipHeightProperty(Renderer renderer)
    {
        return renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(ClipHeightId);
    }
}
