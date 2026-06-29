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
    private GameAlertUIManager _gameAlertUIManager;
    private Light2D[] _buildingLights2D;
    private float[] _buildingLightBaseAlphas;
    private float[] _buildingLightBaseIntensities;
    private bool[] _buildingLightUseDayNightRule;
    private bool _hasBuildingLights;
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
        _gameAlertUIManager = FindFirstObjectByType<GameAlertUIManager>();
    }

    protected virtual void OnEnable()
    {
        currentHealth = maxHealth;
        if (_sr != null) _sr.color = _originalColor;
        if (TargetManager.Instance != null)
        {
            TargetManager.Instance.RegisterTarget(this);
        }
        
        if (this is UnitBase == false && ShouldRegisterBuildingSystems())
        {
            if (NoiseManager.Instance != null)
            {
                NoiseManager.Instance.RegisterBuilding(this);
                _isNoiseBuildingRegistered = true;
            }

            InitializeBuildingLights();
            DayNightCycleManager.OnTimeUpdated += OnTimeUpdated;
            _isBuildingLightEventsRegistered = true;
            if (DayNightCycleManager.Instance != null)
            {
                OnTimeUpdated(DayNightCycleManager.Instance.GetTime());
            }
        }
    }

    protected virtual void OnDisable()
    {
        _flashCoroutine = null;
        if (TargetManager.Instance != null)
        {
            TargetManager.Instance.UnregisterTarget(this);
        }
        
        if (_isNoiseBuildingRegistered && NoiseManager.Instance != null)
        {
            NoiseManager.Instance.UnregisterBuilding(this);
            _isNoiseBuildingRegistered = false;
        }

        if (_isBuildingLightEventsRegistered)
        {
            DayNightCycleManager.OnTimeUpdated -= OnTimeUpdated;
            _isBuildingLightEventsRegistered = false;
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
        if (currentHealth <= 0) return;

        currentHealth -= damage;
        HitAudioRouter.PlayHit(this, context);
        OnAnyDamageTaken?.Invoke(this);
        OnHealthChanged();

        if (currentHealth > 0)
        {
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashEffect());
            RegisterAttackAlert();
        }
        else
        {
            UnregisterAttackAlert();
            Die();
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
        var alertManager = _gameAlertUIManager;
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
        var alertManager = _gameAlertUIManager;
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
    
    protected virtual void Die()
    {
        Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        UnregisterAttackAlert();
    }

    public static event Action<Damageable> OnAnyDamageTaken;

    public event Action HealthChanged;

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

    private void InitializeBuildingLights()
    {
        _buildingLights2D = GetComponentsInChildren<Light2D>(true);
        if (_buildingLights2D != null && _buildingLights2D.Length > 0)
        {
            _buildingLightBaseAlphas = new float[_buildingLights2D.Length];
            _buildingLightBaseIntensities = new float[_buildingLights2D.Length];
            _buildingLightUseDayNightRule = new bool[_buildingLights2D.Length];
            for (int i = 0; i < _buildingLights2D.Length; i++)
            {
                Light2D light2D = _buildingLights2D[i];
                _buildingLightBaseAlphas[i] = light2D != null ? light2D.color.a : 0f;
                _buildingLightBaseIntensities[i] = light2D != null ? light2D.intensity : 0f;
                _buildingLightUseDayNightRule[i] = light2D != null && light2D.lightType != Light2D.LightType.Sprite;
            }
            _hasBuildingLights = true;
        }
    }

    private void OnTimeUpdated(float time)
    {
        if (_hasBuildingLights && DayNightCycleManager.Instance != null)
        {
            float multiplier = DayNightCycleManager.Instance.GetGlobalLightMultiplier();
            for (int i = 0; i < _buildingLights2D.Length; i++)
            {
                Light2D light2D = _buildingLights2D[i];
                if (light2D == null) continue;

                float targetMultiplier = _buildingLightUseDayNightRule[i] ? multiplier : 1f;
                light2D.intensity = _buildingLightBaseIntensities[i] * targetMultiplier;

                float targetAlpha = _buildingLightUseDayNightRule[i]
                    ? _buildingLightBaseAlphas[i] * multiplier
                    : _buildingLightBaseAlphas[i];

                Color color = light2D.color;
                light2D.color = new Color(color.r, color.g, color.b, targetAlpha);
            }
        }
    }

    private bool ShouldRegisterBuildingSystems()
    {
        if (BuildingManager.Instance == null) return true;
        return BuildingManager.IsBuildingProperlyPlaced(transform);
    }
}
