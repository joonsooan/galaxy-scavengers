using System.Collections;
using UnityEngine;

public class Unit_Enemy_0 : UnitBase
{
    private enum AIState
    {
        Idle,
        Warning,
        Attack
    }

    [Header("Zones")]
    [SerializeField] private float homeRadius = 3f;
    [SerializeField] private float territoryRadius = 6f;
    [SerializeField] private float chaseTime = 5f;
    [SerializeField] private float detectionRadius = 8f;
    // [SerializeField] private float roamInterval = 2f;
    public float minRoamInterval = 2.0f;
    public float maxRoamInterval = 3.0f;
    private float _currentRoamInterval;

    [Header("Combat")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackSpeed = 1f;

    [Header("References")]
    [SerializeField] private UnitMovement unitMovement;

    private Vector3 _spawnPosition;
    private AIState _aiState;
    private Damageable _buildingTarget;
    private UnitBase _unitTarget;
    
    private float _roamTimer;
    private bool _lastActionWasMove;
    private float _warningTimer;
    
    private Coroutine _attackCoroutine;

    protected override void Awake()
    {
        base.Awake();
        if (unitMovement == null)
        {
            unitMovement = GetComponent<UnitMovement>();
        }
        _spawnPosition = Vector3.zero;
        _aiState = AIState.Idle;
    }
    
    private void Start()
    {
        SetNewRoamInterval();
    }
    
    public void SetTerritoryCenter(Vector3 territoryCenter)
    {
        _spawnPosition = territoryCenter;
    }

    private void Update()
    {
        UpdateStateLogic();
    }

    private void UpdateStateLogic()
    {
        // if (_aiState != AIState.Attack)
        // {
        //     if (IsAnyBuildingInZone(homeRadius) || IsAnyUnitInZone(homeRadius))
        //     {
        //         EnterAttackState();
        //     }
        //     else if (IsAnyBuildingInZone(territoryRadius))
        //     {
        //         EnterAttackState();
        //     }
        // }

        switch (_aiState)
        {
            case AIState.Idle:
                HandleIdle();
                // HandleTerritoryEntry();
                break;
            
            // case AIState.Warning:
            //     HandleWarning();
            //     break;
            //
            // case AIState.Attack:
            //     HandleAttack();
            //     break;
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

    private void HandleTerritoryEntry()
    {
        UnitBase unit = FindClosestAllyInRing(homeRadius, territoryRadius);
        if (unit == null) return;
        
        _unitTarget = unit;
        _buildingTarget = null;
        _aiState = AIState.Warning;
        _warningTimer = 0f;
        
        MoveToCurrentTarget();
    }

    private void HandleWarning()
    {
        _warningTimer += Time.deltaTime;
        
        if (_unitTarget == null)
        {
            ReturnHome();
            return;
        }
        
        float distanceFromSpawn = Vector3.Distance(_spawnPosition, _unitTarget.transform.position);
        
        if (distanceFromSpawn > territoryRadius)
        {
            ReturnHome();
            return;
        }
        if (distanceFromSpawn <= homeRadius || _warningTimer >= chaseTime)
        {
            EnterAttackState();
            return;
        }
        
        MoveToCurrentTarget();
    }

    private void HandleAttack()
    {
        if (_buildingTarget == null && _unitTarget == null)
        {
            SelectAttackTarget();
        }
        if (_buildingTarget == null && _unitTarget == null)
        {
            return;
        }
        
        Vector3 targetPos = _buildingTarget != null ? _buildingTarget.transform.position : _unitTarget.transform.position;
        float distanceToTarget = Vector3.Distance(transform.position, targetPos);
        bool withinRange = distanceToTarget <= attackRange;
        
        if (!withinRange)
        {
            MoveToCurrentTarget();
        }
        else
        {
            if (unitMovement != null)
            {
                unitMovement.StopMovement();
            }
            if (_attackCoroutine == null)
            {
                _attackCoroutine = StartCoroutine(AttackCoroutine());
            }
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

    private void MoveToCurrentTarget()
    {
        if (unitMovement == null) return;
        
        Vector3 targetPos;
        
        if (_buildingTarget != null)
        {
            targetPos = _buildingTarget.transform.position;
        }
        else if (_unitTarget != null)
        {
            targetPos = _unitTarget.transform.position;
        }
        else
        {
            return;
        }
        unitMovement.SetNewTarget(targetPos, unitMovement.waypointTolerance);
    }

    private void EnterAttackState()
    {
        if (_aiState == AIState.Attack) return;
        
        _aiState = AIState.Attack;
        SelectAttackTarget();
        
        if (_attackCoroutine == null)
        {
            _attackCoroutine = StartCoroutine(AttackCoroutine());
        }
    }

    private void ReturnHome()
    {
        _aiState = AIState.Idle;
        _warningTimer = 0f;
        _buildingTarget = null;
        _unitTarget = null;
        
        if (unitMovement != null)
        {
            unitMovement.SetNewTarget(_spawnPosition, unitMovement.waypointTolerance);
        }
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }
    }

    private void SelectAttackTarget()
    {
        _buildingTarget = FindClosestBuildingInRange(detectionRadius);
        _unitTarget = null;
        if (_buildingTarget != null)
        {
            return;
        }
        _unitTarget = FindClosestAllyInRange(detectionRadius);
    }

    private Damageable FindClosestBuildingInRange(float range)
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
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist <= range && dist < bestDistance)
            {
                bestDistance = dist;
                bestTarget = target;
            }
        }
        return bestTarget;
    }

    private UnitBase FindClosestAllyInRange(float range)
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
            float dist = Vector3.Distance(transform.position, unit.transform.position);
            if (dist <= range && dist < bestDistance)
            {
                bestDistance = dist;
                bestUnit = unit;
            }
        }
        return bestUnit;
    }

    private UnitBase FindClosestAllyInRing(float innerRadius, float outerRadius)
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
            if (distFromSpawn > innerRadius && distFromSpawn <= outerRadius && distFromSpawn < bestDistance)
            {
                bestDistance = distFromSpawn;
                bestUnit = unit;
            }
        }
        return bestUnit;
    }

    private bool IsAnyBuildingInZone(float radius)
    {
        if (TargetManager.Instance == null || TargetManager.Instance.AllTargets.Count == 0)
        {
            return false;
        }
        foreach (Damageable target in TargetManager.Instance.AllTargets)
        {
            if (target == null) continue;
            if (target.GetComponent<UnitBase>() != null) continue;
            float distFromSpawn = Vector3.Distance(_spawnPosition, target.transform.position);
            
            if (distFromSpawn <= radius)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsAnyUnitInZone(float radius)
    {
        if (UnitManager.Instance == null || UnitManager.Instance.AllyUnits == null || UnitManager.Instance.AllyUnits.Count == 0)
        {
            return false;
        }
        foreach (UnitBase unit in UnitManager.Instance.AllyUnits)
        {
            if (unit == null) continue;
            float distFromSpawn = Vector3.Distance(_spawnPosition, unit.transform.position);
            if (distFromSpawn <= radius)
            {
                return true;
            }
        }
        return false;
    }

    private IEnumerator AttackCoroutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f / attackSpeed);
        while (_aiState == AIState.Attack)
        {
            if (_buildingTarget == null && _unitTarget == null)
            {
                SelectAttackTarget();
            }
            if (_buildingTarget != null)
            {
                if (_buildingTarget.CurrentHealth > 0)
                {
                    _buildingTarget.TakeDamage(attackDamage);
                }
                else
                {
                    _buildingTarget = null;
                }
            }
            else if (_unitTarget != null)
            {
                _unitTarget.TakeDamage(attackDamage);
                if (_unitTarget.currentHealth <= 0f)
                {
                    _unitTarget = null;
                }
            }
            yield return wait;
        }
        _attackCoroutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? _spawnPosition : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, homeRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, territoryRadius);
    }
}


