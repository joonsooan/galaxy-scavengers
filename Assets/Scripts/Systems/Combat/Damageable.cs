using System;
using System.Collections;
using UnityEngine;

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
    }

    protected virtual void OnEnable()
    {
        currentHealth = maxHealth;
        TargetManager.Instance?.RegisterTarget(this);
    }

    protected virtual void OnDisable()
    {
        TargetManager.Instance?.UnregisterTarget(this);
    }

    public int MaxHealth {
        get {
            return maxHealth;
        }
    }

    public virtual void TakeDamage(int damage)
    {
        currentHealth -= damage;
        OnDamageTaken(damage);
        OnAnyDamageTaken?.Invoke(this);

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
    
    private void RegisterAttackAlert()
    {
        if (GameAlertUIManager.Instance == null || _isAttackAlertRegistered) return;
        
        if (this is UnitBase)
        {
            GameAlertUIManager.Instance.RegisterAlert(GameAlertType.UnitUnderAttack);
            _isAttackAlertRegistered = true;
        }
        else
        {
            GameAlertUIManager.Instance.RegisterAlert(GameAlertType.BuildingUnderAttack);
            _isAttackAlertRegistered = true;
        }
    }
    
    private void UnregisterAttackAlert()
    {
        if (GameAlertUIManager.Instance == null || !_isAttackAlertRegistered) return;
        
        if (this is UnitBase)
        {
            GameAlertUIManager.Instance.UnregisterAlert(GameAlertType.UnitUnderAttack);
        }
        else
        {
            GameAlertUIManager.Instance.UnregisterAlert(GameAlertType.BuildingUnderAttack);
        }
        
        _isAttackAlertRegistered = false;
    }
    
    protected virtual void OnDestroy()
    {
        UnregisterAttackAlert();
    }

    public static event Action<Damageable> OnAnyDamageTaken;

    private void OnDamageTaken(int damage)
    {
    }

    public void SetMaxHealth(int newMaxHealth)
    {
        float healthRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 1f;
        maxHealth = newMaxHealth;
        currentHealth = Mathf.RoundToInt(maxHealth * healthRatio);
    }

    public void Heal(int amount)
    {
        if (currentHealth >= maxHealth) {
            Debug.Log($"{gameObject.name} : Already at max health");
            return;
        }

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"{gameObject.name} Healed : {currentHealth}/{maxHealth}");
    }

    private IEnumerator FlashEffect()
    {
        if (_sr == null) yield break;

        _sr.color = flashColor;

        yield return new WaitForSeconds(flashDuration);

        _sr.color = _originalColor;
    }
}
