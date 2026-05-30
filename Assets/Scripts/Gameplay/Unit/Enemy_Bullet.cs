using UnityEngine;

public class Enemy_Bullet : MonoBehaviour
{
    public float speed;
    public float lifeTime = 3f;
    
    private int _damage;
    private Vector2 _direction;
    private Rigidbody2D _rb;
    private Grid _grid;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        if (BuildingManager.Instance != null)
        {
            _grid = BuildingManager.Instance.grid;
        }
    }

    public void Initialize(int damage, Vector2 direction, float speed)
    {
        _damage = damage;
        _direction = direction.normalized;
        this.speed = speed;

        if (_rb != null)
        {
            _rb.linearVelocity = _direction * this.speed;
        }

        Invoke(nameof(Deactivate), lifeTime);
    }

    private void Update()
    {
        
        if (_grid != null && BuildingManager.Instance != null)
        {
            Vector3Int currentCell = _grid.WorldToCell(transform.position);
            
            if (BuildingManager.Instance.IsTerrainCell(currentCell) || 
                BuildingManager.Instance.IsResourceTile(currentCell))
            {
                Deactivate();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other is not BoxCollider2D) return;

        Damageable damageable = other.GetComponent<Damageable>();
        if (damageable != null)
        {
            if (damageable is Platform) return;
            bool shouldDamage = false;
            
            UnitBase unit = damageable as UnitBase;
            if (unit != null)
            {
                if (unit.unitType == UnitBase.UnitType.Ally)
                {
                    shouldDamage = true;
                }
            }
            else
            {
                shouldDamage = true;
            }
            
            if (shouldDamage)
            {
                damageable.TakeDamage(_damage, DamageContext.From(DamageAttackType.Projectile, DamageAttackerFaction.Enemy, gameObject));
                Deactivate();
            }
        }
    }

    private void OnDisable()
    {
        CancelInvoke();
    }

    private void Deactivate()
    {
        CancelInvoke();
        
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }
        gameObject.SetActive(false);
    }
}