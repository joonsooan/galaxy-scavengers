using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class Unit_Construct : UnitBase
{
    private const float RepathInterval = 0.5f;
    private const float TaskRequestCooldown = 1.0f;

    [Header("Construct Drone Settings")]
    [SerializeField] public int carryCapacity = 10;
    [SerializeField] private float loadingTime = 1f;
    [SerializeField] private float unloadingTime = 1f;
    [SerializeField] public UnitMovement movement;

    [Header("VFX")]
    [SerializeField] private ParticleSystem constructingParticleSystem;
    [SerializeField] private float particleOffsetDistance = 0.5f;
    [SerializeField] private float yOffset;

    [Header("Hover Animation")]
    [SerializeField] private float hoverHeight = 0.2f;
    [SerializeField] private float hoverDuration = 1.5f;
    private Vector3 _baseHoverLocalPosition;

    private int _carriedAmount;
    private ResourceType _carriedResourceType;

    private Vector3 _currentConstructionDirection;

    private ConstructionSite _currentConstructionSite;
    private ConstructionSite.ConstructionRequest _currentRequest;
    private ConstructState _currentState = ConstructState.Idle;
    private Tween _hoverTween;

    private Coroutine _loadingCoroutine;

    private float _nextRepathTime;
    private float _nextTaskRequestTime;

    private Rigidbody2D _rb;
    private UnitSpriteController _spriteController;
    private Transform _spriteTransform;

    private IStorage _targetStorage;
    private Coroutine _unloadingCoroutine;

    protected override void Awake()
    {
        base.Awake();
        movement = GetComponent<UnitMovement>();
        _rb = GetComponent<Rigidbody2D>();
    }

    protected void Start()
    {
        if (ConstructionManager.Instance != null && movement != null) {
            ConstructionManager.Instance.RegisterConstructDrone(this);
        }
        _spriteController = GetComponentInChildren<UnitSpriteController>();
        if (_spriteController != null) {
            _spriteTransform = _spriteController.transform;
            _baseHoverLocalPosition = _spriteTransform.localPosition;
        }
    }

    private void Update()
    {
        UpdateUnitBaseState();
        DecideNextAction();
        UpdateAnimationState();
        UpdateHoverAnimation();

        if (currentState == UnitState.Constructing) {
            UpdateParticlePosition();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        UnitManager.Instance?.AddUnit(this);

        if (ConstructionManager.Instance != null) {
            ConstructionManager.Instance.RegisterConstructDrone(this);
        }
    }


    protected override void OnDisable()
    {
        base.OnDisable();
        UnitManager.Instance?.RemoveUnit(this);
        ConstructionManager.Instance?.UnregisterConstructDrone(this);
        ReleaseFromConstruction();
    }

    private void OnDestroy()
    {
        StopConstructionParticles();
        StopHover();
    }

    private void UpdateUnitBaseState()
    {
        switch (_currentState) {
        case ConstructState.Idle:
            currentState = UnitState.Idle;
            break;
        case ConstructState.FetchingResource:
            // When loading, use Idle state (not Unloading) to prevent animation speed issues
            if (movement != null && movement.IsMoving) {
                currentState = UnitState.Moving;
            }
            else if (_loadingCoroutine != null) {
                // Loading from storage - use Idle state
                currentState = UnitState.Idle;
            }
            else {
                currentState = UnitState.Idle;
            }
            break;
        case ConstructState.DeliveringResource:
            if (movement != null && movement.IsMoving) {
                currentState = UnitState.Moving;
            }
            else if (_unloadingCoroutine != null) {
                currentState = UnitState.Constructing;
            }
            else {
                // Waiting at delivery location - use Idle state
                currentState = UnitState.Idle;
            }
            break;
        }
    }

    private void DecideNextAction()
    {
        switch (_currentState) {
        case ConstructState.Idle:
            UpdateIdle();
            break;

        case ConstructState.FetchingResource:
            UpdateFetching();
            break;

        case ConstructState.DeliveringResource:
            UpdateDelivering();
            break;
        }
    }

    private void UpdateAnimationState()
    {
        if (_spriteController != null) {
            bool isConstructing = currentState == UnitState.Constructing;
            _spriteController.UpdateAnimationState(currentState, isConstructing: isConstructing);
        }

        bool isLoading = _loadingCoroutine != null;

        if (isLoading) {
            return;
        }

        if (currentState == UnitState.Moving || currentState == UnitState.ReturningToStorage) {
            Vector3 moveDir = movement.GetMoveDirection();
            _spriteController?.UpdateSpriteDirection(moveDir);
            _spriteController?.ClearTarget(); // Clear target when moving
        }
        else if (currentState == UnitState.Constructing && _currentRequest != null && _currentRequest.site != null) {
            if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null) {
                Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
                Vector3 piecePos = BuildingManager.Instance.grid.GetCellCenterWorld(targetPieceCell);
                _spriteController?.SetTargetPosition(piecePos);
            }
        }
        else if (currentState == UnitState.Unloading && _targetStorage != null) {
            _spriteController?.SetTargetTransform((_targetStorage as Component).transform);
        }
        else if (currentState == UnitState.Idle) {
            if (_currentRequest != null && _currentRequest.site != null) {
                if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null) {
                    Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
                    Vector3 piecePos = BuildingManager.Instance.grid.GetCellCenterWorld(targetPieceCell);
                    _spriteController?.SetTargetPosition(piecePos);
                }
            }
            else {
                _spriteController?.ClearTarget();
            }
        }
    }

    public bool IsIdle()
    {
        return _currentState == ConstructState.Idle;
    }

    private void UpdateIdle()
    {
        if (Time.time < _nextTaskRequestTime) {
            return;
        }

        ConstructionManager.Instance?.RequestTask(this);
    }

    private void UpdateFetching()
    {
        if (_targetStorage == null || _currentRequest == null) {
            SetTask_Idle();
            return;
        }

        if (!movement.IsMoving) {
            if (_targetStorage != null && _currentRequest != null) {
                int availableAmount = _targetStorage.GetCurrentResourceAmount(_currentRequest.type);
                if (availableAmount < _currentRequest.amount) {
                    _currentRequest?.site?.CancelRequest(_currentRequest);
                    SetTask_Idle();
                    return;
                }
            }

            if (_loadingCoroutine == null) {
                _loadingCoroutine = StartCoroutine(LoadingResourceCoroutine());
            }
        }
    }

    private IEnumerator LoadingResourceCoroutine()
    {
        if (_targetStorage != null) {
            if (TryGetComponent(out UnitSpriteController spriteController)) {
                Vector2 direction = (((Component)_targetStorage).transform.position - transform.position).normalized;
                if (direction.sqrMagnitude > 0.01f) {
                    spriteController.UpdateSpriteDirection(direction);
                }
                spriteController.SetTargetTransform((_targetStorage as Component).transform);
            }
        }

        movement.ForceStopAllMovement();

        // Show progress bar during loading
        ShowProgressBar();
        float elapsedTime = 0f;

        while (elapsedTime < loadingTime) {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / loadingTime;
            UpdateProgressBar(progress);
            yield return null;
        }

        HideProgressBar();

        if (_targetStorage == null || _currentRequest == null) {
            SetTask_Idle();
            _loadingCoroutine = null;
            yield break;
        }

        // Validate again that the storage still has resources before withdrawing
        int availableAmount = _targetStorage.GetCurrentResourceAmount(_currentRequest.type);
        if (availableAmount < _currentRequest.amount) {
            // Storage no longer has enough resources, cancel task
            _currentRequest?.site?.CancelRequest(_currentRequest);
            SetTask_Idle();
            _loadingCoroutine = null;
            yield break;
        }

        if (_targetStorage.TryWithdrawResource(_currentRequest.type, _currentRequest.amount, out int withdrawnAmount) && withdrawnAmount > 0) {
            _carriedResourceType = _currentRequest.type;
            _carriedAmount = withdrawnAmount;

            if (!CanDepositResource()) {
                if (_targetStorage != null) {
                    _carriedAmount = 0;
                }
                SetTask_Idle();
                _loadingCoroutine = null;
                yield break;
            }

            Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
            Vector3 interactionPos = _currentRequest.site.AssignDeliveryInteractionCell(this, targetPieceCell);
            movement.ResumeMovement();
            movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
            _currentState = ConstructState.DeliveringResource;
        }
        else {
            SetTask_Idle();
        }

        _loadingCoroutine = null;
    }

    private void UpdateDelivering()
    {
        if (_currentRequest == null || _currentRequest.site == null) {
            SetTask_Idle();
            return;
        }

        if (_unloadingCoroutine != null) {
            return;
        }

        if (!CanDepositResource()) {
            SetTask_Idle();
            return;
        }

        bool isAtDelivery = IsAtTarget();

        if (isAtDelivery) {
            if (!CanDepositResource()) {
                SetTask_Idle();
                return;
            }

            _unloadingCoroutine = StartCoroutine(UnloadingResourceCoroutine());
        }
        else if (ShouldRepath()) {
            TryRepathToDelivery();
        }
    }

    private bool CanDepositResource()
    {
        if (_currentRequest == null || _currentRequest.site == null || _carriedAmount <= 0) {
            return false;
        }

        return _currentRequest.site.CanDepositResource(_carriedResourceType, this);
    }

    private IEnumerator UnloadingResourceCoroutine()
    {
        if (_currentRequest == null || _currentRequest.site == null) {
            StopConstructionParticles();
            _unloadingCoroutine = null;
            SetTask_Idle();
            yield break;
        }

        if (!CanDepositResource()) {
            StopConstructionParticles();
            _unloadingCoroutine = null;
            SetTask_Idle();
            yield break;
        }

        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null) {
            Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
            Vector3 piecePos = BuildingManager.Instance.grid.GetCellCenterWorld(targetPieceCell);
            if (TryGetComponent(out UnitSpriteController spriteController)) {
                Vector2 direction = (piecePos - transform.position).normalized;
                if (direction.sqrMagnitude > 0.01f) {
                    spriteController.UpdateSpriteDirection(direction);
                }
                spriteController.SetTargetPosition(piecePos);
            }

            AdjustConstructionDirection(piecePos);
        }

        movement.ForceStopAllMovement();

        StartConstructionParticles();

        // Show progress bar during constructing (unloading)
        ShowProgressBar();
        float elapsedTime = 0f;

        while (elapsedTime < unloadingTime) {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / unloadingTime;
            UpdateProgressBar(progress);
            yield return null;
        }

        HideProgressBar();

        if (!CanDepositResource()) {
            StopConstructionParticles();
            _unloadingCoroutine = null;
            SetTask_Idle();
            yield break;
        }

        StopConstructionParticles();
        _unloadingCoroutine = null;

        if (_currentRequest != null && _currentRequest.site != null) {
            ConstructionSite site = _currentRequest.site;
            ResourceType resourceType = _carriedResourceType;
            int resourceAmount = _carriedAmount;

            _currentRequest = null;
            _carriedAmount = 0;

            site.TryDepositResource(resourceType, resourceAmount, this);
            site.ReleaseDrone(this);

            _currentState = ConstructState.Idle;
            ConstructionManager.Instance?.OnSiteResourceDelivered(site);
        }
        else {
            _currentState = ConstructState.Idle;
        }
    }


    private void AdjustSpriteDirectionToPosition(Vector3 targetPosition)
    {
        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null) return;
        if (!TryGetComponent(out UnitSpriteController spriteController)) return;

        Vector3Int unitCell = BuildingManager.Instance.grid.WorldToCell(transform.position);
        Vector3Int targetCell = BuildingManager.Instance.grid.WorldToCell(targetPosition);
        Vector2 direction = CalculateDirection(unitCell, targetCell);
        spriteController.UpdateSpriteDirection(direction);
    }

    private void AdjustSpriteDirectionToBuilding(Transform buildingTransform)
    {
        if (buildingTransform == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null) return;
        if (!TryGetComponent(out UnitSpriteController spriteController)) return;

        Vector3Int unitCell = BuildingManager.Instance.grid.WorldToCell(transform.position);
        Vector3Int buildingCell = BuildingManager.Instance.grid.WorldToCell(buildingTransform.position);
        Vector2 direction = CalculateDirection(unitCell, buildingCell);
        spriteController.UpdateSpriteDirection(direction);
    }

    private bool IsAtTarget()
    {
        return movement.HasReachedTarget(movement.waypointTolerance + 0.1f) &&
            !movement.IsMoving &&
            _rb.linearVelocity.sqrMagnitude < 0.01f;
    }

    private Vector2 CalculateDirection(Vector3Int from, Vector3Int to)
    {
        Vector3Int relativePosition = to - from;

        if (relativePosition.x > 0) return Vector2.right;
        if (relativePosition.x < 0) return Vector2.left;
        if (relativePosition.y > 0) return Vector2.up;
        if (relativePosition.y < 0) return Vector2.down;

        return Vector2.zero;
    }

    private bool ShouldRepath()
    {
        return !movement.IsMoving && _rb.linearVelocity.sqrMagnitude < 0.01f && Time.time >= _nextRepathTime;
    }

    private void TryRepathToDelivery()
    {
        if (_currentRequest == null || _currentRequest.site == null) {
            SetTask_Idle();
            return;
        }

        _nextRepathTime = Time.time + RepathInterval;
        Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
        Vector3 interactionPos = _currentRequest.site.AssignDeliveryInteractionCell(this, targetPieceCell);
        bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
        if (!hasPath) {
            SetTask_Idle();
        }
    }

    public void SetTask_FetchResource(ConstructionSite.ConstructionRequest request, ConstructionSite site)
    {
        if (movement == null) {
            if (request != null && request.site != null) {
                request.site.CancelRequest(request);
            }
            return;
        }

        _currentRequest = request;
        _targetStorage = null;

        if (ResourceManager.Instance != null) {
            List<IStorage> storages = ResourceManager.Instance.GetAllStorages();
            if (storages != null && storages.Count > 0) {
                List<IStorage> candidates = new List<IStorage>();
                foreach (IStorage storage in storages) {
                    if (storage == null) continue;
                    if (storage.GetCurrentResourceAmount(request.type) <= 0) continue;
                    candidates.Add(storage);
                }

                if (candidates.Count > 0) {
                    candidates.Sort((a, b) =>
                        Vector3.Distance(site.GetPosition(), a.GetPosition())
                            .CompareTo(Vector3.Distance(site.GetPosition(), b.GetPosition())));

                    foreach (IStorage storage in candidates) {
                        bool hasPath = movement.SetNewTarget(storage.GetPosition());
                        if (hasPath) {
                            _targetStorage = storage;
                            _currentState = ConstructState.FetchingResource;
                            return;
                        }
                    }
                }
            }
        }

        _currentRequest?.site?.CancelRequest(_currentRequest);
        _nextTaskRequestTime = Time.time + TaskRequestCooldown;
        SetTask_Idle();
    }


    private void SetTask_Idle()
    {
        ReleaseFromConstruction();
        _currentState = ConstructState.Idle;
    }

    public void SetTaskRequestCooldown(float duration)
    {
        _nextTaskRequestTime = Time.time + duration;
    }

    private void ReleaseFromConstruction()
    {
        StopConstructionParticles();

        if (_currentRequest != null) {
            _currentRequest.site?.CancelRequest(_currentRequest);
            _currentRequest = null;
        }

        if (_currentConstructionSite != null) {
            _currentConstructionSite.ReleaseDrone(this);
            _currentConstructionSite = null;
        }

        StopAllCoroutines();
        _loadingCoroutine = null;
        _unloadingCoroutine = null;
    }

    private void AdjustConstructionDirection(Vector3 targetPosition)
    {
        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null) return;

        Vector3Int unitCell = BuildingManager.Instance.grid.WorldToCell(transform.position);
        Vector3Int targetCell = BuildingManager.Instance.grid.WorldToCell(targetPosition);
        Vector3Int relativePosition = targetCell - unitCell;

        Vector3 targetDirection = Vector3.zero;

        if (relativePosition.x > 0) {
            targetDirection = Vector3.right;
        }
        else if (relativePosition.x < 0) {
            targetDirection = Vector3.left;
        }
        else if (relativePosition.y > 0) {
            targetDirection = Vector3.up;
        }
        else if (relativePosition.y < 0) {
            targetDirection = Vector3.down;
        }

        _currentConstructionDirection = targetDirection;
    }

    private void StartConstructionParticles()
    {
        if (constructingParticleSystem != null) {
            UpdateParticlePosition();
            if (!constructingParticleSystem.isPlaying) {
                constructingParticleSystem.Play();
            }
        }
    }

    private void StopConstructionParticles()
    {
        if (constructingParticleSystem != null && constructingParticleSystem.isPlaying) {
            constructingParticleSystem.Stop();
        }
    }

    private void UpdateParticlePosition()
    {
        if (constructingParticleSystem == null) return;

        Transform particleTransform = constructingParticleSystem.transform;
        Vector3 offsetPosition = transform.position + _currentConstructionDirection * particleOffsetDistance - new Vector3(0, yOffset, 0);
        particleTransform.position = offsetPosition;
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

    private enum ConstructState
    {
        Idle,
        FetchingResource,
        DeliveringResource
    }
}
