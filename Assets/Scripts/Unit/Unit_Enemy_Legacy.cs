using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Unit_Enemy_Legacy : UnitBase
{
    [Header("Enemy Stats")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackSpeed = 1.0f;
    [SerializeField] private float targetSearchInterval = 1.0f;

    [Header("References")]
    [SerializeField] private UnitMovement unitMovement;

    private Damageable _target;
    private Coroutine _attackCoroutine;
    private WaitForSeconds _searchWait;

    protected override void Awake()
    {
        base.Awake();
        
        unitMovement = GetComponent<UnitMovement>();
        _searchWait = new WaitForSeconds(targetSearchInterval);
    }

    private IEnumerator Start()
    {
        currentState = UnitState.Idle;

        yield return null;
        
        StartCoroutine(FindTargetCoroutine());
    }

    private void Update()
    {
        DecideNextAction();
    }
    
    private void DecideNextAction()
    {
        if (_target == null || _target.CurrentHealth <= 0)
        {
            HandleTargetLoss();
            return;
        }

        // Check if enemy has reached the interaction cell (where it should attack from)
        bool hasReachedInteractionCell = unitMovement.HasReachedTarget(unitMovement.waypointTolerance + 0.1f);
        
        // Also check distance to target as a fallback (for non-building targets or edge cases)
        float distanceToTarget = GetDistanceToTarget(_target);
        bool isWithinAttackRange = distanceToTarget <= attackRange;

        switch (currentState)
        {
            case UnitState.Idle:
                break;

            case UnitState.Moving:
                // Start attacking when we've reached the interaction cell AND are within attack range
                if (hasReachedInteractionCell && isWithinAttackRange)
                {
                    // Face the target before starting to attack
                    if (_target != null)
                    {
                        AdjustSpriteDirectionToTarget(_target.transform);
                    }
                    StartAttacking();
                }
                break;

            case UnitState.Attacking:
                // Continue facing the target while attacking
                if (_target != null)
                {
                    AdjustSpriteDirectionToTarget(_target.transform);
                }
                
                // Stop attacking if we're no longer at the interaction cell or out of range
                if (!hasReachedInteractionCell || !isWithinAttackRange)
                {
                    StopAttacking();
                    MoveToTarget();
                }
                break;
        }
    }
    
    private float GetDistanceToTarget(Damageable target)
    {
        // For buildings, check distance to the nearest occupied cell, not just the center
        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
        {
            Vector3Int targetCell = BuildingManager.Instance.grid.WorldToCell(target.transform.position);
            if (BuildingManager.Instance.GetBuildingAt(targetCell, out List<Vector3Int> occupiedCells))
            {
                // Find the closest occupied cell to the enemy
                float minDistance = float.MaxValue;
                foreach (Vector3Int cell in occupiedCells)
                {
                    Vector3 cellWorldPos = BuildingManager.Instance.grid.GetCellCenterWorld(cell);
                    float distance = Vector2.Distance(transform.position, cellWorldPos);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                    }
                }
                return minDistance;
            }
        }
        
        // Fallback to center distance for non-buildings or if BuildingManager is unavailable
        return Vector2.Distance(transform.position, target.transform.position);
    }
    
    private void MoveToTarget()
    {
        if (_target == null) return;
        
        // Use SetNewTarget which automatically finds interaction cells for buildings
        // Use a small stopping distance since we want to reach the interaction cell exactly
        if (unitMovement.SetNewTarget(_target.transform.position, unitMovement.waypointTolerance))
        {
            currentState = UnitState.Moving;
        }
        else
        {
            currentState = UnitState.Idle;
        }
    }
    
    private IEnumerator FindTargetCoroutine()
    {
        while (true)
        {
            if (currentState == UnitState.Idle)
            {
                if (TargetManager.Instance != null && TargetManager.Instance.AllTargets.Count > 0)
                {
                    var potentialTargets = TargetManager.Instance.AllTargets
                        .OrderBy(u => Vector2.Distance(transform.position, u.transform.position));

                    Damageable closestTarget = potentialTargets.FirstOrDefault();

                    if (closestTarget != null)
                    {
                        _target = closestTarget;
                        MoveToTarget();
                    }
                }
            }
            yield return _searchWait;
        }
    }

    private void HandleTargetLoss()
    {
        _target = null;
        if (currentState != UnitState.Idle)
        {
            StopAttacking();
            unitMovement.StopMovement();
            currentState = UnitState.Idle;
        }
    }
    
    private void StartAttacking()
    {
        currentState = UnitState.Attacking;
        
        // Snap to the interaction cell center before stopping movement
        unitMovement.StopMovement();
        
        // Face the target building
        if (_target != null)
        {
            AdjustSpriteDirectionToTarget(_target.transform);
        }
        
        if (_attackCoroutine == null)
        {
            _attackCoroutine = StartCoroutine(AttackCoroutine());
        }
    }
    
    private void AdjustSpriteDirectionToTarget(Transform targetTransform)
    {
        if (targetTransform == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null) return;
        if (!TryGetComponent<UnitSpriteController>(out var spriteController)) return;

        Vector3Int unitCell = BuildingManager.Instance.grid.WorldToCell(transform.position);
        Vector3Int targetCell = BuildingManager.Instance.grid.WorldToCell(targetTransform.position);
        
        // For large buildings, find the nearest occupied cell
        if (BuildingManager.Instance.GetBuildingAt(targetCell, out List<Vector3Int> occupiedCells))
        {
            float minDistance = float.MaxValue;
            foreach (Vector3Int cell in occupiedCells)
            {
                float distance = Vector3.Distance(transform.position, BuildingManager.Instance.grid.GetCellCenterWorld(cell));
                if (distance < minDistance)
                {
                    minDistance = distance;
                    targetCell = cell;
                }
            }
        }

        Vector3Int relativePosition = targetCell - unitCell;
        Vector2 targetDirection = Vector2.zero;

        if (relativePosition.x > 0)
        {
            targetDirection = Vector2.right;
        }
        else if (relativePosition.x < 0)
        {
            targetDirection = Vector2.left;
        }
        else if (relativePosition.y > 0)
        {
            targetDirection = Vector2.up;
        }
        else if (relativePosition.y < 0)
        {
            targetDirection = Vector2.down;
        }

        spriteController.UpdateSpriteDirection(targetDirection);
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
        while (currentState == UnitState.Attacking && _target != null)
        {
            _target.TakeDamage(attackDamage);
            
            yield return new WaitForSeconds(1f / attackSpeed);
        }
        _attackCoroutine = null;
    }
}