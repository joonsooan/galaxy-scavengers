using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private UnitMovement movement;

    [Header("Hover Animation")]
    [SerializeField] private float hoverHeight = 0.2f;
    [SerializeField] private float hoverDuration = 1.5f;

    public bool HasCheckedIn { get; private set; }
    public bool IsAssigned => _currentProcessor != null;
    public ActiveRecipe CurrentRecipeTask { get; private set; }
    
    private int _carriedAmount;
    private float _nextRepathTime;
    
    private ResourceType _carriedResourceType;
    private Processor _currentProcessor;
    private Processor.ResourceRequest _currentRequest;
    private DroneState _currentState = DroneState.Idle;
    private Coroutine _loadingCoroutine;
    private Coroutine _assignmentCoroutine;
    
    private UnitSpriteController _spriteController;
    private IStorage _targetStorage;
    private Coroutine _unloadingCoroutine;
    private Tween _hoverTween;
    private Vector3 _baseHoverLocalPosition;
    private Transform _spriteTransform;
    private bool _noResourceAlertActive;

    protected override void Awake()
    {
        base.Awake();
        movement = GetComponent<UnitMovement>();
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
        UpdateUnitBaseState();
        UpdateAnimationState();
        DecideNextAction();
        UpdateHoverAnimation();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        UnitManager.Instance?.AddUnit(this);
    }

    protected override void OnDisable()
    {
        if (_noResourceAlertActive)
        {
            GameAlertUIManager.Instance?.UnregisterAlert(GameAlertType.DroneNoResource);
            _noResourceAlertActive = false;
        }
        StopHover();
        base.OnDisable();
        UnitManager.Instance?.RemoveUnit(this);
        ReleaseFromProcessor();
    }

    private void DecideNextAction()
    {
        switch (_currentState) {
        case DroneState.Idle:
            UpdateIdle();
            bool shouldShowAlert = !IsAssigned;
            if (shouldShowAlert && !_noResourceAlertActive)
            {
                GameAlertUIManager.Instance?.RegisterAlert(GameAlertType.DroneNoResource);
                _noResourceAlertActive = true;
            }
            else if (!shouldShowAlert && _noResourceAlertActive)
            {
                GameAlertUIManager.Instance?.UnregisterAlert(GameAlertType.DroneNoResource);
                _noResourceAlertActive = false;
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
            Vector3.Distance(targetPosition, _currentProcessor.transform.position) < 0.1f)
        {
            direction = GetProcessorLookDirection(_currentProcessor);
        }
        else
        {
            direction = (targetPosition - transform.position).normalized;
        }
        
        if (direction.sqrMagnitude > 0.01f)
        {
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
            processor.ProcessorData.ProcessorName.Contains("Smelter"))
        {
            processorPos.x += 0.5f * gridSize;
        }
        
        Vector3 relativePos = interactionCellPos - processorPos;
        Vector2 lookDirection;
        
        float absX = Mathf.Abs(relativePos.x);
        float absY = Mathf.Abs(relativePos.y);

        if (absY > 1.25f)
        {
            if (absX > absY)
            {
                if (relativePos.x > 0)
                {
                    lookDirection = Vector2.left;
                }
                else
                {
                    lookDirection = Vector2.right;
                }
            }
            else
            {
                if (relativePos.y > 0)
                {
                    lookDirection = Vector2.down;
                }
                else
                {
                    lookDirection = Vector2.up;
                }
            }
            
        }
        else
        {
            if (relativePos.x > 0)
            {
                lookDirection = Vector2.left;
            }
            else
            {
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
        if (_currentProcessor == null)
            return;

        if (!HasCheckedIn) {
            if (_assignmentCoroutine != null) {
                return;
            }

            bool isAtProcessorForCheckIn = movement.HasReachedTarget(movement.waypointTolerance);

            if (isAtProcessorForCheckIn) {
                if (_assignmentCoroutine == null) {
                    _assignmentCoroutine = StartCoroutine(AssignmentCoroutine());
                }
                return;
            }

            if (!movement.IsMoving) {
                Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
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
            if (!movement.IsMoving)
            {
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
            // Validate that the storage still has the required resources before loading
            if (_targetStorage != null && _currentRequest != null) {
                int availableAmount = _targetStorage.GetCurrentResourceAmount(_currentRequest.type);
                if (availableAmount < _currentRequest.amount) {
                    // Storage no longer has enough resources, cancel task
                    SetTask_ReturnHome();
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
        movement.ForceStopAllMovement();
        
        if (_targetStorage != null && _spriteController != null) {
            Vector3 storagePos = ((Component)_targetStorage).transform.position;
            LookAtTarget(storagePos);
            _spriteController.SetTargetTransform(((Component)_targetStorage).transform);
        }
        
        // Show progress bar during loading
        ShowProgressBar();
        float elapsedTime = 0f;
        
        while (elapsedTime < loadingTime)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / loadingTime;
            UpdateProgressBar(progress);
            yield return null;
        }
        
        HideProgressBar();

        if (_targetStorage != null && _currentRequest != null) {
            int availableAmount = _targetStorage.GetCurrentResourceAmount(_currentRequest.type);
            if (availableAmount < _currentRequest.amount) {
                SetTask_ReturnHome();
                _loadingCoroutine = null;
                yield break;
            }
            
            if (_targetStorage.TryWithdrawResource(_currentRequest.type, _currentRequest.amount, out int withdrawnAmount)) {
                if (withdrawnAmount > 0) {
                    _carriedResourceType = _currentRequest.type;
                    _carriedAmount = withdrawnAmount;
                    
                    Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
                    movement.ResumeMovement();
                    movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                    
                    _currentState = DroneState.DeliveringResource;
                }
                else {
                    SetTask_ReturnHome();
                }
            }
            else {
                SetTask_ReturnHome();
            }
        }
        else {
            SetTask_ReturnHome();
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

        yield return new WaitForSeconds(unloadingTime);

        if (_currentProcessor != null) {
            _currentProcessor.TryDepositIngredient(_carriedResourceType, _carriedAmount, this);

            _carriedAmount = 0;
            _currentRequest = null;
            _currentState = DroneState.Idle;

            _currentProcessor.RequestTask(this);
        }
        else {
            _currentState = DroneState.Idle;
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

        yield return new WaitForSeconds(assignmentTime);

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
        
        // Check if processor is operational (has aether)
        if (_currentProcessor != null && _currentProcessor is IAetherConsumer consumer && !consumer.IsOperational)
        {
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
            
            // Show progress bar during processing
            if (CurrentRecipeTask != null && CurrentRecipeTask.isProcessing)
            {
                ShowProgressBar();
                float progress = CurrentRecipeTask.processingProgress;
                UpdateProgressBar(progress);
            }
            else
            {
                HideProgressBar();
            }
            
            _currentProcessor.ProcessRecipeWork(CurrentRecipeTask, Time.deltaTime * processingSpeed);

            if (CurrentRecipeTask == null || !CurrentRecipeTask.isProcessing && CurrentRecipeTask.assignedDrone == null) {
                HideProgressBar();
                return;
            }
        }
        else
        {
            HideProgressBar();
        }

        if (!movement.IsMoving && !isAtProcessor) {
            if (Time.time >= _nextRepathTime) {
                _nextRepathTime = Time.time + RepathInterval;
                Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
                bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                if (hasPath)
                {
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
        _currentRequest = request;
        _targetStorage = ResourceManager.Instance.FindClosestStorageWithResource(processor.GetPosition(), request.type, 1);

        if (_targetStorage != null) {
            bool hasPath = movement.SetNewTarget(_targetStorage.GetPosition());
            if (hasPath) {
                _currentState = DroneState.FetchingResource;
                return;
            }
        }

        SetTask_ReturnHome(true);
    }

    public void SetTask_Process(Processor processor, ActiveRecipe recipeTask)
    {
        Vector3 interactionPos = processor.AssignInteractionCell(this);
        bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);

        if (!hasPath) {
            SetTask_Idle();
            Debug.Log($"[Drone:{name}] Process start FAILED: cannot path to processor '{processor.name}' for {recipeTask.recipeData.resourceType}");
            return;
        }

        _currentState = DroneState.Processing;
        CurrentRecipeTask = recipeTask;
    }

    public void SetTask_Idle()
    {
        ReleaseFromRecipeTask();
        _currentState = DroneState.Idle;
    }

    private void SetTask_ReturnHome(bool stopMovement = false)
    {
        ReleaseFromRecipeTask();

        if (_currentRequest != null) {
            _currentProcessor?.CancelRequest(_currentRequest);
            _currentRequest = null;
        }

        _currentState = DroneState.ReturnHome;

        if (stopMovement) {
            movement.StopMovement();
        }
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
