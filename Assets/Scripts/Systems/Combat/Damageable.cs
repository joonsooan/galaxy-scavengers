using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public abstract class Damageable : MonoBehaviour, ICombo
{
    [Header("Health Settings")]
    [SerializeField] protected int maxHealth = 100;

    [Header("Feedback Settings")]
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.3f;

    public int currentHealth;
    private Coroutine _flashCoroutine;
    private Color _originalColor;

    private SpriteRenderer _sr;
    private Light2D[] _buildingLights2D;
    private float[] _buildingLightBaseAlphas;
    private bool[] _buildingLightUseDayNightRule;
    private float _buildingLightTargetMultiplier = 1f;
    private Coroutine _waitForDayNightCoroutine;
    private Coroutine _buildingLightTransitionCoroutine;
    private const float BuildingLightAlphaLerpSpeed = 2f;
    private bool _isNoiseBuildingRegistered;
    private bool _isBuildingLightEventsRegistered;

    public int CurrentHealth {
        get {
            return currentHealth;
        }
    }

    protected virtual void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr == null) {
            _sr = GetComponent<SpriteRenderer>();
        }
        _originalColor = _sr.color;
        _attackAlertWait = CoroutineCache.GetWaitForSeconds(0.5f);
        _flashWait = CoroutineCache.GetWaitForSeconds(flashDuration);
    }

    protected virtual void OnEnable()
    {
        currentHealth = maxHealth;
        TargetManager.Instance?.RegisterTarget(this);
        
        if (this is UnitBase == false && ShouldRegisterBuildingSystems())
        {
            if (NoiseManager.Instance != null)
            {
                NoiseManager.Instance.RegisterBuilding(this);
                _isNoiseBuildingRegistered = true;
            }

            DayNightCycleManager.OnNightStarted += OnNightStarted;
            DayNightCycleManager.OnDayStarted += OnDayStarted;
            _isBuildingLightEventsRegistered = true;
            ApplyBuildingLightState(true);

            if (DayNightCycleManager.Instance == null && gameObject.activeInHierarchy)
            {
                _waitForDayNightCoroutine = StartCoroutine(WaitForDayNightAndApplyBuildingLights());
            }
        }
    }

    protected virtual void OnDisable()
    {
        TargetManager.Instance?.UnregisterTarget(this);
        
        if (_isNoiseBuildingRegistered && NoiseManager.Instance != null)
        {
            NoiseManager.Instance.UnregisterBuilding(this);
            _isNoiseBuildingRegistered = false;
        }

        if (_isBuildingLightEventsRegistered)
        {
            DayNightCycleManager.OnNightStarted -= OnNightStarted;
            DayNightCycleManager.OnDayStarted -= OnDayStarted;
            _isBuildingLightEventsRegistered = false;
        }

        if (_waitForDayNightCoroutine != null)
        {
            StopCoroutine(_waitForDayNightCoroutine);
            _waitForDayNightCoroutine = null;
        }

        if (_buildingLightTransitionCoroutine != null)
        {
            StopCoroutine(_buildingLightTransitionCoroutine);
            _buildingLightTransitionCoroutine = null;
        }
    }

    public int MaxHealth {
        get {
            return maxHealth;
        }
    }

    public virtual void TakeDamage(int damage)
    {
        TakeDamage(damage, DamageContext.Default);
    }

    public virtual void TakeDamage(int damage, DamageContext context)
    {
        currentHealth -= damage;
        HitAudioRouter.PlayHit(this, context);
        OnDamageTaken(damage);
        OnAnyDamageTaken?.Invoke(this);
        OnHealthChanged();

        if (_flashCoroutine != null) {
            StopCoroutine(_flashCoroutine);
        }
        _flashCoroutine = StartCoroutine(FlashEffect());
        
        if (currentHealth > 0)
        {
            RegisterAttackAlert();
        }

        if (currentHealth <= 0) {
            UnregisterAttackAlert();
            Destroy(gameObject);
        }
    }
    
    private bool _isAttackAlertRegistered;
    private float _lastDamageTime;
    private Coroutine _attackAlertTimerCoroutine;
    private const float AttackAlertTimeout = 5f;
    private WaitForSeconds _attackAlertWait;
    private WaitForSeconds _flashWait;
    
    private void RegisterAttackAlert()
    {
        var alertManager = FindFirstObjectByType<GameAlertUIManager>();
        if (alertManager == null) return;
        
        if (this is UnitBase unitBase)
        {
            if (unitBase.unitType != UnitBase.UnitType.Ally)
            {
                return;
            }
        }
        
        _lastDamageTime = Time.time;
        
        if (!_isAttackAlertRegistered)
        {
            if (this is UnitBase)
            {
                alertManager.RegisterAlert(GameAlertType.UnitUnderAttack, this);
                _isAttackAlertRegistered = true;
            }
            else
            {
                alertManager.RegisterAlert(GameAlertType.BuildingUnderAttack, this);
                _isAttackAlertRegistered = true;
            }
        }
        
        if (_attackAlertTimerCoroutine != null)
        {
            StopCoroutine(_attackAlertTimerCoroutine);
        }
        _attackAlertTimerCoroutine = StartCoroutine(AttackAlertTimerCoroutine());
    }
    
    private IEnumerator AttackAlertTimerCoroutine()
    {
        while (_isAttackAlertRegistered)
        {
            yield return _attackAlertWait;
            
            if (Time.time - _lastDamageTime >= AttackAlertTimeout)
            {
                UnregisterAttackAlert();
                yield break;
            }
        }
    }
    
    private void UnregisterAttackAlert()
    {
        var alertManager = FindFirstObjectByType<GameAlertUIManager>();
        if (alertManager == null || !_isAttackAlertRegistered) return;
        
        if (_attackAlertTimerCoroutine != null)
        {
            StopCoroutine(_attackAlertTimerCoroutine);
            _attackAlertTimerCoroutine = null;
        }
        
        if (this is UnitBase)
        {
            alertManager.UnregisterAlert(GameAlertType.UnitUnderAttack, this);
        }
        else
        {
            alertManager.UnregisterAlert(GameAlertType.BuildingUnderAttack, this);
        }
        
        _isAttackAlertRegistered = false;
    }
    
    protected virtual void OnDestroy()
    {
        UnregisterAttackAlert();
        DayNightCycleManager.OnNightStarted -= OnNightStarted;
        DayNightCycleManager.OnDayStarted -= OnDayStarted;
    }

    public static event Action<Damageable> OnAnyDamageTaken;

    public event Action HealthChanged;

    private void OnDamageTaken(int damage)
    {
    }

    protected virtual void OnHealthChanged()
    {
        HealthChanged?.Invoke();
    }

    public void SetMaxHealth(int newMaxHealth)
    {
        float healthRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 1f;
        maxHealth = newMaxHealth;
        currentHealth = Mathf.RoundToInt(maxHealth * healthRatio);
        HealthChanged?.Invoke();
    }

    public void Heal(int amount)
    {
        if (currentHealth >= maxHealth) {
            Debug.Log($"{gameObject.name} : Already at max health");
            return;
        }

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"{gameObject.name} Healed : {currentHealth}/{maxHealth}");
        OnHealthChanged();
    }

    public void RestoreToFullHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged();
    }

    public void SetPersistentTint(Color color)
    {
        if (_sr == null) {
            _sr = GetComponentInChildren<SpriteRenderer>();
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        }
        if (_sr != null) {
            _originalColor = color;
            _sr.color = color;
        }
    }

    private IEnumerator FlashEffect()
    {
        if (_sr == null) yield break;

        _sr.color = flashColor;

        yield return _flashWait;

        _sr.color = _originalColor;
    }

    private IEnumerator WaitForDayNightAndApplyBuildingLights()
    {
        while (DayNightCycleManager.Instance == null)
        {
            yield return null;
        }

        _waitForDayNightCoroutine = null;
        ApplyBuildingLightState(true);
    }

    private void ApplyBuildingLightState(bool immediate)
    {
        if (this is UnitBase)
        {
            return;
        }

        if (DayNightCycleManager.Instance == null)
        {
            return;
        }

        if (_buildingLights2D == null)
        {
            _buildingLights2D = GetComponentsInChildren<Light2D>(true);
            if (_buildingLights2D != null && _buildingLights2D.Length > 0)
            {
                _buildingLightBaseAlphas = new float[_buildingLights2D.Length];
                _buildingLightUseDayNightRule = new bool[_buildingLights2D.Length];
                for (int i = 0; i < _buildingLights2D.Length; i++)
                {
                    Light2D light2D = _buildingLights2D[i];
                    _buildingLightBaseAlphas[i] = light2D != null ? light2D.color.a : 0f;
                    _buildingLightUseDayNightRule[i] = light2D != null && light2D.lightType != Light2D.LightType.Sprite;
                }
            }
        }

        if (_buildingLights2D == null || _buildingLights2D.Length == 0)
        {
            return;
        }

        _buildingLightTargetMultiplier = DayNightCycleManager.Instance.IsDay() ? 0f : 1f;

        if (_buildingLightTransitionCoroutine != null)
        {
            StopCoroutine(_buildingLightTransitionCoroutine);
            _buildingLightTransitionCoroutine = null;
        }

        if (immediate)
        {
            for (int i = 0; i < _buildingLights2D.Length; i++)
            {
                Light2D light2D = _buildingLights2D[i];
                if (light2D == null) continue;

                Color color = light2D.color;
                float targetAlpha = _buildingLightUseDayNightRule != null && i < _buildingLightUseDayNightRule.Length && _buildingLightUseDayNightRule[i]
                    ? _buildingLightBaseAlphas[i] * _buildingLightTargetMultiplier
                    : _buildingLightBaseAlphas[i];
                light2D.color = new Color(color.r, color.g, color.b, targetAlpha);
            }
            return;
        }

        _buildingLightTransitionCoroutine = StartCoroutine(SmoothUpdateBuildingLights());
    }

    private IEnumerator SmoothUpdateBuildingLights()
    {
        while (true)
        {
            bool hasPendingUpdate = false;
            for (int i = 0; i < _buildingLights2D.Length; i++)
            {
                Light2D light2D = _buildingLights2D[i];
                if (light2D == null) continue;

                Color color = light2D.color;
                float targetAlpha = _buildingLightUseDayNightRule != null && i < _buildingLightUseDayNightRule.Length && _buildingLightUseDayNightRule[i]
                    ? _buildingLightBaseAlphas[i] * _buildingLightTargetMultiplier
                    : _buildingLightBaseAlphas[i];
                float nextAlpha = Mathf.Lerp(color.a, targetAlpha, BuildingLightAlphaLerpSpeed * Time.deltaTime);
                light2D.color = new Color(color.r, color.g, color.b, nextAlpha);

                if (Mathf.Abs(nextAlpha - targetAlpha) > 0.01f)
                {
                    hasPendingUpdate = true;
                }
            }

            if (!hasPendingUpdate)
            {
                break;
            }

            yield return null;
        }

        _buildingLightTransitionCoroutine = null;
    }

    private void OnNightStarted()
    {
        ApplyBuildingLightState(false);
    }

    private void OnDayStarted()
    {
        ApplyBuildingLightState(false);
    }

    private bool ShouldRegisterBuildingSystems()
    {
        if (BuildingManager.Instance == null) return true;
        return BuildingManager.IsBuildingProperlyPlaced(transform);
    }
}
