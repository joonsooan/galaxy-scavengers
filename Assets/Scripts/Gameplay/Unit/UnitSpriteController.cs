using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class UnitSpriteController : MonoBehaviour
{
    private const float UpdateThrottle = 0.05f;
    private const float DirectionUpdateThrottle = 0.1f;

    private static readonly int InputXHash = Animator.StringToHash("InputX");
    private static readonly int InputYHash = Animator.StringToHash("InputY");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsMiningHash = Animator.StringToHash("IsMining");
    private static readonly int IsConstructingHash = Animator.StringToHash("IsConstructing");
    private static readonly int IsProcessingHash = Animator.StringToHash("IsProcessing");
    private static readonly int IsPatrollingHash = Animator.StringToHash("IsPatrolling");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int CargoFillHash = Animator.StringToHash("CargoFill");

    private Animator _animator;
    private Vector2 _lastDirection = Vector2.down;
    private float _lastDirectionUpdateTime;
    private float _lastUpdateTime;
    private Vector3? _targetPosition;
    private Transform _targetTransform;
    private UnitMovement _unitMovement;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _unitMovement = GetComponentInParent<UnitMovement>();

        if (_animator != null) {
            _animator.speed = 1.0f;
        }
    }

    private void Update()
    {
        if (Time.time - _lastUpdateTime < UpdateThrottle) {
            return;
        }

        if (_animator.speed != 1.0f) {
            _animator.speed = 1.0f;
        }

        Unit_Processor parentDrone = GetComponentInParent<Unit_Processor>();
        if (parentDrone != null) {
            return;
        }

        bool isMoving = _unitMovement != null && _unitMovement.IsMoving;

        if (!isMoving) {
            Vector3? targetPos = null;

            if (_targetTransform != null) {
                targetPos = _targetTransform.position;
            }
            else if (_targetPosition.HasValue) {
                targetPos = _targetPosition.Value;
            }
            
            if (targetPos.HasValue) {
                Vector2 direction = (targetPos.Value - transform.position).normalized;
                if (direction.sqrMagnitude > 0.01f) {
                    UpdateSpriteDirection(direction);
                    _lastUpdateTime = Time.time;
                }
            }
        }
    }

    public void UpdateSpriteDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.01f) return;

        if (Time.time - _lastDirectionUpdateTime < DirectionUpdateThrottle) {
            return;
        }

        if (_animator.speed != 1.0f) {
            _animator.speed = 1.0f;
        }

        _lastDirection = direction.normalized;

        float x = _lastDirection.x;
        float y = _lastDirection.y;

        float absX = Mathf.Abs(x);
        float absY = Mathf.Abs(y);

        if (absX > 0.3f && absY > 0.3f) {
            y = 0f;
            x = x > 0 ? 1f : -1f;
        }
        else if (absX > absY) {
            y = 0f;
            x = x > 0 ? 1f : -1f;
        }
        else {
            x = 0f;
            y = y > 0 ? 1f : -1f;
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
        bool? isPatrolling = null,
        bool? isAttacking = null
    )
    {
        bool isMoving = currentState == UnitBase.UnitState.Moving || currentState == UnitBase.UnitState.ReturningToStorage;

        if (_animator.speed != 1.0f) {
            _animator.speed = 1.0f;
        }

        _animator.SetBool(IsMovingHash, isMoving);

        if (isMining.HasValue) {
            _animator.SetBool(IsMiningHash, isMining.Value);
        }

        if (isConstructing.HasValue) {
            _animator.SetBool(IsConstructingHash, isConstructing.Value);
        }

        if (isProcessing.HasValue) {
            _animator.SetBool(IsProcessingHash, isProcessing.Value);
        }

        if (isPatrolling.HasValue) {
            _animator.SetBool(IsPatrollingHash, isPatrolling.Value);
        }

        if (isAttacking.HasValue) {
            _animator.SetBool(IsAttackingHash, isAttacking.Value);
        }
    }

    public void UpdateCargoFill(int currentAmount, int maxCapacity)
    {
        float fillPercent = 0f;
        if (maxCapacity > 0) {
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

    public float GetCurrentAnimationLength()
    {
        if (_animator != null && _animator.isInitialized) {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.length;
        }
        return 0f;
    }
}
