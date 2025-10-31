using UnityEngine;

[RequireComponent(typeof(UnitMovement))]
public class Unit_Drone : UnitBase
{
    private enum DroneState
    {
        Idle,
        FetchingResource,
        DeliveringResource,
        Processing,
        ReturnHome
    }

    [Header("Drone Settings")]
    [SerializeField] public int carryCapacity = 10;
    [SerializeField] private float processingSpeed = 1f;
    [SerializeField] private UnitMovement movement;

    private DroneState _currentState = DroneState.Idle;
    
    private ResourceProcessor _homeProcessor;
    private IStorage _targetStorage;
    private ResourceProcessor.ResourceRequest _currentRequest;
    
    private ResourceType _carriedResourceType;
    private int _carriedAmount;

    protected override void Awake()
    {
        base.Awake(); 
        movement = GetComponent<UnitMovement>();
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

    private void Update()
    {
        if (_homeProcessor == null) 
        {
            FindAndAssignClosestProcessor();
            if (_homeProcessor == null) return;
        }
        
        DecideNextAction();
    }

    private void DecideNextAction()
    {
        switch (_currentState)
        {
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

    public void AssignProcessor(ResourceProcessor processor)
    {
        ReleaseFromProcessor();
        
        _homeProcessor = processor;
        
        if (_homeProcessor != null)
        {
            _homeProcessor.AssignDrone(this);
            _currentState = DroneState.Idle;
        }
        else
        {
            _currentState = DroneState.Idle; 
        }
    }

    private void ReleaseFromProcessor()
    {
        if (_homeProcessor != null)
        {
            _homeProcessor.ReleaseDrone(this);
            if (_currentRequest != null)
            {
                _homeProcessor.CancelRequest(_currentRequest);
                _currentRequest = null;
            }
        }
        _homeProcessor = null;
    }

    private void FindAndAssignClosestProcessor()
    {
        ResourceProcessor closestProcessor = BuildingManager.Instance.FindClosestAvailableProcessor(transform.position);
        if (closestProcessor != null)
        {
            AssignProcessor(closestProcessor);
        }
    }

    private void UpdateIdle()
    {
        if (_homeProcessor == null)
            return;
        
        movement.SetNewTarget(_homeProcessor.GetPosition());
        
        if (!movement.IsMoving)
        {
            _homeProcessor.RequestTask(this);
        }
    }

    private void UpdateFetching()
    {
        if (_targetStorage == null || _currentRequest == null)
        {
            SetTask_ReturnHome();
            return;
        }

        if (!movement.IsMoving)
        {
            if (_targetStorage.TryWithdrawResource(_currentRequest.type, _currentRequest.amount, out int withdrawnAmount))
            {
                if (withdrawnAmount > 0)
                {
                    _carriedResourceType = _currentRequest.type;
                    _carriedAmount = withdrawnAmount;
                    movement.SetNewTarget(_homeProcessor.GetPosition());
                    _currentState = DroneState.DeliveringResource;
                }
                else
                {
                    SetTask_ReturnHome();
                }
            }
            else
            {
                SetTask_ReturnHome();
            }
        }
    }

    private void UpdateDelivering()
    {
        if (!movement.IsMoving)
        {
            _homeProcessor.TryDepositIngredient(_carriedResourceType, _carriedAmount, this);
            
            _carriedAmount = 0;
            _currentRequest = null;
            _currentState = DroneState.Idle;
        }
    }

    private void UpdateProcessing()
    {
        if (!movement.IsMoving)
        {
            if (!_homeProcessor.IsProcessing)
            {
                _currentState = DroneState.Idle;
                return;
            }
            
            _homeProcessor.ProcessRecipeWork(Time.deltaTime * processingSpeed);
        }
    }
    
    private void UpdateReturnHome()
    {
        movement.SetNewTarget(_homeProcessor.GetPosition());
        
        if (!movement.IsMoving)
        {
            _currentState = DroneState.Idle;
        }
    }
    
    public void SetTask_FetchResource(ResourceProcessor.ResourceRequest request, ResourceProcessor processor)
    {
        _currentRequest = request;
        _targetStorage = ResourceManager.Instance.FindClosestStorageWithResource(processor.GetPosition(), request.type, 1);
        
        if (_targetStorage != null)
        {
            movement.SetNewTarget(_targetStorage.GetPosition());
            _currentState = DroneState.FetchingResource;
        }
        else
        {
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
        if (_currentRequest != null)
        {
             _homeProcessor.CancelRequest(_currentRequest);
            _currentRequest = null;
        }
        
        _currentState = DroneState.ReturnHome;
        
        if (stopMovement)
        {
            movement.StopMovement();
        }
    }
}