using UnityEngine;

public class Turret_Bullet : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 3f;

    private int _damage;
    private Transform _target;
    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }
    
    public void Initialize(int damage, Transform target)
    {
        _damage = damage;
        _target = target;
    }

    void OnEnable()
    {
        Invoke(nameof(Deactivate), lifeTime);
    }
    
    void FixedUpdate()
    {
        if (_target == null || !_target.gameObject.activeSelf)
        {
            Deactivate();
            return;
        }

        Vector2 direction = (_target.position - transform.position).normalized;
        _rb.linearVelocity = direction * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.transform == _target)
        {
            var enemyHealth = other.GetComponent<UnitBase>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(_damage, DamageContext.From(DamageAttackType.Projectile, DamageAttackerFaction.Ally, gameObject));
            }
            
            Deactivate();
        }
    }

    private void Deactivate()
    {
        CancelInvoke();
        
        _target = null;
        _rb.linearVelocity = Vector2.zero;
        gameObject.SetActive(false);
    }
}