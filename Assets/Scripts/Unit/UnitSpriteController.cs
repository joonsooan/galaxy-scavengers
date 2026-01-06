using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class UnitSpriteController : MonoBehaviour
{
    private Animator _animator;
    private SpriteRenderer _sr;
    private UnitBase _unitBase;
    private UnitMovement _unitMovement;
    
    private static readonly int InputXHash = Animator.StringToHash("InputX");
    private static readonly int InputYHash = Animator.StringToHash("InputY");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsMiningHash = Animator.StringToHash("IsMining");
    private static readonly int IsConstructingHash = Animator.StringToHash("IsConstructing");
    private static readonly int IsProcessingHash = Animator.StringToHash("IsProcessing");
    private static readonly int IsPatrollingHash = Animator.StringToHash("IsPatrolling");
    private static readonly int CargoFillHash = Animator.StringToHash("CargoFill");
    
    private Vector2 _lastDirection = Vector2.down;
    private Transform _targetTransform;
    private Vector3? _targetPosition;
    private float _lastUpdateTime;
    private float _lastDirectionUpdateTime;
    private const float UpdateThrottle = 0.05f; // Update direction at most every 0.05 seconds
    private const float DirectionUpdateThrottle = 0.1f; // Throttle direction updates to prevent rapid changes
    
    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _sr = GetComponent<SpriteRenderer>();
        _unitBase = GetComponent<UnitBase>();
        _unitMovement = GetComponent<UnitMovement>();
        
        // Ensure animator speed is normalized to prevent speed issues
        if (_animator != null)
        {
            _animator.speed = 1.0f;
        }
    }

    public void UpdateSpriteDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.01f) return;
        
        // Throttle direction updates to prevent rapid changes that cause animation speed issues
        if (Time.time - _lastDirectionUpdateTime < DirectionUpdateThrottle)
        {
            return;
        }
        
        // Ensure animator speed stays at 1.0
        if (_animator.speed != 1.0f)
        {
            _animator.speed = 1.0f;
        }
        
        _lastDirection = direction.normalized;

        float x = _lastDirection.x;
        float y = _lastDirection.y;
        
        if (Mathf.Abs(x) > 0.3f && Mathf.Abs(y) > 0.3f)
        {
            y = 0f;
        }
        else if (Mathf.Abs(y) > Mathf.Abs(x))
        {
            x = 0f;
            y = y > 0 ? 1f : -1f;
        }
        else
        {
            y = 0f;
            x = x > 0 ? 1f : -1f;
        }
        
        _animator.SetFloat(InputXHash, x);
        _animator.SetFloat(InputYHash, y);
        _lastDirectionUpdateTime = Time.time;
    }
    
    public void UpdateAnimationState(
        UnitBase.UnitState currentState, 
        bool? isMining = null, 
        bool? isConstructing = null,
        bool? isProcessing = null,
        bool? isPatrolling = null)
    {
        bool isMoving = currentState == UnitBase.UnitState.Moving || currentState == UnitBase.UnitState.ReturningToStorage;
        
        // Ensure animator speed stays at 1.0 to prevent speed issues
        if (_animator.speed != 1.0f)
        {
            _animator.speed = 1.0f;
        }
        
        _animator.SetBool(IsMovingHash, isMoving);
        
        // Only update mining state if explicitly provided (for Unit_Miner)
        if (isMining.HasValue)
        {
            _animator.SetBool(IsMiningHash, isMining.Value);
        }
        
        // Only update constructing state if explicitly provided (for Unit_Construct)
        if (isConstructing.HasValue)
        {
            _animator.SetBool(IsConstructingHash, isConstructing.Value);
        }
        
        // Only update processing state if explicitly provided (for processor drones)
        if (isProcessing.HasValue)
        {
            _animator.SetBool(IsProcessingHash, isProcessing.Value);
        }
        
        // Only update patrolling state if explicitly provided (for scouts)
        if (isPatrolling.HasValue)
        {
            _animator.SetBool(IsPatrollingHash, isPatrolling.Value);
        }
    }
    
    public void UpdateCargoFill(int currentAmount, int maxCapacity)
    {
        float fillPercent = 0f;
        if (maxCapacity > 0)
        {
            fillPercent = (float)currentAmount / maxCapacity;
        }
        _animator.SetFloat(CargoFillHash, fillPercent);
    }
    
    public void SetTargetTransform(Transform target)
    {
        _targetTransform = target;
        _targetPosition = null;
    }
    
    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
        _targetTransform = null;
    }
    
    public void ClearTarget()
    {
        _targetTransform = null;
        _targetPosition = null;
    }
    
    private void Update()
    {
        // Throttle updates to prevent too frequent direction changes
        if (Time.time - _lastUpdateTime < UpdateThrottle)
        {
            return;
        }
        
        // Ensure animator speed stays at 1.0
        if (_animator.speed != 1.0f)
        {
            _animator.speed = 1.0f;
        }
        
        // Only update direction to target if not moving
        bool isMoving = _unitMovement != null && _unitMovement.IsMoving;
        
        if (!isMoving)
        {
            Vector3? targetPos = null;
            
            if (_targetTransform != null)
            {
                targetPos = _targetTransform.position;
            }
            else if (_targetPosition.HasValue)
            {
                targetPos = _targetPosition.Value;
            }
            
            if (targetPos.HasValue)
            {
                Vector2 direction = (targetPos.Value - transform.position).normalized;
                if (direction.sqrMagnitude > 0.01f)
                {
                    UpdateSpriteDirection(direction);
                    _lastUpdateTime = Time.time;
                }
            }
        }
    }
}
