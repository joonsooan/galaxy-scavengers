using System;
using DG.Tweening;
using UnityEngine;

public class Unit_Scout : UnitBase
{
    [Header("Scout Settings")]
    [SerializeField] private UnitMovement unitMovement;

    [Header("Hover Animation")]
    [SerializeField] private float hoverHeight = 0.2f;
    [SerializeField] private float hoverDuration = 1.5f;

    private UnitSpriteController _spriteController;
    private Tween _hoverTween;
    private Vector3 _baseHoverLocalPosition;
    private Transform _spriteTransform;

#pragma warning disable CS0067
    public static event Action<Vector3> OnScoutEnteredLocation;
#pragma warning restore CS0067

    protected override void Awake()
    {
        base.Awake();
        unitMovement = GetComponent<UnitMovement>();
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
        if (currentState == UnitState.Idle) {
            UpdateIdleRoam();
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
        StopHover();
        if (_spriteTransform == null) return;

        _baseHoverLocalPosition = _spriteTransform.localPosition;
        _hoverTween = _spriteTransform.DOLocalMoveY(_baseHoverLocalPosition.y + hoverHeight, hoverDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopHover()
    {
        if (_hoverTween != null && _hoverTween.IsActive()) {
            _hoverTween.Kill();
            _hoverTween = null;
        }

        if (_spriteTransform != null) {
            float currentY = _spriteTransform.localPosition.y;
            float baseY = _baseHoverLocalPosition.y;
            if (Mathf.Abs(currentY - baseY) > 0.01f) {
                _spriteTransform.DOLocalMoveY(baseY, 0.2f).SetEase(Ease.OutQuad);
            }
        }
    }
}
