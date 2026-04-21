using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public class Unit_Drone : UnitBase
{
    private const float RepathInterval = 0.5f;

    [Header("Drone Settings")]
    [SerializeField] public int carryCapacity = 10;
    [SerializeField] private float processingSpeed = 1f;
    [SerializeField] private float loadingTime = 1f;
    [SerializeField] private float unloadingTime = 1f;
    [SerializeField] private float assignmentTime = 1f;
    [SerializeField] private float notAssignedAlertDelay = 3f;
    [SerializeField] private UnitMovement movement;

    [Header("Hover Animation")]
    [SerializeField] private float hoverHeight = 0.2f;
    [SerializeField] private float hoverDuration = 1.5f;
    private Coroutine _assignmentCoroutine;
    private Coroutine _autoAssignCoroutine;
    private Vector3 _baseHoverLocalPosition;
    private WaitForSeconds _assignmentWait;

    private int _carriedAmount;

    private ResourceType _carriedResourceType;
    private Processor _currentProcessor;
    private Processor.ResourceRequest _currentRequest;
    private DroneState _currentState = DroneState.Idle;
    private Tween _hoverTween;
    private Coroutine _loadingCoroutine;
    private float _nextRepathTime;
    private bool _notAssignedAlertActive;
    private bool _noResourceAlertActive;
    private float _notAssignedAlertEnableTime;

    private UnitAllyBatteryDriver _allyBatteryDriver;
    private UnitSpriteController _spriteController;
    private Transform _spriteTransform;
    private IStorage _targetStorage;
    private Coroutine _unloadingCoroutine;
    private List<IStorage> _storageRoute;
    private int _storageRouteIndex;
    private int _remainingRequestAmount;

    public bool HasCheckedIn { get; private set; }

    public bool IsAssigned {
        get {
            return _currentProcessor != null;
        }
    }

    public ActiveRecipe CurrentRecipeTask { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        movement = GetComponent<UnitMovement>();
        _allyBatteryDriver = GetComponent<UnitAllyBatteryDriver>();
    }

    protected void Start()
    {
        _spriteController = GetComponentInChildren<UnitSpriteController>();
        if (_spriteController != null) {
            _spriteTransform = _spriteController.transform;
            _baseHoverLocalPosition = _spriteTransform.localPosition;
        }
        _assignmentWait = CoroutineCache.GetWaitForSeconds(assignmentTime);
        if (!IsAssigned) {
            _autoAssignCoroutine = StartCoroutine(AutoAssignNearestProcessorCoroutine());
        }
    }

    private void Update()
    {
        UpdateUnitBaseState();
        UpdateAnimationState();
        DecideNextAction();
        UpdateHoverAnimation();
        UpdateUnitLightAlpha();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _notAssignedAlertEnableTime = Time.time + Mathf.Max(0f, notAssignedAlertDelay);
        UnitManager.Instance?.AddUnit(this);
    }

    protected override void OnDisable()
    {
        if (_autoAssignCoroutine != null) {
            StopCoroutine(_autoAssignCoroutine);
            _autoAssignCoroutine = null;
        }
        SetDroneIsNotAssignedAlert(false);
        SetDroneNoResourceAlert(false);
        StopHover();
        base.OnDisable();
        UnitManager.Instance?.RemoveUnit(this);
        ReleaseFromProcessor();
    }

    private void DecideNextAction()
    {
        if (_allyBatteryDriver != null && _allyBatteryDriver.BlocksWorkLogic) {
            return;
        }

        switch (_currentState) {
        case DroneState.Idle:
            UpdateIdle();
            bool delayElapsed = Time.time >= _notAssignedAlertEnableTime;
            bool shouldShowNotAssignedAlert = !IsAssigned && delayElapsed;
            SetDroneIsNotAssignedAlert(shouldShowNotAssignedAlert);
            if (shouldShowNotAssignedAlert) {
                SetDroneNoResourceAlert(false);
            }
            break;

        case DroneState.FetchingResource:
            UpdateFetching();
            break;

        case DroneState.DeliveringResource:
            UpdateDelivering();
            break;

        case DroneState.Processing:
            UpdateProcessing();
            break;

        case DroneState.ReturnHome:
            UpdateReturnHome();
            break;
        }
    }

    private void UpdateUnitBaseState()
    {
        switch (_currentState) {
        case DroneState.Idle:
            if (movement != null && movement.IsMoving) {
                currentState = UnitState.Moving;
            }
            else {
                currentState = UnitState.Idle;
            }
            break;
        case DroneState.FetchingResource:
        case DroneState.DeliveringResource:
        case DroneState.ReturnHome:
            currentState = UnitState.Moving;
            break;
        case DroneState.Processing:
            currentState = UnitState.Idle;
            break;
        }
    }

    private void UpdateAnimationState()
    {
        if (_spriteController == null || movement == null) {
            return;
        }

        bool isProcessing = _currentState == DroneState.Processing;
        bool isLoading = _loadingCoroutine != null;
        bool isUnloading = _unloadingCoroutine != null;

        _spriteController.UpdateAnimationState(currentState, isProcessing: isProcessing);

        if (isLoading || isUnloading) {
            return;
        }

        if (currentState == UnitState.Moving) {
            Vector3 moveDir = movement.GetMoveDirection();
            _spriteController.UpdateSpriteDirection(moveDir);
            _spriteController.ClearTarget();
        }
    }

    public void AssignProcessor(Processor processor)
    {
        ReleaseFromProcessor();

        _currentProcessor = processor;
        HasCheckedIn = false;

        if (_currentProcessor != null) {
            movement?.ForceStopAllMovement();
            _currentProcessor.AssignDrone(this);
            _currentState = DroneState.Idle;

            if (_spriteController != null) {
                _spriteController.SetTargetTransform(_currentProcessor.transform);
                Vector2 direction = GetProcessorLookDirection(_currentProcessor);
                if (direction.sqrMagnitude > 0.01f) {
                    _spriteController.UpdateSpriteDirection(direction);
                }
            }
        }
        else {
            _currentState = DroneState.Idle;
        }

        UpdateUnitBaseState();
        UpdateAnimationState();
    }

    private void ReleaseFromProcessor()
    {
        ReleaseFromRecipeTask();
        HideProgressBar();

        if (_loadingCoroutine != null) {
            StopCoroutine(_loadingCoroutine);
            _loadingCoroutine = null;
        }
        if (_unloadingCoroutine != null) {
            StopCoroutine(_unloadingCoroutine);
            _unloadingCoroutine = null;
        }
        if (_assignmentCoroutine != null) {
            StopCoroutine(_assignmentCoroutine);
            _assignmentCoroutine = null;
        }

        if (_currentProcessor != null) {
            _currentProcessor.ReleaseDrone(this);
            if (_currentRequest != null) {
                _currentProcessor.CancelRequest(_currentRequest);
                _currentRequest = null;
            }
        }
        _currentProcessor = null;
    }

    private void LookAtTarget(Vector3 targetPosition)
    {
        if (_spriteController == null) return;

        Vector2 direction;

        if (_currentProcessor != null &&
            Vector3.Distance(targetPosition, _currentProcessor.transform.position) < 0.1f) {
            direction = GetProcessorLookDirection(_currentProcessor);
        }
        else {
            direction = (targetPosition - transform.position).normalized;
        }

        if (direction.sqrMagnitude > 0.01f) {
            _spriteController.UpdateSpriteDirection(direction);
        }
    }

    private Vector2 GetProcessorLookDirection(Processor processor)
    {
        if (processor == null) return Vector2.zero;

        Vector3 interactionCellPos = processor.AssignInteractionCell(this);
        Vector3 processorPos = processor.transform.position;

        float gridSize = 1f;
        processorPos.y += 0.5f * gridSize;

        if (processor.ProcessorData != null &&
            processor.ProcessorData.ProcessorName != null &&
            processor.ProcessorData.ProcessorName.Contains("Smelter")) {
            processorPos.x += 0.5f * gridSize;
        }

        Vector3 relativePos = interactionCellPos - processorPos;
        Vector2 lookDirection;

        float absX = Mathf.Abs(relativePos.x);
        float absY = Mathf.Abs(relativePos.y);

        if (absY > 1.25f) {
            if (absX > absY) {
                if (relativePos.x > 0) {
                    lookDirection = Vector2.left;
                }
                else {
                    lookDirection = Vector2.right;
                }
            }
            else {
                if (relativePos.y > 0) {
                    lookDirection = Vector2.down;
                }
                else {
                    lookDirection = Vector2.up;
                }
            }
        }
        else {
            if (relativePos.x > 0) {
                lookDirection = Vector2.left;
            }
            else {
                lookDirection = Vector2.right;
            }
        }
        return lookDirection;
    }

    private void ReleaseFromRecipeTask()
    {
        if (CurrentRecipeTask != null && _currentProcessor != null) {
            _currentProcessor.ReleaseDroneFromRecipe(this);
        }
        CurrentRecipeTask = null;
    }

    private void UpdateIdle()
    {
        if (_currentProcessor == null) {
            UpdateIdleRoam();
            return;
        }

        ResetIdleRoam();

        if (!HasCheckedIn) {
            if (_assignmentCoroutine != null) {
                return;
            }

            Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
            bool isAtProcessorByDistance = Vector3.Distance(transform.position, interactionPos) <= movement.waypointTolerance;
            bool isAtProcessorForCheckIn = movement.HasReachedTarget(movement.waypointTolerance) || isAtProcessorByDistance;

            if (isAtProcessorForCheckIn) {
                if (_assignmentCoroutine == null) {
                    _assignmentCoroutine = StartCoroutine(AssignmentCoroutine());
                }
                return;
            }

            if (!movement.IsMoving) {
                bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                if (!hasPath) {
                    if (_assignmentCoroutine == null) {
                        _assignmentCoroutine = StartCoroutine(AssignmentCoroutine());
                    }
                }
            }
            return;
        }

        bool isAtProcessor = movement.HasReachedTarget(movement.waypointTolerance);

        if (isAtProcessor) {
            if (!movement.IsMoving) {
                movement.ForceStopAllMovement();
                if (_spriteController != null) {
                    _spriteController.SetTargetTransform(_currentProcessor.transform);
                    Vector2 direction = GetProcessorLookDirection(_currentProcessor);
                    if (direction.sqrMagnitude > 0.01f) {
                        _spriteController.UpdateSpriteDirection(direction);
                    }
                }
                _currentProcessor.RequestTask(this);
            }
        }
        else {
            if (!movement.IsMoving) {
                _currentProcessor.AssignInteractionCell(this);
                _currentProcessor.RequestTask(this);
            }
        }
    }

    private void UpdateFetching()
    {
        if (_targetStorage == null || _currentRequest == null) {
            SetTask_ReturnHome();
            return;
        }

        bool isAtStorage = movement.HasReachedTarget(movement.waypointTolerance);

        if (isAtStorage && !movement.IsMoving) {
            if (_spriteController != null && _targetStorage != null) {
                Vector3 storagePos = ((Component)_targetStorage).transform.position;
                LookAtTarget(storagePos);
                _spriteController.SetTargetTransform(((Component)_targetStorage).transform);
            }
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

        if (_targetStorage != null && _spriteController != null) {
            Vector3 storagePos = ((Component)_targetStorage).transform.position;
            LookAtTarget(storagePos);
            _spriteController.SetTargetTransform(((Component)_targetStorage).transform);
        }

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

        if (_targetStorage != null && _currentRequest != null && _remainingRequestAmount > 0) {
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
                        _currentState = DroneState.FetchingResource;
                        movement.ResumeMovement();
                        movedToNext = true;
                        break;
                    }
                }

                if (!movedToNext) {
                    if (_carriedAmount > 0 && _currentProcessor != null) {
                        Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
                        movement.ResumeMovement();
                        movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                        _currentState = DroneState.DeliveringResource;
                    }
                    else {
                        SetTask_ReturnHome();
                    }
                }
            }
            else {
                if (_carriedAmount > 0 && _currentProcessor != null) {
                    Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
                    movement.ResumeMovement();
                    movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                    _currentState = DroneState.DeliveringResource;
                }
                else {
                    SetTask_ReturnHome();
                }
            }
        }
        else {
            if (_carriedAmount > 0 && _currentProcessor != null) {
                Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
                movement.ResumeMovement();
                movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                _currentState = DroneState.DeliveringResource;
            }
            else {
                SetTask_ReturnHome();
            }
        }

        _loadingCoroutine = null;
    }

    private void UpdateDelivering()
    {
        bool isAtProcessor = movement.HasReachedTarget(movement.waypointTolerance);

        if (isAtProcessor && !movement.IsMoving) {
            if (_spriteController != null && _currentProcessor != null) {
                Vector2 direction = GetProcessorLookDirection(_currentProcessor);
                if (direction.sqrMagnitude > 0.01f) {
                    _spriteController.UpdateSpriteDirection(direction);
                }
                _spriteController.SetTargetTransform(_currentProcessor.transform);
            }
        }

        if (!movement.IsMoving) {
            if (_unloadingCoroutine == null) {
                _unloadingCoroutine = StartCoroutine(UnloadingResourceCoroutine());
            }
        }
    }

    private IEnumerator UnloadingResourceCoroutine()
    {
        movement.ForceStopAllMovement();

        if (_currentProcessor != null && _spriteController != null) {
            Vector2 direction = GetProcessorLookDirection(_currentProcessor);
            if (direction.sqrMagnitude > 0.01f) {
                _spriteController.UpdateSpriteDirection(direction);
            }
            _spriteController.SetTargetTransform(_currentProcessor.transform);
        }

        ShowProgressBar();
        float elapsedTime = 0f;

        while (elapsedTime < unloadingTime) {
            elapsedTime += Time.deltaTime;
            float progress = unloadingTime > 0f ? elapsedTime / unloadingTime : 1f;
            UpdateProgressBar(progress);
            yield return null;
        }

        HideProgressBar();
        bool taskAssigned = false;

        if (_currentProcessor != null) {
            _currentProcessor.TryDepositIngredient(_carriedResourceType, _carriedAmount, this);

            if (_currentState == DroneState.Processing) {
                taskAssigned = true;
            }
        }

        _carriedAmount = 0;
        _currentRequest = null;

        if (!taskAssigned) {
            _currentState = DroneState.Idle;
            if (_currentProcessor != null) {
                _currentProcessor.RequestTask(this);
            }
        }

        _unloadingCoroutine = null;
    }

    private IEnumerator AssignmentCoroutine()
    {
        movement.ForceStopAllMovement();

        if (_currentProcessor != null && _spriteController != null) {
            Vector2 direction = GetProcessorLookDirection(_currentProcessor);
            if (direction.sqrMagnitude > 0.01f) {
                _spriteController.UpdateSpriteDirection(direction);
            }
            _spriteController.SetTargetTransform(_currentProcessor.transform);
        }
        else if (_spriteController != null && movement != null) {
            Vector3 moveDir = movement.GetMoveDirection();
            _spriteController.UpdateSpriteDirection(moveDir);
        }

        yield return _assignmentWait;

        HasCheckedIn = true;

        if (_currentProcessor != null) {
            _currentProcessor.RequestTask(this);
        }

        _assignmentCoroutine = null;
    }

    private void UpdateProcessing()
    {
        if (CurrentRecipeTask == null) {
            SetTask_Idle();
            return;
        }

        if (_currentProcessor != null && _currentProcessor is IElectricityConsumer consumer && !consumer.IsOperational) {
            SetTask_Idle();
            return;
        }

        bool isAtProcessor = movement.HasReachedTarget(movement.waypointTolerance);

        if (isAtProcessor) {
            if (movement.IsMoving) {
                movement.StopMovement();
            }

            if (_spriteController != null && _currentProcessor != null) {
                _spriteController.SetTargetTransform(_currentProcessor.transform);
                Vector2 direction = GetProcessorLookDirection(_currentProcessor);
                if (direction.sqrMagnitude > 0.01f) {
                    _spriteController.UpdateSpriteDirection(direction);
                }
            }

            if (CurrentRecipeTask != null && CurrentRecipeTask.isProcessing) {
                ShowProgressBar();
                float normalizedProgress = 0f;
                if (CurrentRecipeTask.recipeData != null && CurrentRecipeTask.recipeData.processingTime > 0f) {
                    normalizedProgress = CurrentRecipeTask.processingProgress / CurrentRecipeTask.recipeData.processingTime;
                }
                UpdateProgressBar(normalizedProgress);
            } else {
                HideProgressBar();
            }

            if (_currentProcessor != null && CurrentRecipeTask != null) {
                _currentProcessor.ProcessRecipeWork(CurrentRecipeTask, Time.deltaTime * processingSpeed);
            }

            if (CurrentRecipeTask == null || CurrentRecipeTask != null && !CurrentRecipeTask.isProcessing && CurrentRecipeTask.assignedDrone == null) {
                HideProgressBar();
                return;
            }
        }
        else {
            HideProgressBar();
        }

        if (!movement.IsMoving && !isAtProcessor) {
            if (Time.time >= _nextRepathTime) {
                _nextRepathTime = Time.time + RepathInterval;
                Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
                bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                if (hasPath) {
                    _ = movement.FinalTargetPosition != default
                        ? Vector3.Distance(transform.position, movement.FinalTargetPosition)
                        : 0f;
                }
                else {
                    SetTask_Idle();
                }
            }
        }
    }

    private void UpdateReturnHome()
    {
        if (_currentProcessor == null) {
            movement.StopMovement();
            _currentState = DroneState.Idle;
            return;
        }

        Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
        movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);

        if (!movement.IsMoving) {
            _currentState = DroneState.Idle;
        }
    }

    public void SetTask_FetchResource(Processor.ResourceRequest request, Processor processor)
    {
        ResetIdleRoam();
        SetDroneIsNotAssignedAlert(false);
        SetDroneNoResourceAlert(false);
        _currentRequest = request;
        _targetStorage = null;
        _storageRoute = null;
        _storageRouteIndex = 0;
        _remainingRequestAmount = request != null ? request.amount : 0;

        if (ResourceManager.Instance != null && request != null && processor != null && _remainingRequestAmount > 0) {
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

                if (candidates.Count > 0 && totalAvailable >= request.amount) {
                    candidates.Sort((a, b) => {
                        float distA = Vector3.Distance(processor.GetPosition(), a.GetPosition());
                        float distB = Vector3.Distance(processor.GetPosition(), b.GetPosition());
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
                                _currentState = DroneState.FetchingResource;
                                SetDroneNoResourceAlert(false);
                                return;
                            }
                        }
                    }
                }
            }
        }

        SetTask_ReturnHome(true, true);
    }

    public void SetTask_Process(Processor processor, ActiveRecipe recipeTask)
    {
        ResetIdleRoam();
        SetDroneNoResourceAlert(false);
        if (recipeTask == null) {
            SetTask_Idle();
            return;
        }

        Vector3 interactionPos = processor.AssignInteractionCell(this);
        bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);

        if (!hasPath) {
            if (processor != null) {
                processor.ReleaseDroneFromRecipe(this);
            }
            SetTask_Idle();
            return;
        }

        _currentState = DroneState.Processing;
        CurrentRecipeTask = recipeTask;
    }

    public void SetTask_Idle()
    {
        ReleaseFromRecipeTask();
        HideProgressBar();
        _currentState = DroneState.Idle;
        SetDroneNoResourceAlert(false);
    }

    private void SetTask_ReturnHome(bool stopMovement = false, bool dueToNoResource = false)
    {
        ResetIdleRoam();
        ReleaseFromRecipeTask();
        HideProgressBar();

        if (_currentRequest != null) {
            _currentProcessor?.CancelRequest(_currentRequest);
            _currentRequest = null;
        }

        _currentState = DroneState.ReturnHome;

        if (stopMovement) {
            movement.StopMovement();
        }

        SetDroneNoResourceAlert(dueToNoResource && IsAssigned);
    }

    private void SetDroneIsNotAssignedAlert(bool shouldEnable)
    {
        if (shouldEnable == _notAssignedAlertActive) return;
        GameAlertUIManager alertManager = FindFirstObjectByType<GameAlertUIManager>();
        if (shouldEnable) {
            alertManager?.RegisterAlert(GameAlertType.DroneIsNotAssigned, this);
        }
        else {
            alertManager?.UnregisterAlert(GameAlertType.DroneIsNotAssigned, this);
        }
        _notAssignedAlertActive = shouldEnable;
    }

    private void SetDroneNoResourceAlert(bool shouldEnable)
    {
        if (shouldEnable == _noResourceAlertActive) return;
        GameAlertUIManager alertManager = FindFirstObjectByType<GameAlertUIManager>();
        if (shouldEnable) {
            alertManager?.RegisterAlert(GameAlertType.DroneNoResource, this);
        }
        else {
            alertManager?.UnregisterAlert(GameAlertType.DroneNoResource, this);
        }
        _noResourceAlertActive = shouldEnable;
    }

    private IEnumerator AutoAssignNearestProcessorCoroutine()
    {
        yield return null;

        const int maxAttempts = 10;
        WaitForSeconds retryWait = CoroutineCache.GetWaitForSeconds(0.5f);

        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            if (!isActiveAndEnabled || IsAssigned) {
                _autoAssignCoroutine = null;
                yield break;
            }

            Processor processor = FindBestAutoAssignProcessor();
            if (processor != null) {
                AssignProcessor(processor);
                _autoAssignCoroutine = null;
                yield break;
            }

            yield return retryWait;
        }

        _autoAssignCoroutine = null;
    }

    private Processor FindBestAutoAssignProcessor()
    {
        Processor[] processors = FindObjectsByType<Processor>(FindObjectsSortMode.None);
        if (processors == null || processors.Length == 0) {
            return null;
        }

        List<Processor> assignable = processors
            .Where(p => p != null && p.gameObject.activeInHierarchy && !p.IsFull)
            .ToList();

        if (assignable.Count == 0) {
            return null;
        }

        Vector3 dronePos = transform.position;
        List<Processor> withWork = assignable
            .Where(p => p.HasWorkForDrone(carryCapacity))
            .ToList();

        List<Processor> candidates = withWork.Count > 0 ? withWork : assignable;
        Processor best = null;
        float bestDistance = float.MaxValue;

        foreach (Processor processor in candidates) {
            float distance = Vector3.Distance(dronePos, processor.transform.position);
            if (distance < bestDistance) {
                bestDistance = distance;
                best = processor;
            }
        }

        return best;
    }

    private void UpdateHoverAnimation()
    {
        bool shouldHover = _spriteTransform != null &&
            _currentState != DroneState.Processing &&
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

    private enum DroneState
    {
        Idle,
        FetchingResource,
        DeliveringResource,
        Processing,
        ReturnHome
    }
}
