using System.Collections;
using UnityEngine;

public abstract class EnemyUnitBase : UnitBase
{
    private const float MinMoveUpdateInterval = 0.5f;
    private const float InfiniteAttackTargetUpdateInterval = 0.5f;

    [Header("Zones")]
    [SerializeField] protected float warningStatePersist = 5f;
    [SerializeField] protected float outOfTerritoryChaseTime = 3f;

    [Header("Combat")]
    [SerializeField] protected int attackDamage = 10;
    [SerializeField] protected float attackRange = 1.5f;
    [SerializeField] protected float attackSpeed = 1f;
    [SerializeField] private float minTargetMoveDistance = 0.5f;

    [Header("Roaming")]
    public float minRoamInterval = 2.0f;
    public float maxRoamInterval = 3.0f;

    [Header("References")]
    [SerializeField] protected UnitMovement unitMovement;
    private Coroutine _attackCoroutine;
    private CircleCollider2D _attackRangeCollider;
    private float _currentRoamInterval;
    private float _homeRadius;
    private bool _isInInfiniteAttackState;

    private bool _lastActionWasMove;
    private float _lastInfiniteAttackTargetUpdateTime;
    private float _lastMoveToTargetTime;
    private Vector3 _lastTargetPos;
    private float _outOfTerritoryTimer;
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

        _attackRangeCollider = GetComponent<CircleCollider2D>();
        _attackRangeCollider.radius = attackRange;
        _attackRangeCollider.isTrigger = true;

        _spawnPosition = Vector3.zero;
        aiState = AIState.Idle;
        _isInInfiniteAttackState = false;
    }

    protected override void Start()
    {
        SetNewRoamInterval();
        _spriteController = GetComponentInChildren<UnitSpriteController>();
    }

    private void Update()
    {
        UpdateStateLogic();
        UpdateAnimationState();
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    public override void TakeDamage(float damage)
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

    private void UpdateStateLogic()
    {
        switch (aiState) {
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

    private void UpdateAnimationState()
    {
        if (_spriteController == null) {
            return;
        }

        _spriteController.UpdateAnimationState(currentState);

        if (currentState == UnitState.Moving && unitMovement != null) {
            Vector3 moveDir = unitMovement.GetMoveDirection();
            _spriteController.UpdateSpriteDirection(moveDir);
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
        Damageable targetBuilding = FindClosestDamageableInTerritory();
        UnitBase targetUnit = FindClosestAllyInTerritory();

        if (targetBuilding != null) {
            _targetDamageable = targetBuilding;
            _targetUnit = null;
            EnterWarningState();
            return;
        }

        if (targetUnit != null) {
            _targetDamageable = null;
            _targetUnit = targetUnit;
            EnterWarningState();
        }
    }

    protected void EnterWarningState()
    {
        if (aiState == AIState.Attack) {
            if (_attackCoroutine != null) {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }
        }

        aiState = AIState.Warning;
        _warningTimer = 0f;
        _outOfTerritoryTimer = 0f;

        _lastMoveToTargetTime = -MinMoveUpdateInterval;
        _lastTargetPos = Vector3.positiveInfinity;

        if (unitMovement != null) {
            unitMovement.StopMovement();
        }
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
        float distanceFromSpawn = Vector3.Distance(_spawnPosition, targetPos);

        if (distanceFromSpawn > _territoryRadius) {
            _outOfTerritoryTimer += Time.deltaTime;

            if (_outOfTerritoryTimer > outOfTerritoryChaseTime) {
                ReturnToIdle();
                return;
            }
        }
        else {
            _outOfTerritoryTimer = 0f;
        }

        float distanceToTarget = Vector3.Distance(transform.position, targetPos);

        if (distanceToTarget <= attackRange) {
            EnterAttackState();
            return;
        }

        if (_warningTimer >= warningStatePersist) {
            ReturnToIdle();
            return;
        }

        bool isTimeForUpdate = Time.time - _lastMoveToTargetTime >= MinMoveUpdateInterval;
        bool hasTargetMovedEnough = Vector3.Distance(targetPos, _lastTargetPos) > minTargetMoveDistance;

        if (isTimeForUpdate) {
            if (hasTargetMovedEnough || unitMovement != null && !unitMovement.IsMoving) {
                _lastMoveToTargetTime = Time.time;
                _lastTargetPos = targetPos;
                MoveToCurrentTarget();
            }
        }
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
        float distanceToTarget = Vector3.Distance(transform.position, targetPos);

        bool isMovingTarget = currentUnit != null;

        if (distanceToTarget > attackRange) {
            if (!_isInInfiniteAttackState) {
                EnterWarningState();
                return;
            }

            if (isMovingTarget) {
                bool isTimeForUpdate = Time.time - _lastMoveToTargetTime >= MinMoveUpdateInterval;
                bool hasTargetMovedEnough = Vector3.Distance(targetPos, _lastTargetPos) > minTargetMoveDistance;

                if (isTimeForUpdate && (hasTargetMovedEnough || unitMovement != null && !unitMovement.IsMoving)) {
                    _lastMoveToTargetTime = Time.time;
                    _lastTargetPos = targetPos;
                    MoveToCurrentTarget();
                }
            }
            else if (!_isInInfiniteAttackState) {
                EnterWarningState();
            }
            return;
        }

        if (unitMovement != null) {
            unitMovement.StopMovement();
        }

        if (currentBuilding != null) {
            if (currentBuilding.CurrentHealth <= 0) {
                _targetDamageable = null;
                if (!_isInInfiniteAttackState) {
                    EnterWarningState();
                }
                return;
            }
        }
        else if (currentUnit != null) {
            if (currentUnit.currentHealth <= 0) {
                _targetUnit = null;
                if (!_isInInfiniteAttackState) {
                    EnterWarningState();
                }
                return;
            }
        }

        if (_attackCoroutine == null) {
            _attackCoroutine = StartCoroutine(AttackCoroutine());
        }
    }

    protected void EnterAttackState()
    {
        if (aiState == AIState.Attack) {
            return;
        }

        aiState = AIState.Attack;
        _warningTimer = 0f;
        _lastMoveToTargetTime = -MinMoveUpdateInterval;
        _lastTargetPos = Vector3.positiveInfinity;

        if (unitMovement != null) {
            unitMovement.StopMovement();
        }

        if (_attackCoroutine == null) {
            _attackCoroutine = StartCoroutine(AttackCoroutine());
        }
    }

    protected void ReturnToIdle()
    {
        if (_isInInfiniteAttackState) {
            return;
        }

        aiState = AIState.Idle;
        _warningTimer = 0f;
        _targetDamageable = null;
        _targetUnit = null;

        if (_attackCoroutine != null) {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        if (unitMovement != null) {
            unitMovement.StopMovement();
        }
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
        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomRadius = Random.Range(0f, _homeRadius);
            Vector3 candidatePos = _spawnPosition + new Vector3(randomDir.x, randomDir.y, 0f) * randomRadius;
            Vector3Int candidateCell = grid.WorldToCell(candidatePos);

            if (IsCellWalkable(candidateCell)) {
                float distanceFromSpawn = Vector3.Distance(_spawnPosition, candidatePos);
                if (distanceFromSpawn <= _homeRadius) {
                    return grid.GetCellCenterWorld(candidateCell);
                }
            }
        }

        for (int radius = 1; radius <= Mathf.CeilToInt(_homeRadius); radius++) {
            for (int x = -radius; x <= radius; x++) {
                for (int y = -radius; y <= radius; y++) {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;

                    Vector3Int testCell = spawnCell + new Vector3Int(x, y, 0);
                    float distanceFromSpawn = Vector3.Distance(_spawnPosition, grid.GetCellCenterWorld(testCell));

                    if (distanceFromSpawn <= _homeRadius && IsCellWalkable(testCell)) {
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

        if (_targetDamageable != null) {
            targetPos = _targetDamageable.transform.position;
        }
        else if (_targetUnit != null) {
            targetPos = _targetUnit.transform.position;
        }
        else {
            return;
        }
        unitMovement.SetNewTarget(targetPos, unitMovement.waypointTolerance);
    }

    private Damageable FindClosestDamageableInTerritory()
    {
        if (TargetManager.Instance == null || TargetManager.Instance.AllTargets.Count == 0) {
            return null;
        }

        float bestDistance = float.MaxValue;
        Damageable bestTarget = null;

        foreach (Damageable target in TargetManager.Instance.AllTargets) {
            if (target == null) continue;
            if (target.GetComponent<UnitBase>() != null) continue;

            float distFromSpawn = Vector3.Distance(_spawnPosition, target.transform.position);
            if (distFromSpawn <= _territoryRadius && distFromSpawn < bestDistance) {
                bestDistance = distFromSpawn;
                bestTarget = target;
            }
        }
        return bestTarget;
    }

    private UnitBase FindClosestAllyInTerritory()
    {
        if (UnitManager.Instance == null || UnitManager.Instance.AllyUnits == null || UnitManager.Instance.AllyUnits.Count == 0) {
            return null;
        }

        float bestDistance = float.MaxValue;
        UnitBase bestUnit = null;

        foreach (UnitBase unit in UnitManager.Instance.AllyUnits) {
            if (unit == null) continue;
            float distFromSpawn = Vector3.Distance(_spawnPosition, unit.transform.position);
            if (distFromSpawn <= _territoryRadius && distFromSpawn < bestDistance) {
                bestDistance = distFromSpawn;
                bestUnit = unit;
            }
        }
        return bestUnit;
    }

    private void FindClosestTargetForInfiniteAttack()
    {
        if (Time.time - _lastInfiniteAttackTargetUpdateTime < InfiniteAttackTargetUpdateInterval) {
            return;
        }
        _lastInfiniteAttackTargetUpdateTime = Time.time;

        float bestDistance = float.MaxValue;
        Damageable bestDamageable = null;
        UnitBase bestUnit = null;

        if (TargetManager.Instance != null && TargetManager.Instance.AllTargets.Count > 0) {
            foreach (Damageable target in TargetManager.Instance.AllTargets) {
                if (target == null) continue;
                if (target.GetComponent<UnitBase>() != null) continue;

                float dist = Vector3.Distance(transform.position, target.transform.position);
                if (dist < bestDistance) {
                    bestDistance = dist;
                    bestDamageable = target;
                    bestUnit = null;
                }
            }
        }

        if (UnitManager.Instance != null && UnitManager.Instance.AllyUnits != null && UnitManager.Instance.AllyUnits.Count > 0) {
            foreach (UnitBase unit in UnitManager.Instance.AllyUnits) {
                if (unit == null || unit.currentHealth <= 0) continue;

                float dist = Vector3.Distance(transform.position, unit.transform.position);
                if (dist < bestDistance) {
                    bestDistance = dist;
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
        WaitForSeconds wait = new WaitForSeconds(1f / attackSpeed);
        while (aiState == AIState.Attack) {
            Damageable currentBuilding = _targetDamageable;
            UnitBase currentUnit = _targetUnit;

            if (_isInInfiniteAttackState && currentBuilding == null && currentUnit == null) {
                FindClosestTargetForInfiniteAttack();
                currentBuilding = _targetDamageable;
                currentUnit = _targetUnit;

                if (currentBuilding == null && currentUnit == null) {
                    yield return wait;
                    continue;
                }
            }

            if (currentBuilding == null && currentUnit == null) {
                if (!_isInInfiniteAttackState) {
                    EnterWarningState();
                }
                yield break;
            }

            Vector3 targetPos = currentBuilding != null ? currentBuilding.transform.position : currentUnit.transform.position;
            float distanceToTarget = Vector3.Distance(transform.position, targetPos);

            if (distanceToTarget > attackRange) {
                yield return wait;
                continue;
            }

            if (currentBuilding != null) {
                if (currentBuilding.CurrentHealth > 0) {
                    currentBuilding.TakeDamage(attackDamage);
                }
                else {
                    _targetDamageable = null;
                    if (!_isInInfiniteAttackState) {
                        EnterWarningState();
                    }
                    yield break;
                }
            }
            else if (currentUnit != null) {
                if (currentUnit.currentHealth > 0) {
                    currentUnit.TakeDamage(attackDamage);
                }

                if (currentUnit.currentHealth <= 0f) {
                    _targetUnit = null;
                    if (!_isInInfiniteAttackState) {
                        EnterWarningState();
                    }
                    yield break;
                }
            }

            yield return wait;
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
