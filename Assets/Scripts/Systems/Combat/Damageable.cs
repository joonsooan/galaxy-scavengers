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

    public static event Action<Damageable> OnAnyDamageTaken;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    public int currentHealth;

    protected virtual void OnDamageTaken(int damage)
    {
    }
    
    public virtual void SetMaxHealth(int newMaxHealth)
    {
        float healthRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 1f;
        maxHealth = newMaxHealth;
        currentHealth = Mathf.RoundToInt(maxHealth * healthRatio);
    }
    
    private SpriteRenderer _sr;
    private Color _originalColor;
    private Coroutine _flashCoroutine;

    protected virtual void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr == null)
        {
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

    public virtual void TakeDamage(int damage)
    {
        currentHealth -= damage;
        OnDamageTaken(damage);
        OnAnyDamageTaken?.Invoke(this);
        
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
        }
        _flashCoroutine = StartCoroutine(FlashEffect());
        
        if (currentHealth <= 0)
        {
            Destroy(gameObject);
        }
    }
    
    public void Heal(int amount)
    {
        if (currentHealth >= maxHealth)
        {
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