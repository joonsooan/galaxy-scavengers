using UnityEngine;

[RequireComponent(typeof(UnitMovement))]
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

    private bool _isManuallyAssigned;

    private IStorage _targetStorage;
    private ResourceProcessor currentProcessor;

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
        if (currentProcessor == null) {
            // 자동 모드인 경우
            if (!_isManuallyAssigned) {
                FindAndAssignClosestProcessor();
            }
            if (currentProcessor == null) return;
        }

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
        _isManuallyAssigned = isManual;

        if (currentProcessor != null) {
            currentProcessor.AssignDrone(this);
            _currentState = DroneState.Idle;
        }
        else {
            _currentState = DroneState.Idle;
        }
    }

    private void ReleaseFromProcessor()
    {
        if (currentProcessor != null) {
            currentProcessor.ReleaseDrone(this);
            if (_currentRequest != null) {
                currentProcessor.CancelRequest(_currentRequest);
                _currentRequest = null;
            }
        }
        currentProcessor = null;
    }

    private void FindAndAssignClosestProcessor()
    {
        ResourceProcessor closestProcessor = BuildingManager.Instance.FindClosestAvailableProcessor(transform.position);
        if (closestProcessor != null) {
            AssignProcessor(closestProcessor);
        }
    }

    private void UpdateIdle()
    {
        if (currentProcessor == null)
            return;

        movement.SetNewTarget(currentProcessor.GetPosition());

        if (!movement.IsMoving) {
            currentProcessor.RequestTask(this);
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
            currentProcessor.TryDepositIngredient(_carriedResourceType, _carriedAmount, this);

            _carriedAmount = 0;
            _currentRequest = null;
            _currentState = DroneState.Idle;
        }
    }

    private void UpdateProcessing()
    {
        if (!movement.IsMoving) {
            if (!currentProcessor.IsProcessing) {
                _currentState = DroneState.Idle;
                return;
            }

            currentProcessor.ProcessRecipeWork(Time.deltaTime * processingSpeed);
        }
    }

    private void UpdateReturnHome()
    {
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
            movement.SetNewTarget(_targetStorage.GetPosition());
            _currentState = DroneState.FetchingResource;
        }
        else {
            SetTask_ReturnHome();
        }
    }

    public void SetTask_Process(ResourceProcessor processor)
    {
        movement.SetNewTarget(processor.GetPosition());
        _currentState = DroneState.Processing;
    }

    public void SetTask_Idle()
    {
        _currentState = DroneState.Idle;
    }

    private void SetTask_ReturnHome(bool stopMovement = false)
    {
        if (_currentRequest != null) {
            currentProcessor.CancelRequest(_currentRequest);
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
