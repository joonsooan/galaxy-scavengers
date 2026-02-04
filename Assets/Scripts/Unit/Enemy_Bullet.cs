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
        UnitBase player = other.GetComponent<UnitBase>();
        if (player != null && player.unitType == UnitBase.UnitType.Ally)
        {
            player.TakeDamage(_damage);
            Deactivate();
            return;
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