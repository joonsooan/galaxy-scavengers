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
    [SerializeField] private GameObject constructionSiteAnimationPrefab;
    [SerializeField] private int siteAnimationSortingOrder = 10;
    [SerializeField] private string constructionAnimationStateName = "constructing";

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
    private int _constructionStateHash;

    private Coroutine _loadingCoroutine;

    private float _nextRepathTime;
    private float _nextTaskRequestTime;

    private Rigidbody2D _rb;
    private int _remainingRequestAmount;
    private bool _noResourceAlertActive;
    private UnitSpriteController _spriteController;
    private Transform _spriteTransform;
    private List<IStorage> _storageRoute;
    private int _storageRouteIndex;

    private IStorage _targetStorage;
    private Coroutine _unloadingCoroutine;
    private GameObject _currentSiteAnimation;
    private Animator _currentSiteAnimator;
    private bool _isInvulnerable;
    private UnitAllyBatteryDriver _allyBatteryDriver;

    public bool IsInvulnerable => _isInvulnerable;

    public void SetInvulnerable(bool value)
    {
        _isInvulnerable = value;
    }

    public override void TakeDamage(int damage)
    {
        if (_isInvulnerable) return;
        base.TakeDamage(damage);
    }

    public override void TakeDamage(int damage, DamageContext context)
    {
        if (_isInvulnerable) return;
        base.TakeDamage(damage, context);
    }

    protected override void Awake()
    {
        base.Awake();
        movement = GetComponent<UnitMovement>();
        _rb = GetComponent<Rigidbody2D>();
        _allyBatteryDriver = GetComponent<UnitAllyBatteryDriver>();
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
        _constructionStateHash = Animator.StringToHash(constructionAnimationStateName);
    }

    private void Update()
    {
        UpdateUnitBaseState();
        DecideNextAction();
        UpdateAnimationState();
        UpdateHoverAnimation();
        UpdateUnitLightAlpha();

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
        SetConstructNoResourceAlert(false);
        base.OnDisable();
        UnitManager.Instance?.RemoveUnit(this);
        ConstructionManager.Instance?.UnregisterConstructDrone(this);
        ReleaseFromConstruction();
    }

    protected override void OnDestroy()
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
            if (movement != null && movement.IsMoving) {
                currentState = UnitState.Moving;
            }
            else if (_loadingCoroutine != null) {
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
                currentState = UnitState.Idle;
            }
            break;
        }
    }

    private void DecideNextAction()
    {
        if (_allyBatteryDriver != null && _allyBatteryDriver.BlocksWorkLogic) {
            return;
        }

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
            _spriteController?.ClearTarget();
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
        if (!HasPendingConstructionSite())
        {
            SetConstructNoResourceAlert(false);
        }

        if (Time.time < _nextTaskRequestTime) {
            UpdateIdleRoam();
            return;
        }

        ConstructionManager.Instance?.RequestTask(this);
        UpdateIdleRoam();
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

        if (_remainingRequestAmount > 0) {
            int availableAmount = _targetStorage.GetCurrentResourceAmount(_currentRequest.type);
            if (availableAmount > 0) {
                int amountToWithdraw = Mathf.Min(_remainingRequestAmount, availableAmount);
                if (_targetStorage.TryWithdrawResource(_currentRequest.type, amountToWithdraw, out int withdrawnAmount) && withdrawnAmount > 0) {
                    _carriedResourceType = _currentRequest.type;
                    _carriedAmount += withdrawnAmount;
                    _remainingRequestAmount -= withdrawnAmount;
                }
            }

            if (_remainingRequestAmount > 0) {
                bool movedToNext = false;
                if (_storageRoute != null && _storageRoute.Count > 0) {
                    for (int i = _storageRouteIndex + 1; i < _storageRoute.Count; i++) {
                        IStorage nextStorage = _storageRoute[i];
                        if (nextStorage == null) continue;
                        int nextAvailable = nextStorage.GetCurrentResourceAmount(_currentRequest.type);
                        if (nextAvailable <= 0) continue;
                        bool hasPath = movement.SetNewTarget(nextStorage.GetPosition());
                        if (!hasPath) continue;
                        _storageRouteIndex = i;
                        _targetStorage = nextStorage;
                        _currentState = ConstructState.FetchingResource;
                        movement.ResumeMovement();
                        movedToNext = true;
                        break;
                    }
                }

                if (!movedToNext) {
                    if (_carriedAmount > 0 && CanDepositResource()) {
                        if (!TryBeginDeliveringToConstructionSite()) {
                            _currentRequest?.site?.CancelRequest(_currentRequest);
                            SetTask_Idle();
                        }
                    }
                    else {
                        _currentRequest?.site?.CancelRequest(_currentRequest);
                        SetTask_Idle();
                    }
                }
            }
            else {
                if (_carriedAmount > 0 && CanDepositResource()) {
                    if (!TryBeginDeliveringToConstructionSite()) {
                        _currentRequest?.site?.CancelRequest(_currentRequest);
                        SetTask_Idle();
                    }
                }
                else {
                    _currentRequest?.site?.CancelRequest(_currentRequest);
                    SetTask_Idle();
                }
            }
        }
        else {
            if (_carriedAmount > 0 && CanDepositResource()) {
                if (!TryBeginDeliveringToConstructionSite()) {
                    _currentRequest?.site?.CancelRequest(_currentRequest);
                    SetTask_Idle();
                }
            }
            else {
                _currentRequest?.site?.CancelRequest(_currentRequest);
                SetTask_Idle();
            }
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

        Vector3Int targetPieceCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null) {
            targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
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
        if (targetPieceCell != new Vector3Int(int.MinValue, int.MinValue, int.MinValue)) {
            StartSiteAnimation(targetPieceCell);
        }

        ShowProgressBar();
        float elapsedTime = 0f;

        float constructionSpeedMultiplier = GetConstructionSpeedMultiplier();
        float adjustedUnloadingTime = unloadingTime / constructionSpeedMultiplier;

        while (elapsedTime < adjustedUnloadingTime) {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / adjustedUnloadingTime;
            UpdateProgressBar(progress);
            UpdateSiteAnimationProgress(progress);
            yield return null;
        }

        HideProgressBar();

        if (!CanDepositResource()) {
            StopConstructionParticles();
            StopSiteAnimation();
            _unloadingCoroutine = null;
            SetTask_Idle();
            yield break;
        }

        StopConstructionParticles();
        StopSiteAnimation();
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

    private bool TryBeginDeliveringToConstructionSite()
    {
        if (_currentRequest == null || _currentRequest.site == null || movement == null) {
            return false;
        }

        if (_carriedAmount <= 0 || !CanDepositResource()) {
            return false;
        }

        Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
        Vector3 interactionPos = _currentRequest.site.AssignDeliveryInteractionCell(this, targetPieceCell);
        movement.ResumeMovement();
        bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
        if (!hasPath) {
            return false;
        }

        _currentState = ConstructState.DeliveringResource;
        return true;
    }

    public void SetTask_FetchResource(ConstructionSite.ConstructionRequest request, ConstructionSite site)
    {
        ResetIdleRoam();
        if (movement == null) {
            if (request != null && request.site != null) {
                request.site.CancelRequest(request);
            }
            return;
        }

        if (_carriedAmount > 0 && ResourceManager.Instance != null) {
            ReturnCarriedResourcesToStorage();
            _carriedAmount = 0;
            _carriedResourceType = default;
        }

        _currentRequest = request;
        _targetStorage = null;
        _storageRoute = null;
        _storageRouteIndex = 0;
        _remainingRequestAmount = request != null ? request.amount : 0;
        bool hasSufficientResource = false;

        if (ResourceManager.Instance != null && request != null && site != null && _remainingRequestAmount > 0) {
            List<IStorage> storages = ResourceManager.Instance.GetAllStorages();
            if (storages != null && storages.Count > 0) {
                List<IStorage> candidates = new List<IStorage>();
                int totalAvailable = 0;
                foreach (IStorage storage in storages) {
                    if (storage == null) continue;
                    int availableAmount = storage.GetCurrentResourceAmount(request.type);
                    if (availableAmount <= 0) continue;
                    totalAvailable += availableAmount;
                    candidates.Add(storage);
                }

                hasSufficientResource = totalAvailable >= request.amount;

                if (candidates.Count > 0 && hasSufficientResource) {
                    candidates.Sort((a, b) => {
                        float distA = Vector3.Distance(site.GetPosition(), a.GetPosition());
                        float distB = Vector3.Distance(site.GetPosition(), b.GetPosition());
                        return distA.CompareTo(distB);
                    });

                    List<IStorage> route = new List<IStorage>();
                    int remaining = request.amount;
                    foreach (IStorage storage in candidates) {
                        int availableAmount = storage.GetCurrentResourceAmount(request.type);
                        if (availableAmount <= 0) continue;
                        route.Add(storage);
                        remaining -= availableAmount;
                        if (remaining <= 0) {
                            break;
                        }
                    }

                    if (route.Count > 0) {
                        for (int i = 0; i < route.Count; i++) {
                            IStorage storage = route[i];
                            bool hasPath = movement.SetNewTarget(storage.GetPosition());
                            if (hasPath) {
                                _storageRoute = route;
                                _storageRouteIndex = i;
                                _targetStorage = storage;
                                _currentState = ConstructState.FetchingResource;
                                ResetIdleRoam();
                                SetConstructNoResourceAlert(false);
                                return;
                            }
                        }

                        _currentRequest?.site?.CancelRequest(_currentRequest);
                        _nextTaskRequestTime = Time.time + TaskRequestCooldown;
                        SetConstructNoResourceAlert(false);
                        SetTask_Idle();
                        return;
                    }
                }
            }
        }

        _currentRequest?.site?.CancelRequest(_currentRequest);
        _nextTaskRequestTime = Time.time + TaskRequestCooldown;
        SetConstructNoResourceAlert(!hasSufficientResource);
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

    private float GetConstructionSpeedMultiplier()
    {
        return 1f;
    }

    public void NotifySiteDestroyed(ConstructionSite site)
    {
        if (_currentRequest == null || _currentRequest.site != site) return;
        ReleaseFromConstruction();
    }

    private void ReleaseFromConstruction()
    {
        StopConstructionParticles();
        StopSiteAnimation();
        HideProgressBar();

        if (_currentRequest != null) {
            _currentRequest.site?.CancelRequest(_currentRequest);
            _currentRequest = null;
        }

        if (_currentConstructionSite != null) {
            _currentConstructionSite.ReleaseDrone(this);
            _currentConstructionSite = null;
        }

        if (_carriedAmount > 0 && ResourceManager.Instance != null) {
            ReturnCarriedResourcesToStorage();
        }

        _carriedAmount = 0;
        _carriedResourceType = default;

        StopAllCoroutines();
        _loadingCoroutine = null;
        _unloadingCoroutine = null;
    }

    private void SetConstructNoResourceAlert(bool shouldEnable)
    {
        if (shouldEnable == _noResourceAlertActive) return;
        GameAlertUIManager alertManager = FindFirstObjectByType<GameAlertUIManager>();
        if (shouldEnable) {
            alertManager?.RegisterAlert(GameAlertType.ConstructNoResource, this);
        }
        else {
            alertManager?.UnregisterAlert(GameAlertType.ConstructNoResource, this);
        }
        _noResourceAlertActive = shouldEnable;
    }

    private bool HasPendingConstructionSite()
    {
        if (ConstructionManager.Instance == null)
        {
            return false;
        }

        IReadOnlyList<ConstructionSite> sites = ConstructionManager.Instance.ConstructionSites;
        if (sites == null || sites.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < sites.Count; i++)
        {
            ConstructionSite site = sites[i];
            if (site != null && !site.IsComplete)
            {
                return true;
            }
        }

        return false;
    }

    private void ReturnCarriedResourcesToStorage()
    {
        if (_carriedAmount <= 0 || ResourceManager.Instance == null) return;

        List<IStorage> storages = ResourceManager.Instance.GetAllStorages();
        if (storages == null || storages.Count == 0) return;

        List<IStorage> availableStorages = new List<IStorage>();
        foreach (IStorage s in storages) {
            if (s != null && s.GetTotalCurrentAmount() < s.GetMaxCapacity())
                availableStorages.Add(s);
        }

        availableStorages.Sort((a, b) => Vector3.Distance(transform.position, a.GetPosition())
            .CompareTo(Vector3.Distance(transform.position, b.GetPosition())));

        int remaining = _carriedAmount;
        foreach (IStorage storage in availableStorages) {
            if (remaining <= 0) break;

            int beforeAmount = storage.GetCurrentResourceAmount(_carriedResourceType);
            bool added = storage.TryAddResource(_carriedResourceType, remaining);
            int afterAmount = storage.GetCurrentResourceAmount(_carriedResourceType);
            int actuallyAdded = afterAmount - beforeAmount;

            if (added && actuallyAdded > 0) {
                remaining -= actuallyAdded;
            }
        }
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

    private void StartSiteAnimation(Vector3Int pieceCell)
    {
        if (constructionSiteAnimationPrefab == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null) {
            return;
        }

        StopSiteAnimation();

        Vector3 pieceWorldPos = BuildingManager.Instance.grid.GetCellCenterWorld(pieceCell);
        _currentSiteAnimation = Instantiate(constructionSiteAnimationPrefab, pieceWorldPos, Quaternion.identity);
        
        if (_currentSiteAnimation != null) {
            foreach (SpriteRenderer sr in _currentSiteAnimation.GetComponentsInChildren<SpriteRenderer>(true)) {
                sr.sortingOrder = siteAnimationSortingOrder;
            }
        }
        _currentSiteAnimator = _currentSiteAnimation != null ? _currentSiteAnimation.GetComponent<Animator>() : null;
        
        if (_currentSiteAnimator != null) {
            _currentSiteAnimator.Play(Animator.StringToHash(constructionAnimationStateName), 0, 0f);
        }

        if (_currentSiteAnimator != null) {
            int hash = Animator.StringToHash(constructionAnimationStateName);
            _currentSiteAnimator.Play(hash, 0, 0f);
        }
    }

    private void UpdateSiteAnimationProgress(float progress)
    {
        if (_currentSiteAnimator == null) return;
        
        _currentSiteAnimator.Play(_constructionStateHash, 0, Mathf.Clamp01(progress));
    }

    private void StopSiteAnimation()
    {
        if (_currentSiteAnimation != null) {
            Destroy(_currentSiteAnimation);
            _currentSiteAnimation = null;
        }
        _currentSiteAnimator = null;
    }

    private enum ConstructState
    {
        Idle,
        FetchingResource,
        DeliveringResource
    }

    public void OnChargeStateEnter()
    {
        ResetIdleRoam();
        ReleaseFromConstruction();
        _currentState = ConstructState.Idle;
        currentState = UnitState.Idle;
        movement?.StopMovement();
    }

    public void OnChargeStateExit()
    {
        _currentState = ConstructState.Idle;
        currentState = UnitState.Idle;
        movement?.StopMovement();
        ResetIdleRoam();
    }
}
