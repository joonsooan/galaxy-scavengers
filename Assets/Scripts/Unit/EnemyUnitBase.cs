using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public abstract class EnemyUnitBase : UnitBase
{
    private const float InfiniteAttackTargetUpdateInterval = 0.5f;
    private const float TerritoryCheckIntervalIdle = 1.0f;
    private const float TerritoryCheckIntervalWarning = 0.3f;
    private const float AttackHysteresisBuffer = 0.5f;

    [Header("Zones")]
    [SerializeField] protected float warningStatePersist = 5f;
    [SerializeField] protected float outOfTerritoryChaseTime = 3f;

    [Header("Combat")]
    [SerializeField] protected int attackDamage = 10;
    [SerializeField] protected float attackRange = 1.5f;
    [SerializeField] protected float maxUnitChaseDistance = 5.0f;
    [SerializeField] protected float attackDamageTiming = 0.4f;
    [SerializeField] private float minTargetMoveDistance = 0.5f;
    
    [Header("Colliders")]
    [SerializeField] private CircleCollider2D bodyCollider;

    [Header("Roaming")]
    public float minRoamInterval = 2.0f;
    public float maxRoamInterval = 3.0f;

    [Header("References")]
    [SerializeField] protected UnitMovement unitMovement;
    private Coroutine _aiUpdateCoroutine;
    private WaitForSeconds _aiUpdateWait;
    private Coroutine _attackCoroutine;
    private float _currentRoamInterval;
    private float _homeRadius;
    private bool _isInInfiniteAttackState;
    private bool _lastActionWasMove;
    private float _lastInfiniteAttackTargetUpdateTime;
    private Vector3 _lastTargetPos;
    private float _lastTerritoryCheckTime;
    private float _outOfTerritoryTimer;
    private float _pathUpdateTimer;
    private float _roamTimer;
    private Vector3 _spawnPosition;
    private UnitSpriteController _spriteController;
    protected Damageable _targetDamageable;
    protected UnitBase _targetUnit;
    private float _territoryRadius;
    private float _warningTimer;
    private bool _isEnhanced;
    private float _enhancementMoveSpeedMult = 1f;
    private float _enhancementAttackMult = 1f;
    private float _enhancementHealthMult = 1f;
    protected AIState aiState;

    public bool IsEnhanced => _isEnhanced;

    protected override void Awake()
    {
        base.Awake();
        if (bodyCollider != null) {
            bodyCollider.isTrigger = false;
        }
        _spawnPosition = Vector3.zero;
        aiState = AIState.Idle;
        _isInInfiniteAttackState = false;
        _aiUpdateWait = CoroutineCache.GetWaitForSeconds(0.15f);
    }

    protected void Start()
    {
        SetNewRoamInterval();
        _spriteController = GetComponentInChildren<UnitSpriteController>();
        if (unitMovement != null) {
            unitMovement.moveSpeed *= Random.Range(0.95f, 1.05f);
        }
    }

    private void LateUpdate()
    {
        UpdateAnimationState();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_currentRoamInterval <= 0f) SetNewRoamInterval();
        _roamTimer = _currentRoamInterval;
        _pathUpdateTimer = Time.time + Random.Range(0f, 1f) + (GetInstanceID() % 20) * 0.05f;
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
        ResetEnhancement();
        base.OnDisable();
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    private IEnumerator AIUpdateRoutine()
    {
        int initialDelayFrames = Random.Range(0, 30);
        for (int i = 0; i < initialDelayFrames; i++) yield return null;
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

    public bool IsInInfiniteAttackState() => _isInInfiniteAttackState;

    public void SetTerritoryCenter(Vector3 territoryCenter, float hRadius, float tRadius)
    {
        _spawnPosition = territoryCenter;
        _homeRadius = hRadius;
        _territoryRadius = tRadius;
    }

    public void ApplyEnhancement(Color spriteColor, float moveSpeedMult, float attackMult, float healthMult)
    {
        _isEnhanced = true;
        _enhancementMoveSpeedMult = moveSpeedMult;
        _enhancementAttackMult = attackMult;
        _enhancementHealthMult = healthMult;
        SetPersistentTint(spriteColor);
        if (unitMovement != null) {
            unitMovement.moveSpeed *= moveSpeedMult;
        }
        attackDamage = Mathf.RoundToInt(attackDamage * attackMult);
        int newMaxHealth = Mathf.RoundToInt(MaxHealth * healthMult);
        SetMaxHealth(newMaxHealth);
        RestoreToFullHealth();
    }

    private void ResetEnhancement()
    {
        if (!_isEnhanced) return;

        _isEnhanced = false;
        SetPersistentTint(Color.white);
        if (unitMovement != null && _enhancementMoveSpeedMult > 0f) {
            unitMovement.moveSpeed /= _enhancementMoveSpeedMult;
        }
        if (_enhancementAttackMult > 0f) {
            attackDamage = Mathf.RoundToInt(attackDamage / _enhancementAttackMult);
        }
        if (_enhancementHealthMult > 0f) {
            int baseMaxHealth = Mathf.RoundToInt(MaxHealth / _enhancementHealthMult);
            SetMaxHealth(baseMaxHealth);
            RestoreToFullHealth();
        }
        _enhancementMoveSpeedMult = 1f;
        _enhancementAttackMult = 1f;
        _enhancementHealthMult = 1f;
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
        if (_spriteController == null) return;
        bool isMoving = unitMovement != null && unitMovement.IsMoving;
        if (isMoving) currentState = UnitState.Moving;
        else if (aiState == AIState.Attack) currentState = UnitState.Attacking;
        else currentState = UnitState.Idle;
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
        else _spriteController.ClearTarget();
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
                if (unitMovement != null) unitMovement.StopMovement();
                _lastActionWasMove = false;
            }
            else {
                ChooseRoamDestination();
                _lastActionWasMove = true;
            }
        }
    }

    private void SetNewRoamInterval() => _currentRoamInterval = Random.Range(minRoamInterval, maxRoamInterval);

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
            float distSq = (target.transform.position - _spawnPosition).sqrMagnitude;
            if (distSq <= territoryRadiusSquared && distSq < bestBuildingDist) { bestBuildingDist = distSq; bestBuilding = target; }
        }
        List<UnitBase> unitsInArea = UnitManager.Instance.GetAllyUnitsInArea(_spawnPosition, _territoryRadius);
        foreach (UnitBase unit in unitsInArea) {
            if (unit == null) continue;
            float distSq = (unit.transform.position - _spawnPosition).sqrMagnitude;
            if (distSq <= territoryRadiusSquared && distSq < bestUnitDist) { bestUnitDist = distSq; bestUnit = unit; }
        }
        if (bestBuilding != null) { _targetDamageable = bestBuilding; _targetUnit = null; EnterWarningState(); return; }
        if (bestUnit != null) { _targetDamageable = null; _targetUnit = bestUnit; EnterWarningState(); }
    }

    private void EnterWarningState()
    {
        if (aiState == AIState.Warning) return;
        aiState = AIState.Warning;
        _warningTimer = 0f;
        _outOfTerritoryTimer = 0f;
    }

    private void HandleWarning()
    {
        _warningTimer += Time.deltaTime;
        Damageable currentBuilding = _targetDamageable;
        UnitBase currentUnit = _targetUnit;
        if (currentBuilding == null && currentUnit == null) { ReturnToIdle(); return; }
        Vector3 targetPos = currentBuilding != null ? currentBuilding.transform.position : currentUnit.transform.position;
        if (currentUnit != null && (transform.position - targetPos).magnitude > maxUnitChaseDistance) {
            if (!_isInInfiniteAttackState) ReturnToIdle();
            else { _targetUnit = null; FindClosestTargetForInfiniteAttack(); }
            return;
        }
        Vector3 closestTargetPos = GetClosestTargetPosition(currentBuilding, currentUnit);
        if ((transform.position - closestTargetPos).sqrMagnitude <= attackRange * attackRange) { EnterAttackState(); return; }
        if ((_spawnPosition - targetPos).sqrMagnitude > _territoryRadius * _territoryRadius) {
            _outOfTerritoryTimer += Time.deltaTime;
            if (_outOfTerritoryTimer > outOfTerritoryChaseTime) {
                if (!_isInInfiniteAttackState) { ReturnToIdle(); return; }
                else { _outOfTerritoryTimer = 0f; }
            }
        } else _outOfTerritoryTimer = 0f;
        if (_warningTimer >= warningStatePersist && !_isInInfiniteAttackState) { ReturnToIdle(); return; }
        MoveToCurrentTarget();
    }

    private void HandleAttack()
    {
        if (_attackCoroutine != null) return;

        Damageable currentBuilding = _targetDamageable;
        UnitBase currentUnit = _targetUnit;

        if (_isInInfiniteAttackState && currentBuilding == null && currentUnit == null) {
            FindClosestTargetForInfiniteAttack();
            currentBuilding = _targetDamageable;
            currentUnit = _targetUnit;
        }

        if (currentBuilding == null && currentUnit == null) {
            if (!_isInInfiniteAttackState) EnterWarningState();
            return;
        }

        Vector3 targetPos = GetClosestTargetPosition(currentBuilding, currentUnit);
        float distanceToTargetSquared = (transform.position - targetPos).sqrMagnitude;
        float attackRangeSquared = attackRange * attackRange;

        if (distanceToTargetSquared > attackRangeSquared) {
            MoveToCurrentTarget();
            return;
        }

        if (RequiresLineOfSight() && !HasLineOfSightToTarget(currentBuilding, currentUnit)) {
            MoveToCurrentTarget();
            return;
        }

        if (unitMovement != null && unitMovement.IsMoving) {
            unitMovement.StopMovement();
        }

        _attackCoroutine = StartCoroutine(AttackCoroutine());
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
        if (destination != Vector3.zero) unitMovement.SetNewTarget(destination, unitMovement.waypointTolerance);
    }

    private Vector3 FindWalkableRoamDestination()
    {
        if (_spawnPosition == Vector3.zero || _homeRadius <= 0f) return Vector3.zero;
        Grid grid = BuildingManager.Instance.grid;
        if (grid == null) return Vector3.zero;
        for (int attempt = 0; attempt < 50; attempt++) {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            Vector3 candidatePos = _spawnPosition + (Vector3)randomDir * Random.Range(0f, _homeRadius);
            Vector3Int candidateCell = grid.WorldToCell(candidatePos);
            if (BuildingManager.Instance.IsCellWalkable(candidateCell)) return grid.GetCellCenterWorld(candidateCell);
        }
        return _spawnPosition;
    }

    private void MoveToCurrentTarget()
    {
        if (unitMovement == null) return;
        Vector3 targetPos = _targetDamageable != null ? _targetDamageable.transform.position : (_targetUnit != null ? _targetUnit.transform.position : Vector3.zero);
        if (targetPos == Vector3.zero) return;
        if (Time.time < _pathUpdateTimer) return;
        if ((targetPos - _lastTargetPos).sqrMagnitude > minTargetMoveDistance * minTargetMoveDistance || !unitMovement.IsMoving) {
            float dist = Vector3.Distance(transform.position, targetPos);
            float interval = dist < 5f ? 0.3f : dist < 15f ? 0.5f : 0.7f;
            _pathUpdateTimer = Time.time + interval;
            _lastTargetPos = targetPos;
            unitMovement.SetNewTarget(targetPos, unitMovement.waypointTolerance);
        }
    }

    private void FindClosestTargetForInfiniteAttack()
    {
        if (Time.time - _lastInfiniteAttackTargetUpdateTime < InfiniteAttackTargetUpdateInterval) return;
        _lastInfiniteAttackTargetUpdateTime = Time.time;

        float bestBuildingDistSq = float.MaxValue;
        Damageable bestBuilding = null;
        if (TargetManager.Instance != null) {
            foreach (Damageable t in TargetManager.Instance.AllTargets) {
                if (t == null || t.GetComponent<UnitBase>() != null) continue;
                float dSq = (transform.position - t.transform.position).sqrMagnitude;
                if (dSq < bestBuildingDistSq) { bestBuildingDistSq = dSq; bestBuilding = t; }
            }
        }

        float bestUnitDistSq = float.MaxValue;
        UnitBase bestUnit = null;
        if (UnitManager.Instance != null && UnitManager.Instance.AllyUnits != null) {
            foreach (UnitBase u in UnitManager.Instance.AllyUnits) {
                if (u == null || u.CurrentHealth <= 0) continue;
                float dSq = (transform.position - u.transform.position).sqrMagnitude;
                if (dSq < bestUnitDistSq) { bestUnitDistSq = dSq; bestUnit = u; }
            }
        }

        if (bestBuilding != null) {
            _targetDamageable = bestBuilding;
            _targetUnit = null;
        }
        else if (bestUnit != null) {
            _targetDamageable = null;
            _targetUnit = bestUnit;
        }
        else {
            _targetDamageable = null;
            _targetUnit = null;
        }
    }
    
    protected virtual bool RequiresLineOfSight() => false;

    protected virtual bool HasLineOfSightToTarget(Damageable building, UnitBase unit)
    {
        return true;
    }

    protected Vector3 GetClosestTargetPosition(Damageable building, UnitBase unit)
    {
        if (unit != null) return unit.transform.position;
        if (building == null) return Vector3.zero;

        Grid grid = BuildingManager.Instance?.grid;
        if (grid == null) return building.transform.position;

        Vector3Int buildingCell = grid.WorldToCell(building.transform.position);
        if (BuildingManager.Instance.GetBuildingAt(buildingCell, out List<Vector3Int> occupiedCells) && occupiedCells != null && occupiedCells.Count > 0)
        {
            Vector3 enemyPos = transform.position;
            Vector3 closestPos = grid.GetCellCenterWorld(occupiedCells[0]);
            float minDistSq = (enemyPos - closestPos).sqrMagnitude;
            for (int i = 1; i < occupiedCells.Count; i++)
            {
                Vector3 cellPos = grid.GetCellCenterWorld(occupiedCells[i]);
                float dSq = (enemyPos - cellPos).sqrMagnitude;
                if (dSq < minDistSq) { minDistSq = dSq; closestPos = cellPos; }
            }
            return closestPos;
        }
        return building.transform.position;
    }

    protected virtual bool CanPerformAttack()
    {
        return true;
    }

    protected virtual void PerformAttackLogic(Damageable building, UnitBase unit)
    {
        if (building != null) building.TakeDamage(attackDamage);
        else if (unit != null) unit.TakeDamage(attackDamage);
    }

    private IEnumerator AttackCoroutine()
    {
        while (aiState == AIState.Attack) {
            Damageable currentT = _targetDamageable;
            UnitBase currentU = _targetUnit;
            
            if (currentT == null && currentU == null) break;

            Vector3 tPos = GetClosestTargetPosition(currentT, currentU);
            
            if ((transform.position - tPos).sqrMagnitude > attackRange * attackRange) {
                break; 
            }

            if (!CanPerformAttack()) {
                break;
            }

            if (_spriteController != null) {
                _spriteController.UpdateAnimationState(currentState, isAttacking: true);
                
                yield return null; 

                float animLen = _spriteController.GetCurrentAnimationLength();
                if (animLen <= 0) animLen = 0.5f;

                float dDelay = animLen * Mathf.Clamp01(attackDamageTiming);
                
                yield return CoroutineCache.GetWaitForSeconds(dDelay);

                if (_targetDamageable != null || _targetUnit != null) {
                    PerformAttackLogic(_targetDamageable, _targetUnit);
                }

                yield return CoroutineCache.GetWaitForSeconds(Mathf.Max(0, animLen - dDelay));
                _spriteController.UpdateAnimationState(currentState, isAttacking: false);
            }
            else {
                PerformAttackLogic(currentT, currentU);
                yield return CoroutineCache.GetWaitForSeconds(0.5f);
            }
        }
        
        _attackCoroutine = null;
    }

    protected enum AIState { Idle, Warning, Attack }
}