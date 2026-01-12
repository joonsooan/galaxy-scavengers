using System.Collections;
using UnityEngine;

public abstract class EnemyUnitBase : UnitBase
{
    protected enum AIState
    {
        Idle,
        Warning,
        Attack
    }

    [Header("Zones")]
    [SerializeField] protected float warningStatePersist = 5f;
    protected float homeRadius;
    protected float territoryRadius;

    [Header("Combat")]
    [SerializeField] protected int attackDamage = 10;
    [SerializeField] protected float attackRange = 1.5f;
    [SerializeField] protected float attackSpeed = 1f;

    [Header("Roaming")]
    public float minRoamInterval = 2.0f;
    public float maxRoamInterval = 3.0f;
    private float _currentRoamInterval;

    [Header("References")]
    [SerializeField] protected UnitMovement unitMovement;
    private CircleCollider2D _attackRangeCollider;

    protected Vector3 _spawnPosition;
    protected AIState _aiState;
    protected Damageable _targetDamageable;
    protected UnitBase _targetUnit;
    
    private float _roamTimer;
    private bool _lastActionWasMove;
    protected float _warningTimer;
    
    private Coroutine _attackCoroutine;

    protected override void Awake()
    {
        base.Awake();
        if (unitMovement == null)
        {
            unitMovement = GetComponent<UnitMovement>();
        }
        
        _attackRangeCollider = GetComponent<CircleCollider2D>();
        if (_attackRangeCollider == null)
        {
            _attackRangeCollider = gameObject.AddComponent<CircleCollider2D>();
        }
        _attackRangeCollider.radius = attackRange;
        _attackRangeCollider.isTrigger = true;
        
        _spawnPosition = Vector3.zero;
        _aiState = AIState.Idle;
    }
    
    private void Start()
    {
        SetNewRoamInterval();
    }
    
    public void SetTerritoryCenter(Vector3 territoryCenter, float homeRadius, float territoryRadius)
    {
        _spawnPosition = territoryCenter;
        this.homeRadius = homeRadius;
        this.territoryRadius = territoryRadius;
    }

    private void Update()
    {
        UpdateStateLogic();
    }

    private void UpdateStateLogic()
    {
        switch (_aiState)
        {
            case AIState.Idle:
                HandleIdle();
                CheckForTerritoryEntry();
                break;
            
            case AIState.Warning:
                HandleWarning();
                break;
            
            case AIState.Attack:
                HandleAttack();
                break;
        }
    }

    private void HandleIdle()
    {
        _roamTimer += Time.deltaTime;
        if (_roamTimer < _currentRoamInterval) return;
        _roamTimer = 0f;
        SetNewRoamInterval();
        
        if (!_lastActionWasMove)
        {
            ChooseRoamDestination();
            _lastActionWasMove = true;
        }
        else
        {
            if (Random.value < 0.5f)
            {
                if (unitMovement != null)
                {
                    unitMovement.StopMovement();
                }
                _lastActionWasMove = false;
            }
            else
            {
                ChooseRoamDestination();
                _lastActionWasMove = true;
            }
        }
    }
    
    private void SetNewRoamInterval()
    {
        _currentRoamInterval = Random.Range(minRoamInterval, maxRoamInterval);
    }

    private void CheckForTerritoryEntry()
    {
        Damageable targetBuilding = FindClosestDamageableInTerritory();
        UnitBase targetUnit = FindClosestAllyInTerritory();
        
        if (targetBuilding != null)
        {
            _targetDamageable = targetBuilding;
            _targetUnit = null;
            EnterWarningState();
            return;
        }
        
        if (targetUnit != null)
        {
            _targetDamageable = null;
            _targetUnit = targetUnit;
            EnterWarningState();
            return;
        }
    }

    protected void EnterWarningState()
    {
        if (_aiState == AIState.Attack)
        {
            if (_attackCoroutine != null)
            {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }
        }
        
        _aiState = AIState.Warning;
        _warningTimer = 0f;
        
        if (unitMovement != null)
        {
            unitMovement.StopMovement();
        }
    }

    private void HandleWarning()
    {
        _warningTimer += Time.deltaTime;
        
        Damageable currentBuilding = _targetDamageable;
        UnitBase currentUnit = _targetUnit;
        
        if (currentBuilding == null && currentUnit == null)
        {
            ReturnToIdle();
            return;
        }
        
        Vector3 targetPos = currentBuilding != null ? currentBuilding.transform.position : currentUnit.transform.position;
        float distanceFromSpawn = Vector3.Distance(_spawnPosition, targetPos);
        
        if (distanceFromSpawn > territoryRadius)
        {
            ReturnToIdle();
            return;
        }
        
        float distanceToTarget = Vector3.Distance(transform.position, targetPos);
        
        if (distanceToTarget <= attackRange)
        {
            EnterAttackState();
            return;
        }
        
        if (_warningTimer >= warningStatePersist)
        {
            if (distanceToTarget > attackRange)
            {
                ReturnToIdle();
                return;
            }
        }
        
        MoveToCurrentTarget();
    }

    private void HandleAttack()
    {
        Damageable currentBuilding = _targetDamageable;
        UnitBase currentUnit = _targetUnit;
        
        if (currentBuilding == null && currentUnit == null)
        {
            EnterWarningState();
            return;
        }
        
        Vector3 targetPos = currentBuilding != null ? currentBuilding.transform.position : currentUnit.transform.position;
        float distanceToTarget = Vector3.Distance(transform.position, targetPos);
        
        if (distanceToTarget > attackRange * 1.5f)
        {
            MoveToCurrentTarget();
        }
        else if (distanceToTarget > attackRange)
        {
            MoveToCurrentTarget();
        }
        else
        {
            if (unitMovement != null)
            {
                unitMovement.StopMovement();
            }
            
            if (currentBuilding != null)
            {
                if (currentBuilding.CurrentHealth <= 0)
                {
                    _targetDamageable = null;
                    EnterWarningState();
                    return;
                }
            }
            else if (currentUnit != null)
            {
                if (currentUnit.currentHealth <= 0)
                {
                    _targetUnit = null;
                    EnterWarningState();
                    return;
                }
            }
            
            if (_attackCoroutine == null)
            {
                _attackCoroutine = StartCoroutine(AttackCoroutine());
            }
        }
    }

    protected void EnterAttackState()
    {
        if (_aiState == AIState.Attack) return;
        
        _aiState = AIState.Attack;
        _warningTimer = 0f;
        
        if (unitMovement != null)
        {
            unitMovement.StopMovement();
        }
        
        if (_attackCoroutine == null)
        {
            _attackCoroutine = StartCoroutine(AttackCoroutine());
        }
    }

    protected void ReturnToIdle()
    {
        _aiState = AIState.Idle;
        _warningTimer = 0f;
        _targetDamageable = null;
        _targetUnit = null;
        
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }
        
        if (unitMovement != null)
        {
            unitMovement.StopMovement();
        }
    }

    private void ChooseRoamDestination()
    {
        if (unitMovement == null) return;
        
        Vector3 destination = FindWalkableRoamDestination();
        if (destination != Vector3.zero)
        {
            unitMovement.SetNewTarget(destination, unitMovement.waypointTolerance);
        }
    }
    
    private Vector3 FindWalkableRoamDestination()
    {
        Grid grid = BuildingManager.Instance.grid;
        Vector3Int spawnCell = grid.WorldToCell(_spawnPosition);
        
        int maxAttempts = 50;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomRadius = Random.Range(0f, homeRadius);
            Vector3 candidatePos = _spawnPosition + new Vector3(randomDir.x, randomDir.y, 0f) * randomRadius;
            Vector3Int candidateCell = grid.WorldToCell(candidatePos);
            
            if (IsCellWalkable(candidateCell))
            {
                float distanceFromSpawn = Vector3.Distance(_spawnPosition, candidatePos);
                if (distanceFromSpawn <= homeRadius)
                {
                    return grid.GetCellCenterWorld(candidateCell);
                }
            }
        }
        
        for (int radius = 1; radius <= Mathf.CeilToInt(homeRadius); radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                    
                    Vector3Int testCell = spawnCell + new Vector3Int(x, y, 0);
                    float distanceFromSpawn = Vector3.Distance(_spawnPosition, grid.GetCellCenterWorld(testCell));
                    
                    if (distanceFromSpawn <= homeRadius && IsCellWalkable(testCell))
                    {
                        return grid.GetCellCenterWorld(testCell);
                    }
                }
            }
        }
        
        return _spawnPosition;
    }
    
    private bool IsCellWalkable(Vector3Int cell)
    {
        if (BuildingManager.Instance == null) return false;
        
        if (BuildingManager.Instance.IsTerrainCell(cell)) return false;
        if (BuildingManager.Instance.IsBuildingTile(cell)) return false;
        if (BuildingManager.Instance.IsResourceTile(cell)) return false;
        if (BuildingManager.Instance.GetPieceAt(cell) != null) return false;
        
        return true;
    }

    protected void MoveToCurrentTarget()
    {
        if (unitMovement == null) return;
        
        Vector3 targetPos;
        
        if (_targetDamageable != null)
        {
            targetPos = _targetDamageable.transform.position;
        }
        else if (_targetUnit != null)
        {
            targetPos = _targetUnit.transform.position;
        }
        else
        {
            return;
        }
        unitMovement.SetNewTarget(targetPos, unitMovement.waypointTolerance);
    }

    private Damageable FindClosestDamageableInTerritory()
    {
        if (TargetManager.Instance == null || TargetManager.Instance.AllTargets.Count == 0)
        {
            return null;
        }
        
        float bestDistance = float.MaxValue;
        Damageable bestTarget = null;
        
        foreach (Damageable target in TargetManager.Instance.AllTargets)
        {
            if (target == null) continue;
            if (target.GetComponent<UnitBase>() != null) continue;
            
            float distFromSpawn = Vector3.Distance(_spawnPosition, target.transform.position);
            if (distFromSpawn <= territoryRadius && distFromSpawn < bestDistance)
            {
                bestDistance = distFromSpawn;
                bestTarget = target;
            }
        }
        return bestTarget;
    }

    private UnitBase FindClosestAllyInTerritory()
    {
        if (UnitManager.Instance == null || UnitManager.Instance.AllyUnits == null || UnitManager.Instance.AllyUnits.Count == 0)
        {
            return null;
        }
        
        float bestDistance = float.MaxValue;
        UnitBase bestUnit = null;
        
        foreach (UnitBase unit in UnitManager.Instance.AllyUnits)
        {
            if (unit == null) continue;
            float distFromSpawn = Vector3.Distance(_spawnPosition, unit.transform.position);
            if (distFromSpawn <= territoryRadius && distFromSpawn < bestDistance)
            {
                bestDistance = distFromSpawn;
                bestUnit = unit;
            }
        }
        return bestUnit;
    }

    private IEnumerator AttackCoroutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f / attackSpeed);
        while (_aiState == AIState.Attack)
        {
            Damageable currentBuilding = _targetDamageable;
            UnitBase currentUnit = _targetUnit;
            
            if (currentBuilding == null && currentUnit == null)
            {
                EnterWarningState();
                yield break;
            }
            
            Vector3 targetPos = currentBuilding != null ? currentBuilding.transform.position : currentUnit.transform.position;
            float distanceToTarget = Vector3.Distance(transform.position, targetPos);
            
            if (distanceToTarget > attackRange)
            {
                yield return wait;
                continue;
            }
            
            if (currentBuilding != null)
            {
                if (currentBuilding.CurrentHealth > 0)
                {
                    currentBuilding.TakeDamage(attackDamage);
                }
                else
                {
                    _targetDamageable = null;
                    EnterWarningState();
                    yield break;
                }
            }
            else if (currentUnit != null)
            {
                if (currentUnit.currentHealth > 0)
                {
                    currentUnit.TakeDamage(attackDamage);
                }
                
                if (currentUnit.currentHealth <= 0f)
                {
                    _targetUnit = null;
                    EnterWarningState();
                    yield break;
                }
            }
            
            yield return wait;
        }
        _attackCoroutine = null;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? _spawnPosition : transform.position;
        float drawHomeRadius = Application.isPlaying ? homeRadius : 3f;
        float drawTerritoryRadius = Application.isPlaying ? territoryRadius : 6f;
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, drawHomeRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, drawTerritoryRadius);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
