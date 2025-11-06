using System.Collections;
using System.Linq;
using UnityEngine;

public class Unit_Enemy_0 : UnitBase
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

        float distanceToTarget = Vector2.Distance(transform.position, _target.transform.position);

        switch (currentState)
        {
            case UnitState.Idle:
                break;

            case UnitState.Moving:
                if (distanceToTarget <= attackRange)
                {
                    StartAttacking();
                }
                break;

            case UnitState.Attacking:
                if (distanceToTarget > attackRange)
                {
                    StopAttacking();
                    MoveToTarget();
                }
                break;
        }
    }
    
    private void MoveToTarget()
    {
        if (_target == null) return;
        
        if (unitMovement.SetNewTarget(_target.transform.position, attackRange * 0.9f))
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
        unitMovement.StopMovement();
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
        while (currentState == UnitState.Attacking && _target != null)
        {
            _target.TakeDamage(attackDamage);
            
            yield return new WaitForSeconds(1f / attackSpeed);
        }
        _attackCoroutine = null;
    }
}