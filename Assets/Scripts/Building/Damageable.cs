using System.Collections;
using UnityEngine;

public abstract class Damageable : MonoBehaviour, ICombo
{
    [Header("Health Settings")]
    [SerializeField] protected int maxHealth = 100;
    
    [Header("Feedback Settings")]
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.3f;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    
    protected int currentHealth;
    
    private SpriteRenderer _sr;
    private Color _originalColor;
    private Coroutine _flashCoroutine;

    protected virtual void Awake()
    {
        _sr = GetComponent<SpriteRenderer>(); 
        if (_sr != null)
        {
            _originalColor = _sr.material.color;
        }
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
    
    private IEnumerator FlashEffect()
    {
        if (_sr == null) yield break;

        _sr.material.color = flashColor;

        yield return new WaitForSeconds(flashDuration);

        _sr.material.color = _originalColor;
    }
}