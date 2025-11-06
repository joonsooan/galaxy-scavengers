using UnityEngine;

public class Unit_Drone : UnitBase
{
    [Header("Drone Settings")]
    [SerializeField] public int carryCapacity = 10;
    [SerializeField] private float processingSpeed = 1f;
    [SerializeField] private UnitMovement movement;
    [SerializeField] private Sprite droneIcon;

    private int _carriedAmount;
    private ResourceType _carriedResourceType;
    private ResourceProcessor.ResourceRequest _currentRequest;
    private DroneState _currentState = DroneState.Idle;

    // private bool _isManuallyAssigned;
    private IStorage _targetStorage;
    private ResourceProcessor currentProcessor;
    public ActiveRecipe CurrentRecipeTask { get; private set; }
    
    private float _nextRepathTime;
    private const float RepathInterval = 0.5f;

    public bool IsAssigned {
        get {
            return currentProcessor != null;
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

    public void AssignProcessor(ResourceProcessor processor, bool isManual = false)
    {
        ReleaseFromProcessor();

        currentProcessor = processor;

        // _isManuallyAssigned = isManual;

        if (currentProcessor != null) {
            currentProcessor.AssignDrone(this);
            _currentState = DroneState.Idle;
            Debug.Log($"[Drone:{name}] Assigned to processor '{currentProcessor.name}' (manual:{isManual})");
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

        if (!movement.IsMoving) {
            bool hasPath = movement.SetNewTarget(currentProcessor.GetPosition());
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
                    movement.SetNewTarget(currentProcessor.GetPosition());
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
            bool deposited = currentProcessor.TryDepositIngredient(_carriedResourceType, _carriedAmount, this);
            Debug.Log($"[Drone:{name}] Delivering: deposit {_carriedAmount} {_carriedResourceType} to '{currentProcessor.name}' -> {(deposited ? "OK" : "FAILED")}" );

            _carriedAmount = 0;
            _currentRequest = null;
            _currentState = DroneState.Idle;
            Debug.Log($"[Drone:{name}] State -> Idle (after delivery)");
        }
    }

    private void UpdateProcessing()
    {
        if (CurrentRecipeTask == null) {
            SetTask_Idle();
            return;
        }

        float distanceToProcessor = currentProcessor != null
            ? Vector2.Distance(transform.position, currentProcessor.GetPosition())
            : float.MaxValue;

        bool isAtProcessor = distanceToProcessor <= (movement.waypointTolerance + 0.1f);

        if (isAtProcessor) {
            if (!CurrentRecipeTask.isProcessing) {
                SetTask_Idle();
                return;
            }
            currentProcessor.ProcessRecipeWork(CurrentRecipeTask, Time.deltaTime * processingSpeed);
            return;
        }

        if (!movement.IsMoving && currentProcessor != null) {
            if (Time.time >= _nextRepathTime) {
                _nextRepathTime = Time.time + RepathInterval;
                bool hasPath = movement.SetNewTarget(currentProcessor.GetPosition());
                if (hasPath) {
                    Debug.Log($"[Drone:{name}] Processing: re-issuing move order to processor '{currentProcessor.name}'");
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
        
        movement.SetNewTarget(currentProcessor.GetPosition());

        if (!movement.IsMoving) {
            _currentState = DroneState.Idle;
        }
    }

    public void SetTask_FetchResource(ResourceProcessor.ResourceRequest request, ResourceProcessor processor)
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

    public void SetTask_Process(ResourceProcessor processor, ActiveRecipe recipeTask)
    {
        bool hasPath = movement.SetNewTarget(processor.GetPosition());
        
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
