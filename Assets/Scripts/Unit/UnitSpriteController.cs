using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class UnitSpriteController : MonoBehaviour
{
    private Animator _animator;
    private SpriteRenderer _sr;
    
    private static readonly int InputXHash = Animator.StringToHash("InputX");
    private static readonly int InputYHash = Animator.StringToHash("InputY");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsMiningHash = Animator.StringToHash("IsMining");
    private static readonly int CargoFillHash = Animator.StringToHash("CargoFill");
    
    private Vector2 _lastDirection = Vector2.down;
    
    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _sr = GetComponent<SpriteRenderer>();
    }

    public void UpdateSpriteDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.01f) return;
        _lastDirection = direction.normalized;

        _animator.SetFloat(InputXHash, _lastDirection.x);
        _animator.SetFloat(InputYHash, _lastDirection.y);

        if (_lastDirection.x < -0.01f)
        {
            _sr.flipX = true;
        }
        else if (_lastDirection.x > 0.01f)
        {
            _sr.flipX = false;
        }
    }
    
    public void UpdateAnimationState(UnitBase.UnitState currentState)
    {
        bool isMoving = currentState == UnitBase.UnitState.Moving || currentState == UnitBase.UnitState.ReturningToStorage;
        bool isMining = currentState == UnitBase.UnitState.Mining;

        _animator.SetBool(IsMovingHash, isMoving);
        _animator.SetBool(IsMiningHash, isMining);
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
}
