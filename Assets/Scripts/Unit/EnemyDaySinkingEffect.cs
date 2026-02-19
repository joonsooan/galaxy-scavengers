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
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private float clipStart = -1f;
    [SerializeField] private float clipEnd = 1f;
    [SerializeField] private float sinkDistance = 0.7f;

    [Header("Shake")]
    [SerializeField] private float shakeAmplitude = 0.05f;
    [SerializeField] private float shakeFrequency = 28f;

    [Header("Particle")]
    [SerializeField] private bool waitForParticleFadeOut = true;
    [SerializeField] private float particleFadeOutMaxWait = 0.35f;

    private static readonly int ClipHeightId = Shader.PropertyToID("_ClipHeight");

    private MaterialPropertyBlock _propertyBlock;
    private Coroutine _sinkingCoroutine;
    private Action _onComplete;
    private UnitMovement _unitMovement;
    private Rigidbody2D _rigidbody2D;
    private Vector3 _startLocalPosition;

    public bool IsSinking { get; private set; }

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>(true);
        }

        if (spriteRoot == null && targetRenderer != null)
        {
            spriteRoot = targetRenderer.transform;
        }

        _unitMovement = GetComponent<UnitMovement>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        if (spriteRoot != null)
        {
            _startLocalPosition = spriteRoot.localPosition;
        }

        SetClipHeight(GetVisibleClipHeight());
    }

    private void OnEnable()
    {
        ResetVisualState();
    }

    private void OnDisable()
    {
        if (_sinkingCoroutine != null)
        {
            StopCoroutine(_sinkingCoroutine);
            _sinkingCoroutine = null;
        }

        IsSinking = false;
        _onComplete = null;
        ResetVisualState();
    }

    public void StartSinking(Action onComplete = null)
    {
        if (!isActiveAndEnabled || IsSinking)
        {
            return;
        }

        SetClipHeight(clipStart);
        _onComplete = onComplete;
        _sinkingCoroutine = StartCoroutine(SinkingRoutine());
    }

    private IEnumerator SinkingRoutine()
    {
        IsSinking = true;

        if (_unitMovement != null)
        {
            _unitMovement.ForceStopAllMovement();
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
        }

        if (spriteRoot != null)
        {
            _startLocalPosition = spriteRoot.localPosition;
        }

        if (sinkParticle != null)
        {
            sinkParticle.Play();
        }

        if (duration <= 0f)
        {
            ApplySinkingFrame(1f, 0f);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ApplySinkingFrame(t, elapsed);
                yield return null;
            }
        }

        if (sinkParticle != null)
        {
            sinkParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (waitForParticleFadeOut)
            {
                float wait = 0f;
                while (wait < particleFadeOutMaxWait && sinkParticle.IsAlive(true))
                {
                    wait += Time.deltaTime;
                    yield return null;
                }
            }
        }

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

    private void ApplySinkingFrame(float t, float elapsed)
    {
        SetClipHeight(Mathf.Lerp(clipStart, clipEnd, t));

        if (spriteRoot == null)
        {
            return;
        }

        float shakeOffsetX = Mathf.Sin(elapsed * shakeFrequency) * shakeAmplitude * (1f - t);
        Vector3 nextLocalPosition = _startLocalPosition;
        nextLocalPosition.x += shakeOffsetX;
        nextLocalPosition.y -= sinkDistance * t;
        spriteRoot.localPosition = nextLocalPosition;
    }

    private void ResetVisualState()
    {
        if (targetRenderer == null) return;
        
        SetClipHeight(GetVisibleClipHeight());

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

    private float GetVisibleClipHeight()
    {
        return Mathf.Min(clipStart, clipEnd);
    }
}
