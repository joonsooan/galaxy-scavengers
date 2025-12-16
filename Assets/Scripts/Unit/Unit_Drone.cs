using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit_Drone : UnitBase
{
    [Header("Drone Settings")]
    [SerializeField] public int carryCapacity = 10;
    [SerializeField] private float processingSpeed = 1f;
    [SerializeField] private float loadingTime = 1f;
    [SerializeField] private float unloadingTime = 1f;
    [SerializeField] private float assignmentTime = 1f;
    [SerializeField] private UnitMovement movement;
    [SerializeField] private Sprite droneIcon;

    private int _carriedAmount;
    private ResourceType _carriedResourceType;
    private Processor.ResourceRequest _currentRequest;
    private DroneState _currentState = DroneState.Idle;

    private IStorage _targetStorage;
    private Processor _currentProcessor;
    public ActiveRecipe CurrentRecipeTask { get; private set; }
    private bool _hasCheckedIn;
    
    private float _nextRepathTime;
    private const float RepathInterval = 0.5f;
    
    private Coroutine _loadingCoroutine;
    private Coroutine _unloadingCoroutine;
    private Coroutine _assignmentCoroutine;

    public bool IsAssigned => _currentProcessor != null;

    public bool HasCheckedIn {
        get {
            return _hasCheckedIn;
        }
    }

    public Sprite DroneIcon {
        get {
            return droneIcon;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        movement = GetComponent<UnitMovement>();
    }

    private void Update()
    {
        DecideNextAction();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        UnitManager.Instance?.AddUnit(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnitManager.Instance?.RemoveUnit(this);
        ReleaseFromProcessor();
    }

    private void DecideNextAction()
    {
        switch (_currentState) {
        case DroneState.Idle:
            UpdateIdle();
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

    public void AssignProcessor(Processor processor, bool isManual = false)
    {
        ReleaseFromProcessor();

        _currentProcessor = processor;
        _hasCheckedIn = false;

        if (_currentProcessor != null) {
            _currentProcessor.AssignDrone(this);
            _currentState = DroneState.Idle;
            Debug.Log($"[Drone:{name}] Assigned to processor '{_currentProcessor.name}' (manual:{isManual}). Must check in before receiving tasks.");
        }
        else {
            _currentState = DroneState.Idle;
            Debug.Log($"[Drone:{name}] Unassigned from processor");
        }
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
            Debug.Log($"[Drone:{name}] Released from processor '{_currentProcessor.name}'");
        }
        _currentProcessor = null;
    }

    private void ReleaseFromRecipeTask()
    {
        if (CurrentRecipeTask != null && _currentProcessor != null) {
            _currentProcessor.ReleaseDroneFromRecipe(this);
            Debug.Log($"[Drone:{name}] Released from recipe {CurrentRecipeTask.recipeData.resourceType}");
        }
        CurrentRecipeTask = null;
    }

    private void UpdateIdle()
    {
        if (_currentProcessor == null)
            return;

        if (!_hasCheckedIn) {
            if (_assignmentCoroutine != null) {
                return;
            }
            
            bool isAtProcessorForCheckIn = movement.HasReachedTarget(movement.waypointTolerance + 0.1f);
            
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

        bool isAtProcessor = movement.HasReachedTarget(movement.waypointTolerance + 0.1f);
        
        if (isAtProcessor) {
            if (!movement.IsMoving) {
                movement.ForceStopAllMovement();
                AdjustSpriteDirectionToBuilding(_currentProcessor.transform);
                _currentProcessor.RequestTask(this);
            }
        }
        else {
            if (!movement.IsMoving) {
                Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
                bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                if (!hasPath) {
                    _currentProcessor.RequestTask(this);
                    Debug.Log($"[Drone:{name}] Idle: cannot path to processor '{_currentProcessor.name}', requesting task");
                }
            }
        }
    }

    private void UpdateFetching()
    {
        if (_targetStorage == null || _currentRequest == null) {
            SetTask_ReturnHome();
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
        
        if (_targetStorage != null) {
            AdjustSpriteDirectionToBuilding(((Component)_targetStorage).transform);
        }
        
        yield return new WaitForSeconds(loadingTime);
        
        if (_targetStorage != null && _currentRequest != null) {
            if (_targetStorage.TryWithdrawResource(_currentRequest.type, _currentRequest.amount, out int withdrawnAmount)) {
                if (withdrawnAmount > 0) {
                    _carriedResourceType = _currentRequest.type;
                    _carriedAmount = withdrawnAmount;
                    Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
                    movement.ResumeMovement(); // Resume movement before setting new target
                    movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                    _currentState = DroneState.DeliveringResource;
                    Debug.Log($"[Drone:{name}] Loaded {withdrawnAmount} {_carriedResourceType} from storage");
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
        if (!movement.IsMoving) {
            if (_unloadingCoroutine == null) {
                _unloadingCoroutine = StartCoroutine(UnloadingResourceCoroutine());
            }
        }
    }
    
    private IEnumerator UnloadingResourceCoroutine()
    {
        movement.ForceStopAllMovement();
        
        if (_currentProcessor != null) {
            AdjustSpriteDirectionToBuilding(_currentProcessor.transform);
        }
        
        yield return new WaitForSeconds(unloadingTime);
        
        if (_currentProcessor != null) {
            bool deposited = _currentProcessor.TryDepositIngredient(_carriedResourceType, _carriedAmount, this);

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
        
        if (_currentProcessor != null) {
            AdjustSpriteDirectionToBuilding(_currentProcessor.transform);
        }
        
        yield return new WaitForSeconds(assignmentTime);
        
        _hasCheckedIn = true;
        
        if (_currentProcessor != null) {
            _currentProcessor.RequestTask(this);
        }
        
        _assignmentCoroutine = null;
    }
    
    private void AdjustSpriteDirectionToBuilding(Transform buildingTransform)
    {
        if (buildingTransform == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null) return;
        if (!TryGetComponent<UnitSpriteController>(out var spriteController)) return;

        Vector3Int unitCell = BuildingManager.Instance.grid.WorldToCell(transform.position);
        Vector3Int buildingCell = BuildingManager.Instance.grid.WorldToCell(buildingTransform.position);
        
        Vector3Int targetCell = buildingCell;
        if (BuildingManager.Instance.GetBuildingAt(buildingCell, out List<Vector3Int> occupiedCells))
        {
            float minDistance = float.MaxValue;
            foreach (Vector3Int cell in occupiedCells)
            {
                float distance = Vector3.Distance(transform.position, BuildingManager.Instance.grid.GetCellCenterWorld(cell));
                if (distance < minDistance)
                {
                    minDistance = distance;
                    targetCell = cell;
                }
            }
        }

        Vector3Int relativePosition = targetCell - unitCell;
        Vector2 targetDirection = Vector2.zero;

        if (relativePosition.x > 0)
        {
            targetDirection = Vector2.right;
        }
        else if (relativePosition.x < 0)
        {
            targetDirection = Vector2.left;
        }
        else if (relativePosition.y > 0)
        {
            targetDirection = Vector2.up;
        }
        else if (relativePosition.y < 0)
        {
            targetDirection = Vector2.down;
        }

        spriteController.UpdateSpriteDirection(targetDirection);
    }

    private void UpdateProcessing()
    {
        if (CurrentRecipeTask == null) {
            SetTask_Idle();
            return;
        }

        bool isAtProcessor = movement.HasReachedTarget(movement.waypointTolerance + 0.1f);

        if (isAtProcessor) {
            if (movement.IsMoving) {
                movement.StopMovement();
            }
            
            AdjustSpriteDirectionToBuilding(_currentProcessor.transform);
            
            _currentProcessor.ProcessRecipeWork(CurrentRecipeTask, Time.deltaTime * processingSpeed);
            
            if (CurrentRecipeTask == null || (!CurrentRecipeTask.isProcessing && CurrentRecipeTask.assignedDrone == null)) {
                return;
            }
        }

        if (!movement.IsMoving && !isAtProcessor) {
            if (Time.time >= _nextRepathTime) {
                _nextRepathTime = Time.time + RepathInterval;
                Vector3 interactionPos = _currentProcessor.AssignInteractionCell(this);
                bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                if (hasPath) {
                    float distanceToTarget = movement.FinalTargetPosition != default 
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

    private enum DroneState
    {
        Idle,
        FetchingResource,
        DeliveringResource,
        Processing,
        ReturnHome
    }
}
