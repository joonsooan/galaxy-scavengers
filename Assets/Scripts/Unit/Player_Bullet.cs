using UnityEngine;

public class Player_Bullet : MonoBehaviour
{
    public float speed = 10f;
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

    public void Initialize(int damage, Vector2 direction)
    {
        _damage = damage;
        _direction = direction.normalized;
        
        if (_direction.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
        
        if (_rb != null)
        {
            _rb.linearVelocity = _direction * speed;
        }
    }

    void OnEnable()
    {
        Invoke(nameof(Deactivate), lifeTime);
    }

    void Update()
    {
        if (_grid != null)
        {
            Vector3Int currentCell = _grid.WorldToCell(transform.position);
            
            if (BuildingManager.Instance != null)
            {
                if (BuildingManager.Instance.IsTerrainCell(currentCell) || 
                    BuildingManager.Instance.IsResourceTile(currentCell))
                {
                    Deactivate();
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (_rb == null) return;

        if (_rb.linearVelocity.sqrMagnitude < 0.01f)
        {
            _rb.linearVelocity = _direction * speed;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        UnitBase enemy = other.GetComponent<UnitBase>();
        if (enemy != null && enemy.unitType == UnitBase.UnitType.Enemy)
        {
            enemy.TakeDamage(_damage);
            Deactivate();
            return;
        }

        if (other is not BoxCollider2D) return;

        Damageable building = other.GetComponentInParent<Damageable>();
        if (building != null && building.GetComponent<UnitBase>() == null)
        {
            Deactivate();
        }
    }

    private void Deactivate()
    {
        CancelInvoke();
        
        _direction = Vector2.zero;
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }
        gameObject.SetActive(false);
    }
}
