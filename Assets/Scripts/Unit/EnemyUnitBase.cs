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
    [SerializeField] private float minTargetMoveDistance = 0.5f;

    [Header("Roaming")]
    public float minRoamInterval = 2.0f;
    public float maxRoamInterval = 3.0f;
    private float _currentRoamInterval;

    [Header("References")]
    [SerializeField] protected UnitMovement unitMovement;

    protected float warningTimer;
    protected Vector3 spawnPosition;
    protected AIState aiState;
    protected Damageable targetDamageable;
    protected UnitBase targetUnit;
    
    private bool _lastActionWasMove;
    private float _roamTimer;
    private float _lastMoveToTargetTime;
    private Vector3 _lastTargetPos;
    private Coroutine _attackCoroutine;
    private CircleCollider2D _attackRangeCollider;
    
    private const float MinMoveUpdateInterval = 0.5f;
    
    protected override void Awake()
    {
        base.Awake();
        
        _attackRangeCollider = GetComponent<CircleCollider2D>();
        _attackRangeCollider.radius = attackRange;
        _attackRangeCollider.isTrigger = true;
        
        spawnPosition = Vector3.zero;
        aiState = AIState.Idle;
    }
    
    private void Start()
    {
        SetNewRoamInterval();
    }
    
    public void SetTerritoryCenter(Vector3 territoryCenter, float hRadius, float tRadius)
    {
        spawnPosition = territoryCenter;
        homeRadius = hRadius;
        territoryRadius = tRadius;
    }

    private void Update()
    {
        UpdateStateLogic();
    }

    private void UpdateStateLogic()
    {
        switch (aiState)
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
            targetDamageable = targetBuilding;
            this.targetUnit = null;
            EnterWarningState();
            return;
        }
        
        if (targetUnit != null)
        {
            targetDamageable = null;
            this.targetUnit = targetUnit;
            EnterWarningState();
        }
    }

    protected void EnterWarningState()
    {
        if (aiState == AIState.Attack)
        {
            if (_attackCoroutine != null)
            {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }
        }
        
        aiState = AIState.Warning;
        warningTimer = 0f;
        
        _lastMoveToTargetTime = -MinMoveUpdateInterval; 
        _lastTargetPos = Vector3.positiveInfinity;
        
        if (unitMovement != null)
        {
            unitMovement.StopMovement();
        }
    }

    private void HandleWarning()
    {
        warningTimer += Time.deltaTime;
    
        Damageable currentBuilding = targetDamageable;
        UnitBase currentUnit = targetUnit;
    
        if (currentBuilding == null && currentUnit == null)
        {
            ReturnToIdle();
            return;
        }
    
        Vector3 targetPos = currentBuilding != null ? currentBuilding.transform.position : currentUnit.transform.position;
        float distanceFromSpawn = Vector3.Distance(spawnPosition, targetPos);
    
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
    
        if (warningTimer >= warningStatePersist)
        {
            ReturnToIdle();
            return;
        }

        bool isTimeForUpdate = (Time.time - _lastMoveToTargetTime >= MinMoveUpdateInterval);
        bool hasTargetMovedEnough = Vector3.Distance(targetPos, _lastTargetPos) > minTargetMoveDistance;
    
        if (isTimeForUpdate)
        {
            if (hasTargetMovedEnough || (unitMovement != null && !unitMovement.IsMoving)) 
            {
                _lastMoveToTargetTime = Time.time;
                _lastTargetPos = targetPos;
                MoveToCurrentTarget();
            }
        }
    }

    private void HandleAttack()
    {
        Damageable currentBuilding = targetDamageable;
        UnitBase currentUnit = targetUnit;
    
        if (currentBuilding == null && currentUnit == null)
        {
            EnterWarningState();
            return;
        }
    
        Vector3 targetPos = currentBuilding != null ? currentBuilding.transform.position : currentUnit.transform.position;
        float distanceToTarget = Vector3.Distance(transform.position, targetPos);
    
        if (distanceToTarget > attackRange)
        {
            EnterWarningState();
            return;
        }

        if (unitMovement != null)
        {
            unitMovement.StopMovement();
        }
    
        if (currentBuilding != null)
        {
            if (currentBuilding.CurrentHealth <= 0)
            {
                targetDamageable = null;
                EnterWarningState();
                return;
            }
        }
        else if (currentUnit != null)
        {
            if (currentUnit.currentHealth <= 0)
            {
                targetUnit = null;
                EnterWarningState();
                return;
            }
        }
    
        if (_attackCoroutine == null)
        {
            _attackCoroutine = StartCoroutine(AttackCoroutine());
        }
    }
    
    protected void EnterAttackState()
    {
        if (aiState == AIState.Attack) return;
        
        aiState = AIState.Attack;
        warningTimer = 0f;
        
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
        aiState = AIState.Idle;
        warningTimer = 0f;
        targetDamageable = null;
        targetUnit = null;
        
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
        Vector3 destination = FindWalkableRoamDestination();
        if (destination != Vector3.zero)
        {
            unitMovement.SetNewTarget(destination, unitMovement.waypointTolerance);
        }
    }
    
    private Vector3 FindWalkableRoamDestination()
    {
        if (spawnPosition == Vector3.zero || homeRadius <= 0f)
        {
            return Vector3.zero;
        }
        
        Grid grid = BuildingManager.Instance.grid;
        if (grid == null) return Vector3.zero;
        
        Vector3Int spawnCell = grid.WorldToCell(spawnPosition);
        
        int maxAttempts = 50;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomRadius = Random.Range(0f, homeRadius);
            Vector3 candidatePos = spawnPosition + new Vector3(randomDir.x, randomDir.y, 0f) * randomRadius;
            Vector3Int candidateCell = grid.WorldToCell(candidatePos);
            
            if (IsCellWalkable(candidateCell))
            {
                float distanceFromSpawn = Vector3.Distance(spawnPosition, candidatePos);
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
                    float distanceFromSpawn = Vector3.Distance(spawnPosition, grid.GetCellCenterWorld(testCell));
                    
                    if (distanceFromSpawn <= homeRadius && IsCellWalkable(testCell))
                    {
                        return grid.GetCellCenterWorld(testCell);
                    }
                }
            }
        }
        
        return spawnPosition;
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
        
        if (targetDamageable != null)
        {
            targetPos = targetDamageable.transform.position;
        }
        else if (targetUnit != null)
        {
            targetPos = targetUnit.transform.position;
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
            
            float distFromSpawn = Vector3.Distance(spawnPosition, target.transform.position);
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
            float distFromSpawn = Vector3.Distance(spawnPosition, unit.transform.position);
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
        while (aiState == AIState.Attack)
        {
            Damageable currentBuilding = targetDamageable;
            UnitBase currentUnit = targetUnit;
            
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
                    targetDamageable = null;
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
                    targetUnit = null;
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
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
