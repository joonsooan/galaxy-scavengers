using UnityEngine;
using System.Collections;

public class Turret : Damageable, IAetherConsumer
{
    [Header("Turret Stats")]
    [SerializeField] private float attackRange = 10f;
    [SerializeField] private float fireInterval = 1f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private GameObject bulletPrefab;
    [Header("Aether Consumption")]
    [SerializeField] private int aetherConsumptionPerSecond = 1;

    private Transform _target;
    private Coroutine _attackCoroutine;
    private WaitForSeconds _findTargetWait;
    private Vector3 _bulletSpawnPosition;
    private bool _isOperational = true;
    private AetherConsumptionManager _aetherConsumptionManager;
    
    public int AetherConsumptionPerSecond => aetherConsumptionPerSecond;
    public bool IsOperational => _isOperational;
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + new Vector3(0.5f, 0.5f, 0f), attackRange);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        if (!BuildingManager.IsBuildingProperlyPlaced(transform))
        {
            return;
        }
        
        FindAndCacheAetherManager();
        if (_aetherConsumptionManager != null)
        {
            _aetherConsumptionManager.RegisterConsumer(this);
        }
        
        _bulletSpawnPosition = transform.position + new Vector3(0.5f, 0.5f, 0f);
        _findTargetWait = new WaitForSeconds(0.2f);
        StartCoroutine(UpdateTargetCoroutine());
    }

    protected override void OnDisable()
    {
        if (_aetherConsumptionManager != null)
        {
            _aetherConsumptionManager.UnregisterConsumer(this);
        }
        
        base.OnDisable();
        
        StopAllCoroutines();
        _attackCoroutine = null;
    }
    
    private void FindAndCacheAetherManager()
    {
        if (_aetherConsumptionManager == null)
        {
            _aetherConsumptionManager = FindFirstObjectByType<AetherConsumptionManager>();
        }
    }
    
    public void OnAetherUnavailable()
    {
        if (_isOperational)
        {
            _isOperational = false;
            StopAttacking();
        }
    }
    
    public void OnAetherAvailable()
    {
        if (!_isOperational)
        {
            _isOperational = true;
        }
    }

    private void Update()
    {
        if (!_isOperational)
        {
            StopAttacking();
            return;
        }
        
        if (!IsTargetValid())
        {
            _target = null;
            StopAttacking();
            return;
        }

        StartAttacking();
    }

    private bool IsTargetValid()
    {
        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            return false;
        }

        return Vector2.Distance(transform.position, _target.position) <= attackRange;
    }

    private IEnumerator UpdateTargetCoroutine()
    {
        while (true)
        {
            if (_target == null)
            {
                FindClosestEnemy();
            }
            yield return _findTargetWait;
        }
    }

    private void FindClosestEnemy()
    {
        var enemies = UnitManager.Instance.EnemyUnits;
        Transform closestTarget = null;
        float minDistance = float.MaxValue;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < minDistance && distance <= attackRange)
            {
                minDistance = distance;
                closestTarget = enemy.transform;
            }
        }
        _target = closestTarget;
    }

    private void StartAttacking()
    {
        if (_attackCoroutine == null)
        {
            _attackCoroutine = StartCoroutine(AttackCoroutine());
        }
    }

    private void StopAttacking()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }
    }

    private IEnumerator AttackCoroutine()
    {
        while (true)
        {
            Shoot();
            yield return new WaitForSeconds(fireInterval);
        }
    }

    private void Shoot()
    {
        if (_target == null)
        {
            Debug.Log("Target is null");
            return;
        }
        
        GameObject bulletObj = ObjectPooler.Instance.SpawnFromPool("TurretBullet", _bulletSpawnPosition, Quaternion.identity);

        if (bulletObj != null && bulletObj.TryGetComponent<Turret_Bullet>(out var bulletScript))
        {
            bulletScript.Initialize(attackDamage, _target);
        }
    }
}