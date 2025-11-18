using System.Collections.Generic;
using UnityEngine;

public class Unit_Drone : UnitBase
{
    [Header("Drone Settings")]
    [SerializeField] public int carryCapacity = 10;
    [SerializeField] private float processingSpeed = 1f;
    [SerializeField] private UnitMovement movement;
    [SerializeField] private Sprite droneIcon;
    // [SerializeField] private float interactionDistance = 1.1f;

    private int _carriedAmount;
    private ResourceType _carriedResourceType;
    private Processor.ResourceRequest _currentRequest;
    private DroneState _currentState = DroneState.Idle;

    // private bool _isManuallyAssigned;
    private IStorage _targetStorage;
    private Processor currentProcessor;
    public ActiveRecipe CurrentRecipeTask { get; private set; }
    private bool _hasCheckedIn = false;
    
    private float _nextRepathTime;
    private const float RepathInterval = 0.5f;

    public bool IsAssigned {
        get {
            return currentProcessor != null;
        }
    }

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
        // if (currentProcessor == null) {
        //     // 자동 모드인 경우
        //     if (!_isManuallyAssigned) {
        //         FindAndAssignClosestProcessor();
        //     }
        //     if (currentProcessor == null) return;
        // }

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

        currentProcessor = processor;
        _hasCheckedIn = false; // Reset check-in status when assigned to a new processor

        // _isManuallyAssigned = isManual;

        if (currentProcessor != null) {
            currentProcessor.AssignDrone(this);
            _currentState = DroneState.Idle;
            Debug.Log($"[Drone:{name}] Assigned to processor '{currentProcessor.name}' (manual:{isManual}). Must check in before receiving tasks.");
        }
        else {
            _currentState = DroneState.Idle;
            Debug.Log($"[Drone:{name}] Unassigned from processor");
        }
    }

    private void ReleaseFromProcessor()
    {
        ReleaseFromRecipeTask();

        if (currentProcessor != null) {
            currentProcessor.ReleaseDrone(this);
            if (_currentRequest != null) {
                currentProcessor.CancelRequest(_currentRequest);
                _currentRequest = null;
            }
            Debug.Log($"[Drone:{name}] Released from processor '{currentProcessor.name}'");
        }
        currentProcessor = null;
    }

    private void ReleaseFromRecipeTask()
    {
        if (CurrentRecipeTask != null && currentProcessor != null) {
            currentProcessor.ReleaseDroneFromRecipe(this);
            Debug.Log($"[Drone:{name}] Released from recipe {CurrentRecipeTask.recipeData.resourceType}");
        }
        CurrentRecipeTask = null;
    }

    // private void FindAndAssignClosestProcessor()
    // {
    //     ResourceProcessor closestProcessor = BuildingManager.Instance.FindClosestAvailableProcessor(transform.position);
    //     if (closestProcessor != null) {
    //         AssignProcessor(closestProcessor);
    //     }
    // }

    private void UpdateIdle()
    {
        if (currentProcessor == null)
            return;

        // Check if drone needs to check in (arrive at processor for the first time)
        if (!_hasCheckedIn) {
            Vector3 processorPos = currentProcessor.GetPosition();
            float distanceToProcessor = Vector3.Distance(transform.position, processorPos);
            float checkInDistance = movement.waypointTolerance + 0.1f;
            
            // Check if we're already at the processor (either by reaching target or by physical distance)
            bool isAtProcessor = movement.HasReachedTarget(checkInDistance) || 
                                 distanceToProcessor <= checkInDistance;
            
            if (isAtProcessor) {
                // Drone has arrived at processor - check in
                _hasCheckedIn = true;
                Debug.Log($"[Drone:{name}] Checked in at processor '{currentProcessor.name}'. Now available for tasks.");
                
                // Face the processor building when checking in
                AdjustSpriteDirectionToBuilding(currentProcessor.transform);
                
                // After checking in, request a task
                if (currentProcessor != null) {
                    currentProcessor.RequestTask(this);
                }
                return;
            }
            
            // Not at processor yet - move towards it using assigned interaction cell
            if (!movement.IsMoving) {
                Vector3 interactionPos = currentProcessor.AssignInteractionCell(this);
                bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                if (!hasPath) {
                    // Can't path to processor, but still need to check in
                    // Mark as checked in anyway so it doesn't get stuck
                    _hasCheckedIn = true;
                    Debug.Log($"[Drone:{name}] Cannot path to processor '{currentProcessor.name}', marking as checked in anyway");
                    currentProcessor.RequestTask(this);
                }
            }
            return;
        }

        // Already checked in - normal idle behavior
        if (!movement.IsMoving) {
            Vector3 interactionPos = currentProcessor.AssignInteractionCell(this);
            bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
            if (!hasPath) {
                currentProcessor.RequestTask(this);
                Debug.Log($"[Drone:{name}] Idle: cannot path to processor '{currentProcessor.name}', requesting task");
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
            if (_targetStorage.TryWithdrawResource(_currentRequest.type, _currentRequest.amount, out int withdrawnAmount)) {
                if (withdrawnAmount > 0) {
                    _carriedResourceType = _currentRequest.type;
                    _carriedAmount = withdrawnAmount;
                    Vector3 interactionPos = currentProcessor.AssignInteractionCell(this);
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
    }

    private void UpdateDelivering()
    {
        if (!movement.IsMoving) {
            // Face the processor building before depositing
            if (currentProcessor != null) {
                AdjustSpriteDirectionToBuilding(currentProcessor.transform);
            }
            
            bool deposited = currentProcessor.TryDepositIngredient(_carriedResourceType, _carriedAmount, this);
            Debug.Log($"[Drone:{name}] Delivering: deposit {_carriedAmount} {_carriedResourceType} to '{currentProcessor.name}' -> {(deposited ? "OK" : "FAILED")}" );

            _carriedAmount = 0;
            _currentRequest = null;
            _currentState = DroneState.Idle;
            Debug.Log($"[Drone:{name}] State -> Idle (after delivery)");
            
            if (currentProcessor != null) {
                currentProcessor.RequestTask(this);
            }
        }
    }
    
    private void AdjustSpriteDirectionToBuilding(Transform buildingTransform)
    {
        if (buildingTransform == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null) return;
        if (!TryGetComponent<UnitSpriteController>(out var spriteController)) return;

        Vector3Int unitCell = BuildingManager.Instance.grid.WorldToCell(transform.position);
        Vector3Int buildingCell = BuildingManager.Instance.grid.WorldToCell(buildingTransform.position);
        
        // For large buildings, find the nearest occupied cell
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
            
            // Face the processor building
            AdjustSpriteDirectionToBuilding(currentProcessor.transform);
            
            currentProcessor.ProcessRecipeWork(CurrentRecipeTask, Time.deltaTime * processingSpeed);
            
            // Check if processing is complete (recipe should be cleared by ProcessRecipeWork)
            if (CurrentRecipeTask == null || (!CurrentRecipeTask.isProcessing && CurrentRecipeTask.assignedDrone == null)) {
                // Processing is done, but ProcessRecipeWork already set the drone to Idle
                // This is just a safety check
                return;
            }
        }

        if (!movement.IsMoving && !isAtProcessor) {
            // Only attempt to re-path if enough time has passed
            if (Time.time >= _nextRepathTime) {
                _nextRepathTime = Time.time + RepathInterval;
                Vector3 interactionPos = currentProcessor.AssignInteractionCell(this);
                bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                if (hasPath) {
                    float distanceToTarget = movement.FinalTargetPosition != default 
                        ? Vector3.Distance(transform.position, movement.FinalTargetPosition)
                        : 0f;
                    Debug.Log($"[Drone:{name}] Processing: moving to processor '{currentProcessor.name}' for recipe {CurrentRecipeTask.recipeData.resourceType} (distance to target: {distanceToTarget:F2})");
                }
                else {
                    Debug.Log($"[Drone:{name}] Processing: cannot path to processor '{currentProcessor.name}', going Idle");
                    SetTask_Idle();
                }
            }
        }
    }

    private void UpdateReturnHome()
    {
        if (currentProcessor == null) {
            movement.StopMovement();
            _currentState = DroneState.Idle;
            return;
        }
        
        Vector3 interactionPos = currentProcessor.AssignInteractionCell(this);
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
                Debug.Log($"[Drone:{name}] State -> FetchingResource: {_currentRequest.amount} {_currentRequest.type} from '{(_targetStorage as Object)?.name}' for processor '{processor.name}'");
                return;
            }
        }

        SetTask_ReturnHome(true);
        Debug.Log($"[Drone:{name}] Fetch failed: no storage or path blocked. Returning home.");
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
        Debug.Log($"[Drone:{name}] State -> Processing: {recipeTask.recipeData.resourceType} on '{processor.name}'");
    }

    public void SetTask_Idle()
    {
        ReleaseFromRecipeTask();
        _currentState = DroneState.Idle;
        Debug.Log($"[Drone:{name}] State -> Idle");
    }

    private void SetTask_ReturnHome(bool stopMovement = false)
    {
        ReleaseFromRecipeTask();

        if (_currentRequest != null) {
            currentProcessor?.CancelRequest(_currentRequest);
            _currentRequest = null;
        }

        _currentState = DroneState.ReturnHome;
        Debug.Log($"[Drone:{name}] State -> ReturnHome (stopMovement:{stopMovement})");

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
