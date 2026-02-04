using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public abstract class EnemyUnitBase : UnitBase
{
    private const float MinMoveUpdateInterval = 0.5f;
    private const float InfiniteAttackTargetUpdateInterval = 0.5f;
    private const float TerritoryCheckIntervalIdle = 1.0f;
    private const float TerritoryCheckIntervalWarning = 0.3f;
    private const float PathRecomputeDistance = 2.0f;
    private const float PathUpdateInterval = 0.25f;
    private const float MinTargetMoveThreshold = 0.5f;
    private const float AttackHysteresisBuffer = 0.5f;

    [Header("Zones")]
    [SerializeField] protected float warningStatePersist = 5f;
    [SerializeField] protected float outOfTerritoryChaseTime = 3f;

    [Header("Combat")]
    [SerializeField] protected int attackDamage = 10;
    [SerializeField] protected float attackRange = 1.5f;
    [SerializeField] protected float attackSpeed = 1f;
    [SerializeField] protected float maxUnitChaseDistance = 5.0f;
    [SerializeField] protected float attackDamageTiming = 0.4f;
    [SerializeField] private float minTargetMoveDistance = 0.5f;
    
    [Header("Colliders")]
    [SerializeField] private CircleCollider2D bodyCollider;
    [SerializeField] private CircleCollider2D attackRangeCollider;

    [Header("Roaming")]
    public float minRoamInterval = 2.0f;
    public float maxRoamInterval = 3.0f;

    [Header("References")]
    [SerializeField] protected UnitMovement unitMovement;
    private Coroutine _aiUpdateCoroutine;
    private WaitForSeconds _aiUpdateWait;
    private Coroutine _attackCoroutine;
    private CircleCollider2D _attackRangeCollider;
    private float _currentRoamInterval;
    private float _homeRadius;
    private bool _isInInfiniteAttackState;

    private bool _lastActionWasMove;
    private float _lastInfiniteAttackTargetUpdateTime;
    private Vector3 _lastKnownTargetPos;
    private float _lastMoveToTargetTime;
    private Vector3 _lastPathfindingTargetPos;
    private Vector3 _lastTargetPos;
    private float _lastTerritoryCheckTime;
    private float _outOfTerritoryTimer;

    private float _pathUpdateTimer;
    private float _roamTimer;
    private Vector3 _spawnPosition;
    private UnitSpriteController _spriteController;
    private Damageable _targetDamageable;
    private UnitBase _targetUnit;
    private float _territoryRadius;
    private float _warningTimer;
    protected AIState aiState;

    protected override void Awake()
    {
        base.Awake();

        if (attackRangeCollider != null) {
            attackRangeCollider.radius = attackRange;
            attackRangeCollider.isTrigger = true;
        }

        if (bodyCollider != null) {
            bodyCollider.isTrigger = false;
        }

        _spawnPosition = Vector3.zero;
        aiState = AIState.Idle;
        _isInInfiniteAttackState = false;
        _aiUpdateWait = CoroutineCache.GetWaitForSeconds(0.1f);
    }

    protected void Start()
    {
        SetNewRoamInterval();
        _spriteController = GetComponentInChildren<UnitSpriteController>();
    }

    private void LateUpdate()
    {
        UpdateAnimationState();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (_currentRoamInterval <= 0f) {
            SetNewRoamInterval();
        }
        _roamTimer = _currentRoamInterval;

        if (_aiUpdateWait != null) {
            _aiUpdateCoroutine = StartCoroutine(AIUpdateRoutine());
        }
    }

    protected override void OnDisable()
    {
        if (_aiUpdateCoroutine != null) {
            StopCoroutine(_aiUpdateCoroutine);
            _aiUpdateCoroutine = null;
        }

        base.OnDisable();
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    private IEnumerator AIUpdateRoutine()
    {
        int initialDelayFrames = Random.Range(0, 12);
        for (int i = 0; i < initialDelayFrames; i++) {
            yield return null;
        }

        while (true) {
            UpdateStateLogic();
            yield return _aiUpdateWait;
        }
    }

    public override void TakeDamage(int damage)
    {
        base.TakeDamage(damage);

        if (!_isInInfiniteAttackState) {
            _isInInfiniteAttackState = true;
            EnterAttackState();
        }
    }

    public void SetTerritoryCenter(Vector3 territoryCenter, float hRadius, float tRadius)
    {
        _spawnPosition = territoryCenter;
        _homeRadius = hRadius;
        _territoryRadius = tRadius;
    }

    public void ActivateInfiniteAttackState()
    {
        if (!_isInInfiniteAttackState) {
            _isInInfiniteAttackState = true;
            EnterAttackState();
        }
    }

    public void DeactivateInfiniteAttackState()
    {
        if (_isInInfiniteAttackState) {
            _isInInfiniteAttackState = false;
            ReturnToIdle();
        }
    }

    public bool IsInInfiniteAttackState()
    {
        return _isInInfiniteAttackState;
    }

    private void UpdateStateLogic()
    {
        switch (aiState) {
        case AIState.Idle:
            HandleIdle();
            if (Time.time - _lastTerritoryCheckTime >= TerritoryCheckIntervalIdle) {
                CheckForTerritoryEntry();
                _lastTerritoryCheckTime = Time.time;
            }
            break;

        case AIState.Warning:
            HandleWarning();
            if (Time.time - _lastTerritoryCheckTime >= TerritoryCheckIntervalWarning) {
                CheckForTerritoryEntry();
                _lastTerritoryCheckTime = Time.time;
            }
            break;

        case AIState.Attack:
            HandleAttack();
            break;
        }
    }

    private void UpdateAnimationState()
    {
        if (_spriteController == null) {
            return;
        }
        
        bool isMoving = unitMovement != null && unitMovement.IsMoving;
        
        if (isMoving) {
            currentState = UnitState.Moving;
        }
        else if (aiState == AIState.Attack) {
            currentState = UnitState.Attacking;
        }
        else {
            currentState = UnitState.Idle;
        }

        _spriteController.UpdateAnimationState(currentState);

        if (currentState == UnitState.Moving && unitMovement != null) {
            Vector3 moveDir = unitMovement.GetMoveDirection();
            _spriteController.UpdateSpriteDirection(moveDir);
            _spriteController.ClearTarget();
        }
        else if (currentState == UnitState.Attacking || aiState == AIState.Warning) {
            Transform targetT = _targetDamageable != null ? _targetDamageable.transform : (_targetUnit != null ? _targetUnit.transform : null);
            if (targetT != null) {
                Vector2 direction = (targetT.position - transform.position).normalized;
                _spriteController.UpdateSpriteDirection(direction);
                _spriteController.SetTargetTransform(targetT);
            }
        }
        else {
            _spriteController.ClearTarget();
        }
    }

    private void HandleIdle()
    {
        _roamTimer += Time.deltaTime;
        if (_roamTimer < _currentRoamInterval) return;
        _roamTimer = 0f;
        SetNewRoamInterval();

        if (!_lastActionWasMove) {
            ChooseRoamDestination();
            _lastActionWasMove = true;
        }
        else {
            if (Random.value < 0.5f) {
                if (unitMovement != null) {
                    unitMovement.StopMovement();
                }
                _lastActionWasMove = false;
            }
            else {
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
        if (TargetManager.Instance == null || UnitManager.Instance == null) return;

        float territoryRadiusSquared = _territoryRadius * _territoryRadius;
        Damageable bestBuilding = null;
        UnitBase bestUnit = null;
        float bestBuildingDist = float.MaxValue;
        float bestUnitDist = float.MaxValue;

        List<Damageable> targetsInArea = TargetManager.Instance.GetTargetsInArea(_spawnPosition, _territoryRadius);
        foreach (Damageable target in targetsInArea) {
            if (target == null) continue;

            Vector3 targetPos = target.transform.position;
            float distFromSpawnSquared = (targetPos - _spawnPosition).sqrMagnitude;

            if (distFromSpawnSquared <= territoryRadiusSquared && distFromSpawnSquared < bestBuildingDist) {
                bestBuildingDist = distFromSpawnSquared;
                bestBuilding = target;
            }
        }

        List<UnitBase> unitsInArea = UnitManager.Instance.GetAllyUnitsInArea(_spawnPosition, _territoryRadius);
        foreach (UnitBase unit in unitsInArea) {
            if (unit == null) continue;

            Vector3 unitPos = unit.transform.position;
            float distFromSpawnSquared = (unitPos - _spawnPosition).sqrMagnitude;

            if (distFromSpawnSquared <= territoryRadiusSquared && distFromSpawnSquared < bestUnitDist) {
                bestUnitDist = distFromSpawnSquared;
                bestUnit = unit;
            }
        }

        if (bestBuilding != null) {
            _targetDamageable = bestBuilding;
            _targetUnit = null;
            EnterWarningState();
            return;
        }

        if (bestUnit != null) {
            _targetDamageable = null;
            _targetUnit = bestUnit;
            EnterWarningState();
        }
    }

    private void EnterWarningState()
    {
        if (aiState == AIState.Warning) return;
        
        aiState = AIState.Warning;
        _warningTimer = 0f;
        _outOfTerritoryTimer = 0f;
        _lastMoveToTargetTime = -MinMoveUpdateInterval;
    }

    private void HandleWarning()
    {
        _warningTimer += Time.deltaTime;

        Damageable currentBuilding = _targetDamageable;
        UnitBase currentUnit = _targetUnit;

        if (currentBuilding == null && currentUnit == null) {
            ReturnToIdle();
            return;
        }

        Vector3 targetPos = currentBuilding != null ? currentBuilding.transform.position : currentUnit.transform.position;
        
        if (currentUnit != null) {
            float distanceToUnit = (transform.position - targetPos).magnitude;
            if (distanceToUnit > maxUnitChaseDistance) {
                if (!_isInInfiniteAttackState) { ReturnToIdle(); }
                else { _targetUnit = null; FindClosestTargetForInfiniteAttack(); }
                return;
            }
        }
        
        float distanceToTargetSquared = (transform.position - targetPos).sqrMagnitude;
        float attackRangeSquared = attackRange * attackRange;

        if (distanceToTargetSquared <= attackRangeSquared) {
            EnterAttackState();
            return;
        }

        float distanceFromSpawnSquared = (_spawnPosition - targetPos).sqrMagnitude;
        float territoryRadiusSquared = _territoryRadius * _territoryRadius;
        if (distanceFromSpawnSquared > territoryRadiusSquared) {
            _outOfTerritoryTimer += Time.deltaTime;
            if (_outOfTerritoryTimer > outOfTerritoryChaseTime) { ReturnToIdle(); return; }
        } else { _outOfTerritoryTimer = 0f; }

        if (_warningTimer >= warningStatePersist) { ReturnToIdle(); return; }

        MoveToCurrentTarget();
    }

    private void HandleAttack()
    {
        Damageable currentBuilding = _targetDamageable;
        UnitBase currentUnit = _targetUnit;

        if (_isInInfiniteAttackState && currentBuilding == null && currentUnit == null) {
            FindClosestTargetForInfiniteAttack();
            currentBuilding = _targetDamageable;
            currentUnit = _targetUnit;
        }

        if (currentBuilding == null && currentUnit == null) {
            if (!_isInInfiniteAttackState) {
                EnterWarningState();
            }
            return;
        }

        Vector3 targetPos = currentBuilding != null ? currentBuilding.transform.position : currentUnit.transform.position;
        float distanceToTargetSquared = (transform.position - targetPos).sqrMagnitude;
        
        float currentAttackRange = (_attackCoroutine != null) ? (attackRange + AttackHysteresisBuffer) : attackRange;
        float attackRangeSquared = currentAttackRange * currentAttackRange;

        if (distanceToTargetSquared > attackRangeSquared) {
            if (_attackCoroutine != null) {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
                if (_spriteController != null) _spriteController.UpdateAnimationState(currentState, isAttacking: false);
            }
            
            MoveToCurrentTarget();
            return;
        }

        if (unitMovement != null && unitMovement.IsMoving) {
            unitMovement.StopMovement();
        }

        if (_attackCoroutine == null) {
            _attackCoroutine = StartCoroutine(AttackCoroutine());
        }
    }

    private void EnterAttackState()
    {
        if (aiState == AIState.Attack) return;
        aiState = AIState.Attack;
        _warningTimer = 0f;
    }

    private void ReturnToIdle()
    {
        if (_isInInfiniteAttackState) return;
        aiState = AIState.Idle;
        _targetDamageable = null;
        _targetUnit = null;
        if (_attackCoroutine != null) { StopCoroutine(_attackCoroutine); _attackCoroutine = null; }
        if (unitMovement != null) unitMovement.StopMovement();
    }

    private void ChooseRoamDestination()
    {
        Vector3 destination = FindWalkableRoamDestination();
        if (destination != Vector3.zero) {
            unitMovement.SetNewTarget(destination, unitMovement.waypointTolerance);
        }
    }

    private Vector3 FindWalkableRoamDestination()
    {
        if (_spawnPosition == Vector3.zero || _homeRadius <= 0f) {
            return Vector3.zero;
        }

        Grid grid = BuildingManager.Instance.grid;
        if (grid == null) return Vector3.zero;

        Vector3Int spawnCell = grid.WorldToCell(_spawnPosition);

        int maxAttempts = 50;
        float homeRadiusSquared;
        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomRadius = Random.Range(0f, _homeRadius);
            Vector3 candidatePos = _spawnPosition + new Vector3(randomDir.x, randomDir.y, 0f) * randomRadius;
            Vector3Int candidateCell = grid.WorldToCell(candidatePos);

            if (IsCellWalkable(candidateCell)) {
                float distanceFromSpawnSquared = (_spawnPosition - candidatePos).sqrMagnitude;
                homeRadiusSquared = _homeRadius * _homeRadius;
                if (distanceFromSpawnSquared <= homeRadiusSquared) {
                    return grid.GetCellCenterWorld(candidateCell);
                }
            }
        }

        homeRadiusSquared = _homeRadius * _homeRadius;
        for (int radius = 1; radius <= Mathf.CeilToInt(_homeRadius); radius++) {
            for (int x = -radius; x <= radius; x++) {
                for (int y = -radius; y <= radius; y++) {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;

                    Vector3Int testCell = spawnCell + new Vector3Int(x, y, 0);
                    Vector3 testCellWorldPos = grid.GetCellCenterWorld(testCell);
                    float distanceFromSpawnSquared = (_spawnPosition - testCellWorldPos).sqrMagnitude;

                    if (distanceFromSpawnSquared <= homeRadiusSquared && IsCellWalkable(testCell)) {
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

    private void MoveToCurrentTarget()
    {
        if (unitMovement == null) return;
        Vector3 targetPos = _targetDamageable != null ? _targetDamageable.transform.position : (_targetUnit != null ? _targetUnit.transform.position : Vector3.zero);
        if (targetPos == Vector3.zero) return;

        if (Time.time < _pathUpdateTimer) return;
        
        float minTargetMoveDistanceSquared = minTargetMoveDistance * minTargetMoveDistance;
        if ((targetPos - _lastTargetPos).sqrMagnitude > minTargetMoveDistanceSquared || !unitMovement.IsMoving) {
            _pathUpdateTimer = Time.time + PathUpdateInterval;
            _lastTargetPos = targetPos;
            unitMovement.SetNewTarget(targetPos, unitMovement.waypointTolerance);
        }
    }

    private void FindClosestTargetForInfiniteAttack()
    {
        if (Time.time - _lastInfiniteAttackTargetUpdateTime < InfiniteAttackTargetUpdateInterval) {
            return;
        }
        _lastInfiniteAttackTargetUpdateTime = Time.time;

        float bestDistanceSquared = float.MaxValue;
        Damageable bestDamageable = null;
        UnitBase bestUnit = null;

        if (TargetManager.Instance != null && TargetManager.Instance.AllTargets.Count > 0) {
            foreach (Damageable target in TargetManager.Instance.AllTargets) {
                if (target == null) continue;
                if (target.GetComponent<UnitBase>() != null) continue;

                float distSquared = (transform.position - target.transform.position).sqrMagnitude;
                if (distSquared < bestDistanceSquared) {
                    bestDistanceSquared = distSquared;
                    bestDamageable = target;
                }
            }
        }

        if (UnitManager.Instance != null && UnitManager.Instance.AllyUnits != null && UnitManager.Instance.AllyUnits.Count > 0) {
            foreach (UnitBase unit in UnitManager.Instance.AllyUnits) {
                if (unit == null || unit.CurrentHealth <= 0) continue;

                float distSquared = (transform.position - unit.transform.position).sqrMagnitude;
                if (distSquared < bestDistanceSquared) {
                    bestDistanceSquared = distSquared;
                    bestDamageable = null;
                    bestUnit = unit;
                }
            }
        }

        if (bestDamageable != null || bestUnit != null) {
            _targetDamageable = bestDamageable;
            _targetUnit = bestUnit;
        }
    }

    private IEnumerator AttackCoroutine()
    {
        float baseCooldown = 1f / attackSpeed;
        
        while (aiState == AIState.Attack) {
            Damageable currentTarget = _targetDamageable;
            UnitBase currentUnitTarget = _targetUnit;
            
            if (currentTarget == null && currentUnitTarget == null) yield break;

            Vector3 targetPos = currentTarget != null ? currentTarget.transform.position : currentUnitTarget.transform.position;
            if ((transform.position - targetPos).sqrMagnitude > (attackRange * attackRange)) {
                _attackCoroutine = null;
                yield break;
            }

            if (_spriteController != null) {
                _spriteController.UpdateAnimationState(currentState, isAttacking: true);
                
                float animationLength = _spriteController.GetCurrentAnimationLength();
                if (animationLength <= 0) animationLength = 0.5f; // 예외 처리

                float damageDelay = animationLength * Mathf.Clamp01(attackDamageTiming);
                
                yield return CoroutineCache.GetWaitForSeconds(damageDelay);

                float checkRangeSq = (attackRange + AttackHysteresisBuffer) * (attackRange + AttackHysteresisBuffer);
                if ((transform.position - targetPos).sqrMagnitude <= checkRangeSq) {
                    if (currentTarget != null) currentTarget.TakeDamage(attackDamage);
                    else if (currentUnitTarget != null) currentUnitTarget.TakeDamage(attackDamage);
                }

                yield return CoroutineCache.GetWaitForSeconds(animationLength - damageDelay);
                _spriteController.UpdateAnimationState(currentState, isAttacking: false);

                float remainingCooldown = Mathf.Max(0, baseCooldown - animationLength);
                if (remainingCooldown > 0) yield return CoroutineCache.GetWaitForSeconds(remainingCooldown);
            }
            else {
                if (currentTarget != null) currentTarget.TakeDamage(attackDamage);
                yield return CoroutineCache.GetWaitForSeconds(baseCooldown);
            }
        }
        _attackCoroutine = null;
    }

    protected enum AIState
    {
        Idle,
        Warning,
        Attack
    }
}
