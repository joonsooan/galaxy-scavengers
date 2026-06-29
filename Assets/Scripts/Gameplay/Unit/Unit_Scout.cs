using DG.Tweening;
using UnityEngine;

public class Unit_Scout : UnitBase
{
    [Header("Scout Settings")]
    [SerializeField] private UnitMovement unitMovement;

    [Header("Hover Animation")]
    [SerializeField] private float hoverHeight = 0.2f;
    [SerializeField] private float hoverDuration = 1.5f;

    private UnitAllyBatteryDriver _allyBatteryDriver;
    private UnitSpriteController _spriteController;
    private Tween _hoverTween;
    private Vector3 _baseHoverLocalPosition;
    private Transform _spriteTransform;

    protected override void Awake()
    {
        base.Awake();
        unitMovement = GetComponent<UnitMovement>();
        _allyBatteryDriver = GetComponent<UnitAllyBatteryDriver>();
    }

    protected void Start()
    {
        _spriteController = GetComponentInChildren<UnitSpriteController>();
        if (_spriteController != null) {
            _spriteTransform = _spriteController.transform;
            _baseHoverLocalPosition = _spriteTransform.localPosition;
        }
    }

    private void Update()
    {
        if (_allyBatteryDriver == null || !_allyBatteryDriver.BlocksWorkLogic) {
            if (currentState == UnitState.Idle) {
                UpdateIdleRoam();
            }
        }

        UpdateAnimationState();
        UpdateHoverAnimation();
        UpdateUnitLightAlpha();
    }

    protected override void OnDestroy()
    {
        StopHover();
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

    private void UpdateHoverAnimation()
    {
        bool shouldHover = _spriteTransform != null &&
                          (currentState == UnitState.Idle || currentState == UnitState.Moving);

        if (shouldHover) {
            if (_hoverTween == null || !_hoverTween.IsActive()) {
                StartHover();
            }
        }
        else {
            if (_hoverTween != null && _hoverTween.IsActive()) {
                StopHover();
            }
        }
    }

    private void StartHover()
    {
        if (_spriteTransform == null) return;
        _spriteTransform.DOKill();
        _hoverTween = null;
        _hoverTween = _spriteTransform.DOLocalMoveY(_baseHoverLocalPosition.y + hoverHeight, hoverDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopHover()
    {
        if (_spriteTransform == null) return;
        _spriteTransform.DOKill();
        _hoverTween = null;
        float currentY = _spriteTransform.localPosition.y;
        float baseY = _baseHoverLocalPosition.y;
        if (Mathf.Abs(currentY - baseY) > 0.01f) {
            _hoverTween = _spriteTransform.DOLocalMoveY(baseY, 0.2f).SetEase(Ease.OutQuad);
        }
    }
}
