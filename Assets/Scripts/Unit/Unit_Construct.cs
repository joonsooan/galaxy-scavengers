using System.Collections;
using UnityEngine;
using DG.Tweening;

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
    
    [Header("Floating Animation")]
    [SerializeField] private float floatAmplitude = 0.1f;
    [SerializeField] private float floatDuration = 2f;

    private int _carriedAmount;
    private ResourceType _carriedResourceType;

    private ConstructionSite _currentConstructionSite;
    private ConstructionSite.ConstructionRequest _currentRequest;
    private ConstructState _currentState = ConstructState.Idle;

    private Coroutine _loadingCoroutine;

    private float _nextRepathTime;
    private float _nextTaskRequestTime;

    private Rigidbody2D _rb;

    private IStorage _targetStorage;
    private Coroutine _unloadingCoroutine;
    private UnitSpriteController _spriteController;
    
    private Tween _floatingTween;
    private float _floatingPhase;
    private Vector3 _currentConstructionDirection;
    private float _startYPosition;

    protected override void Awake()
    {
        base.Awake();
        movement = GetComponent<UnitMovement>();
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        if (ConstructionManager.Instance != null && movement != null)
        {
            ConstructionManager.Instance.RegisterConstructDrone(this);
        }
        _spriteController = GetComponent<UnitSpriteController>();
    }

    private void Update()
    {
        UpdateUnitBaseState();
        DecideNextAction();
        UpdateAnimationState();
        
        // Handle floating animation for Idle and Moving states
        if (currentState == UnitState.Idle || currentState == UnitState.Moving)
        {
            if (_floatingTween == null || !_floatingTween.IsActive())
            {
                StartFloatingAnimation();
            }
            else if (_floatingTween.IsActive())
            {
                // Apply floating offset to current position each frame
                ApplyFloatingOffset();
            }
        }
        else
        {
            StopFloatingAnimation();
        }
        
        if (currentState == UnitState.Constructing)
        {
            UpdateParticlePosition();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        UnitManager.Instance?.AddUnit(this);
        
        if (ConstructionManager.Instance != null)
        {
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
        StopFloatingAnimation();
        StopConstructionParticles();
    }

    private void UpdateUnitBaseState()
    {
        switch (_currentState)
        {
            case ConstructState.Idle:
                currentState = UnitState.Idle;
                break;
            case ConstructState.FetchingResource:
                // When loading, use Idle state (not Unloading) to prevent animation speed issues
                if (movement != null && movement.IsMoving)
                {
                    currentState = UnitState.Moving;
                }
                else if (_loadingCoroutine != null)
                {
                    // Loading from storage - use Idle state
                    currentState = UnitState.Idle;
                }
                else
                {
                    currentState = UnitState.Idle;
                }
                break;
            case ConstructState.DeliveringResource:
                if (movement != null && movement.IsMoving)
                {
                    currentState = UnitState.Moving;
                }
                else if (_unloadingCoroutine != null)
                {
                    currentState = UnitState.Constructing;
                }
                else
                {
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
        if (_spriteController != null)
        {
            bool isConstructing = currentState == UnitState.Constructing;
            _spriteController.UpdateAnimationState(currentState, isConstructing: isConstructing);
        }
        
        if (currentState == UnitState.Moving || currentState == UnitState.ReturningToStorage)
        {
            Vector3 moveDir = movement.GetMoveDirection();
            _spriteController?.UpdateSpriteDirection(moveDir);
            _spriteController?.ClearTarget(); // Clear target when moving
        }
        else if (currentState == UnitState.Constructing && _currentRequest != null && _currentRequest.site != null)
        {
            if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
            {
                Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
                Vector3 piecePos = BuildingManager.Instance.grid.GetCellCenterWorld(targetPieceCell);
                _spriteController?.SetTargetPosition(piecePos);
            }
        }
        else if (currentState == UnitState.Unloading && _targetStorage != null)
        {
            _spriteController?.SetTargetTransform((_targetStorage as Component).transform);
        }
        else if (currentState == UnitState.Idle)
        {
            if (_currentRequest != null && _currentRequest.site != null)
            {
                if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
                {
                    Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
                    Vector3 piecePos = BuildingManager.Instance.grid.GetCellCenterWorld(targetPieceCell);
                    _spriteController?.SetTargetPosition(piecePos);
                }
            }
            else
            {
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
        if (Time.time < _nextTaskRequestTime)
        {
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
            if (_loadingCoroutine == null) {
                _loadingCoroutine = StartCoroutine(LoadingResourceCoroutine());
            }
        }
    }

    private IEnumerator LoadingResourceCoroutine()
    {
        movement.ForceStopAllMovement();

        if (_targetStorage != null)
        {
            if (TryGetComponent(out UnitSpriteController spriteController))
            {
                spriteController.SetTargetTransform((_targetStorage as Component).transform);
            }
        }

        yield return new WaitForSeconds(loadingTime);

        if (_targetStorage == null || _currentRequest == null)
        {
            SetTask_Idle();
            _loadingCoroutine = null;
            yield break;
        }

        if (_targetStorage.TryWithdrawResource(_currentRequest.type, _currentRequest.amount, out int withdrawnAmount) && withdrawnAmount > 0)
        {
            _carriedResourceType = _currentRequest.type;
            _carriedAmount = withdrawnAmount;
            
            if (!CanDepositResource())
            {
                if (_targetStorage != null)
                {
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
        else
        {
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

        if (!CanDepositResource())
        {
            SetTask_Idle();
            return;
        }

        bool isAtDelivery = IsAtTarget();

        if (isAtDelivery) {
            if (!CanDepositResource())
            {
                SetTask_Idle();
                return;
            }
            
            _unloadingCoroutine = StartCoroutine(UnloadingResourceCoroutine());
        }
        else if (ShouldRepath())
        {
            TryRepathToDelivery();
        }
    }
    
    private bool CanDepositResource()
    {
        if (_currentRequest == null || _currentRequest.site == null || _carriedAmount <= 0)
        {
            return false;
        }
        
        return _currentRequest.site.CanDepositResource(_carriedResourceType, this);
    }

    private IEnumerator UnloadingResourceCoroutine()
    {
        movement.ForceStopAllMovement();

        if (_currentRequest == null || _currentRequest.site == null)
        {
            StopConstructionParticles();
            _unloadingCoroutine = null;
            SetTask_Idle();
            yield break;
        }

        if (!CanDepositResource())
        {
            StopConstructionParticles();
            _unloadingCoroutine = null;
            SetTask_Idle();
            yield break;
        }

        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
        {
            Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
            Vector3 piecePos = BuildingManager.Instance.grid.GetCellCenterWorld(targetPieceCell);
            if (TryGetComponent(out UnitSpriteController spriteController))
            {
                spriteController.SetTargetPosition(piecePos);
            }
            
            AdjustConstructionDirection(piecePos);
        }

        StartConstructionParticles();

        yield return new WaitForSeconds(unloadingTime);
        
        if (!CanDepositResource())
        {
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
        if (_currentRequest == null || _currentRequest.site == null)
        {
            SetTask_Idle();
            return;
        }

        _nextRepathTime = Time.time + RepathInterval;
        Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
        Vector3 interactionPos = _currentRequest.site.AssignDeliveryInteractionCell(this, targetPieceCell);
        bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
        if (!hasPath)
        {
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
        _targetStorage = ResourceManager.Instance.FindClosestStorageWithResource(site.GetPosition(), request.type, 1);

        if (_targetStorage != null)
        {
            bool hasPath = movement.SetNewTarget(_targetStorage.GetPosition());
            if (hasPath)
            {
                _currentState = ConstructState.FetchingResource;
                return;
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
        
        if (_currentRequest != null)
        {
            _currentRequest.site?.CancelRequest(_currentRequest);
            _currentRequest = null;
        }

        if (_currentConstructionSite != null)
        {
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
    
    private void StartFloatingAnimation()
    {
        StopFloatingAnimation();
        
        _startYPosition = transform.position.y;
        _floatingPhase = 0f;
        
        _floatingTween = DOTween.To(
            () => _floatingPhase,
            phase => _floatingPhase = phase,
            1f,
            floatDuration
        )
        .SetEase(Ease.Linear)
        .SetLoops(-1, LoopType.Restart);
    }
    
    private void ApplyFloatingOffset()
    {
        if (_floatingTween == null || !_floatingTween.IsActive()) return;
        
        float offset = Mathf.Sin(_floatingPhase * Mathf.PI * 2f) * floatAmplitude;
        
        if (_rb != null)
        {
            Vector2 currentPos = _rb.position;
            float targetY = _startYPosition + offset;
            _rb.MovePosition(new Vector2(currentPos.x, targetY));
        }
        else
        {
            Vector3 pos = transform.position;
            transform.position = new Vector3(pos.x, _startYPosition + offset, pos.z);
        }
    }
    
    private void StopFloatingAnimation()
    {
        if (_floatingTween != null && _floatingTween.IsActive())
        {
            _floatingTween.Kill();
            _floatingTween = null;
        }
        _floatingPhase = 0f;
    }
    
    private void StartConstructionParticles()
    {
        if (constructingParticleSystem != null)
        {
            UpdateParticlePosition();
            if (!constructingParticleSystem.isPlaying)
            {
                constructingParticleSystem.Play();
            }
        }
    }
    
    private void StopConstructionParticles()
    {
        if (constructingParticleSystem != null && constructingParticleSystem.isPlaying)
        {
            constructingParticleSystem.Stop();
        }
    }
    
    private void UpdateParticlePosition()
    {
        if (constructingParticleSystem == null) return;
        
        Transform particleTransform = constructingParticleSystem.transform;
        Vector3 offsetPosition = transform.position + (_currentConstructionDirection * particleOffsetDistance) - new Vector3(0, yOffset, 0);
        particleTransform.position = offsetPosition;
    }

    private enum ConstructState
    {
        Idle,
        FetchingResource,
        DeliveringResource
    }
}