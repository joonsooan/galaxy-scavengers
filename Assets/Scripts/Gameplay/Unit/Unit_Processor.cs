using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public class Unit_Processor : UnitBase
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
    private Coroutine _redistributionCoroutine;
    private RedistributionTask _activeRedistributionTask;
    private Vector3 _baseHoverLocalPosition;

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
    private GameAlertUIManager _gameAlertUIManager;
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
        _gameAlertUIManager = FindFirstObjectByType<GameAlertUIManager>();
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

    private void SetDroneState(DroneState newState)
    {
        if (_currentState == newState) {
            return;
        }

        _currentState = newState;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _notAssignedAlertEnableTime = Time.time + Mathf.Max(0f, notAssignedAlertDelay);
        if (UnitManager.Instance != null) {
            UnitManager.Instance.AddUnit(this);
        }
    }

    protected override void OnDisable()
    {
        if (_autoAssignCoroutine != null) {
            StopCoroutine(_autoAssignCoroutine);
            _autoAssignCoroutine = null;
        }
        if (_redistributionCoroutine != null) {
            StopCoroutine(_redistributionCoroutine);
            _redistributionCoroutine = null;
        }
        if (_activeRedistributionTask != null && ResourceManager.Instance != null) {
            ResourceManager.Instance.CancelRedistributionTask(_activeRedistributionTask);
            _activeRedistributionTask = null;
        }
        SetDroneIsNotAssignedAlert(false);
        SetDroneNoResourceAlert(false);
        StopHover();
        base.OnDisable();
        if (UnitManager.Instance != null) {
            UnitManager.Instance.RemoveUnit(this);
        }
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
            currentState = UnitState.Constructing;
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
            SetDroneState(DroneState.Idle);

            if (_spriteController != null) {
                _spriteController.SetTargetTransform(_currentProcessor.transform);
                Vector2 direction = GetProcessorLookDirection(_currentProcessor);
                if (direction.sqrMagnitude > 0.01f) {
                    _spriteController.UpdateSpriteDirection(direction);
                }
            }
        }
        else {
            SetDroneState(DroneState.Idle);
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
        if (_redistributionCoroutine != null) {
            StopCoroutine(_redistributionCoroutine);
            _redistributionCoroutine = null;
        }
        if (_activeRedistributionTask != null && ResourceManager.Instance != null) {
            ResourceManager.Instance.CancelRedistributionTask(_activeRedistributionTask);
            _activeRedistributionTask = null;
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
            if (_redistributionCoroutine == null && _activeRedistributionTask == null && ResourceManager.Instance != null) {
                if (ResourceManager.Instance.TryClaimRedistributionTask(transform.position, out RedistributionTask task)) {
                    _activeRedistributionTask = task;
                    _redistributionCoroutine = StartCoroutine(ExecuteRedistributionCoroutine(task));
                    return;
                }
            }

            if (_redistributionCoroutine != null) {
                return;
            }

            UpdateIdleRoam();
            return;
        }

        ResetIdleRoam();

        if (!HasCheckedIn) {
            if (_assignmentCoroutine != null) {
                return;
            }

            Vector3 checkInInteractionPos = _currentProcessor.AssignInteractionCell(this);
            bool isAtProcessorByDistanceForCheckIn = Vector3.Distance(transform.position, checkInInteractionPos) <= movement.waypointTolerance;
            bool isAtProcessorForCheckIn = movement.HasReachedTarget(movement.waypointTolerance) || isAtProcessorByDistanceForCheckIn;

            if (isAtProcessorForCheckIn) {
                if (_assignmentCoroutine == null) {
                    _assignmentCoroutine = StartCoroutine(AssignmentCoroutine());
                }
                return;
            }

            if (!movement.IsMoving) {
                bool hasPath = movement.SetNewTargetDirect(checkInInteractionPos, movement.waypointTolerance);
                if (!hasPath) {
                    if (_assignmentCoroutine == null) {
                        _assignmentCoroutine = StartCoroutine(AssignmentCoroutine());
                    }
                }
            }
            return;
        }

        Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
        bool isAtProcessorByDistance = Vector3.Distance(transform.position, interactionPos) <= movement.waypointTolerance;
        bool isAtProcessor = movement.HasReachedTarget(movement.waypointTolerance) || isAtProcessorByDistance;

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
                movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
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
        float loadDuration = loadingTime / Mathf.Max(0.05f, _allyBatteryDriver != null ? _allyBatteryDriver.GetWorkSpeedMultiplier() : 1f);

        while (elapsedTime < loadDuration) {
            elapsedTime += Time.deltaTime;
            float progress = loadDuration > 0f ? elapsedTime / loadDuration : 1f;
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
                        SetDroneState(DroneState.FetchingResource);
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
                        SetDroneState(DroneState.DeliveringResource);
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
                    SetDroneState(DroneState.DeliveringResource);
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
                SetDroneState(DroneState.DeliveringResource);
            }
            else {
                SetTask_ReturnHome();
            }
        }

        _loadingCoroutine = null;
    }

    private void UpdateDelivering()
    {
        Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
        bool isAtProcessorByDistance = Vector3.Distance(transform.position, interactionPos) <= movement.waypointTolerance;
        bool isAtProcessor = movement.HasReachedTarget(movement.waypointTolerance) || isAtProcessorByDistance;

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
        float unloadDuration = unloadingTime / Mathf.Max(0.05f, _allyBatteryDriver != null ? _allyBatteryDriver.GetWorkSpeedMultiplier() : 1f);

        while (elapsedTime < unloadDuration) {
            elapsedTime += Time.deltaTime;
            float progress = unloadDuration > 0f ? elapsedTime / unloadDuration : 1f;
            UpdateProgressBar(progress);
            yield return null;
        }

        HideProgressBar();

        if (_currentProcessor != null) {
            _currentProcessor.TryDepositIngredient(_carriedResourceType, _carriedAmount, this);
        }

        _carriedAmount = 0;

        if (_currentState != DroneState.FetchingResource) {
            _currentRequest = null;
        }

        if (_currentState == DroneState.DeliveringResource) {
            SetDroneState(DroneState.Idle);
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

        float assignDuration = assignmentTime / Mathf.Max(0.05f, _allyBatteryDriver != null ? _allyBatteryDriver.GetWorkSpeedMultiplier() : 1f);
        float assignElapsed = 0f;
        while (assignElapsed < assignDuration) {
            assignElapsed += Time.deltaTime;
            yield return null;
        }

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

        Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
        bool isAtProcessorByDistance = Vector3.Distance(transform.position, interactionPos) <= movement.waypointTolerance;
        bool isAtProcessor = movement.HasReachedTarget(movement.waypointTolerance) || isAtProcessorByDistance;

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
                float workMult = _allyBatteryDriver != null ? _allyBatteryDriver.GetWorkSpeedMultiplier() : 1f;
                _currentProcessor.ProcessRecipeWork(CurrentRecipeTask, Time.deltaTime * processingSpeed * workMult);
            }

            if (CurrentRecipeTask == null || _currentState != DroneState.Processing) {
                return;
            }

            bool processingEnded = CurrentRecipeTask == null ||
                (!CurrentRecipeTask.isProcessing && CurrentRecipeTask.assignedDrone == null);
            if (processingEnded) {
                HideProgressBar();
                SetTask_Idle();
                return;
            }
        }
        else {
            HideProgressBar();
        }

        if (!movement.IsMoving && !isAtProcessor) {
            if (Time.time >= _nextRepathTime) {
                _nextRepathTime = Time.time + RepathInterval;
                movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
            }
        }
    }

    private void UpdateReturnHome()
    {
        if (_currentProcessor == null) {
            movement.StopMovement();
            SetDroneState(DroneState.Idle);
            return;
        }

        Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
        movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);

        if (!movement.IsMoving) {
            SetDroneState(DroneState.Idle);
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
                                SetDroneState(DroneState.FetchingResource);
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

        CurrentRecipeTask = recipeTask;
        SetDroneState(DroneState.Processing);
    }

    public void SetTask_Idle()
    {
        ReleaseFromRecipeTask();
        HideProgressBar();
        SetDroneState(DroneState.Idle);
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

        SetDroneState(DroneState.ReturnHome);

        if (stopMovement) {
            movement.StopMovement();
        }

        SetDroneNoResourceAlert(dueToNoResource && IsAssigned);
    }

    private void SetDroneIsNotAssignedAlert(bool shouldEnable)
    {
        if (shouldEnable == _notAssignedAlertActive) return;
        if (_gameAlertUIManager != null) {
            if (shouldEnable) {
                _gameAlertUIManager.RegisterAlert(GameAlertType.DroneIsNotAssigned, this);
            }
            else {
                _gameAlertUIManager.UnregisterAlert(GameAlertType.DroneIsNotAssigned, this);
            }
        }
        _notAssignedAlertActive = shouldEnable;
    }

    private void SetDroneNoResourceAlert(bool shouldEnable)
    {
        if (shouldEnable == _noResourceAlertActive) return;
        if (_gameAlertUIManager != null) {
            if (shouldEnable) {
                _gameAlertUIManager.RegisterAlert(GameAlertType.DroneNoResource, this);
            }
            else {
                _gameAlertUIManager.UnregisterAlert(GameAlertType.DroneNoResource, this);
            }
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

    private IEnumerator ExecuteRedistributionCoroutine(RedistributionTask task)
    {
        // --- Leg 1: Travel to source and withdraw ---
        if (task.Source == null) {
            CancelActiveRedistribution();
            yield break;
        }

        movement.SetNewTarget(task.Source.GetPosition());

        while (true) {
            if (IsAssigned) {
                CancelActiveRedistribution();
                yield break;
            }

            if (task.Source == null) {
                CancelActiveRedistribution();
                yield break;
            }

            if (!movement.IsMoving) {
                break;
            }
            yield return null;
        }

        if (task.Source == null) {
            CancelActiveRedistribution();
            yield break;
        }

        task.Source.TryWithdrawResource(task.ResourceType, task.Amount, out int withdrawn);

        if (withdrawn <= 0) {
            CancelActiveRedistribution();
            yield break;
        }

        // --- Leg 2: Travel to destination and deposit ---
        IStorage destination = task.Destination;
        if (destination == null) {
            destination = ResourceManager.Instance != null
                ? ResourceManager.Instance.GetBestDepositTarget(task.ResourceType, transform.position)
                : null;
        }

        if (destination == null) {
            if (task.Source != null) {
                task.Source.TryAddResource(task.ResourceType, withdrawn);
            }
            CancelActiveRedistribution();
            yield break;
        }

        movement.SetNewTarget(destination.GetPosition());

        while (true) {
            if (IsAssigned) {
                // Complete this leg before handing off.
                break;
            }

            if (destination == null) {
                IStorage fallback = ResourceManager.Instance != null
                    ? ResourceManager.Instance.GetBestDepositTarget(task.ResourceType, transform.position)
                    : null;
                if (fallback == null) {
                    if (task.Source != null) {
                        task.Source.TryAddResource(task.ResourceType, withdrawn);
                    }
                    CancelActiveRedistribution();
                    yield break;
                }
                destination = fallback;
                movement.SetNewTarget(destination.GetPosition());
            }

            if (!movement.IsMoving) {
                break;
            }
            yield return null;
        }

        // Arrival filter check.
        if (destination == null) {
            if (task.Source != null) {
                task.Source.TryAddResource(task.ResourceType, withdrawn);
            }
            CancelActiveRedistribution();
            yield break;
        }

        StorageFilter destFilter = destination.GetFilter();
        if (destFilter != null && !destFilter.IsAllowed(task.ResourceType)) {
            IStorage rerouted = ResourceManager.Instance != null
                ? ResourceManager.Instance.GetBestDepositTarget(task.ResourceType, transform.position, destination)
                : null;
            if (rerouted != null) {
                destination = rerouted;
            }
            else {
                if (task.Source != null) {
                    task.Source.TryAddResource(task.ResourceType, withdrawn);
                }
                CancelActiveRedistribution();
                yield break;
            }
        }

        destination.TryAddResource(task.ResourceType, withdrawn);

        if (ResourceManager.Instance != null) {
            ResourceManager.Instance.CompleteRedistributionTask(task);
        }
        _activeRedistributionTask = null;
        _redistributionCoroutine = null;
    }

    private void CancelActiveRedistribution()
    {
        if (_activeRedistributionTask != null && ResourceManager.Instance != null) {
            ResourceManager.Instance.CancelRedistributionTask(_activeRedistributionTask);
        }
        _activeRedistributionTask = null;
        _redistributionCoroutine = null;
    }

    private enum DroneState
    {
        Idle,
        FetchingResource,
        DeliveringResource,
        Processing,
        ReturnHome
    }

    public void OnChargeStateEnter()
    {
        InterruptForCharging();
    }

    public void OnChargeStateExit()
    {
        SetDroneState(DroneState.Idle);
        currentState = UnitState.Idle;
        movement?.StopMovement();
        ResetIdleRoam();
        if (_currentProcessor != null) {
            _currentProcessor.RequestTask(this);
        }
    }

    private void InterruptForCharging()
    {
        HideProgressBar();
        ReleaseFromRecipeTask();
        if (_currentRequest != null) {
            _currentProcessor?.CancelRequest(_currentRequest);
            _currentRequest = null;
        }
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
        if (_redistributionCoroutine != null) {
            StopCoroutine(_redistributionCoroutine);
            _redistributionCoroutine = null;
        }
        if (_activeRedistributionTask != null && ResourceManager.Instance != null) {
            ResourceManager.Instance.CancelRedistributionTask(_activeRedistributionTask);
            _activeRedistributionTask = null;
        }
        _carriedAmount = 0;
        _targetStorage = null;
        _storageRoute = null;
        _storageRouteIndex = 0;
        _remainingRequestAmount = 0;
        SetDroneState(DroneState.Idle);
        currentState = UnitState.Idle;
        movement?.StopMovement();
        SetDroneNoResourceAlert(false);
        SetDroneIsNotAssignedAlert(false);
        ResetIdleRoam();
    }
}
