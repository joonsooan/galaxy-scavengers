using System.Collections;
using UnityEngine;

public abstract class UnitBase : MonoBehaviour
{
    public enum UnitState
    {
        Idle,
        Moving,
        Attacking,
        Mining,
        Constructing,
        ReturningToStorage,
        Unloading
    }

    public enum UnitType
    {
        Ally,
        Enemy
    }

    [Header("Unit Stats")]
    public float maxHealth;
    public float currentHealth;
    public UnitType unitType;

    [Header("Feedback Settings")]
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.3f;

    public UnitState currentState;
    private Coroutine _flashCoroutine;
    private Color _originalColor;
    private SpriteRenderer _sr;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }

    protected virtual void Start()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
    }

    protected virtual void OnEnable()
    {
        if (UnitManager.Instance != null) {
            UnitManager.Instance.AddUnit(this);
        }

        if (unitType == UnitType.Ally && GetComponent<VisionProvider>() == null) {
            VisionProvider visionProvider = gameObject.AddComponent<VisionProvider>();
            visionProvider.SetVisionRange(3f);
        }
    }

    protected virtual void OnDisable()
    {
        if (UnitManager.Instance != null) {
            UnitManager.Instance.RemoveUnit(this);
        }
    }

    public virtual void TakeDamage(float damage)
    {
        currentHealth -= damage;

        if (_flashCoroutine != null) {
            StopCoroutine(_flashCoroutine);
        }
        _flashCoroutine = StartCoroutine(FlashEffect());

        if (currentHealth <= 0) {
            Destroy(gameObject);
        }
    }

    public void Heal(float amount)
    {
        if (currentHealth >= maxHealth) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    private IEnumerator FlashEffect()
    {
        if (_sr == null) yield break;

        _sr.material.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        _sr.material.color = _originalColor;
    }
}
