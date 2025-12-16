using System.Collections;
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

    protected override void Awake()
    {
        base.Awake();
        movement = GetComponent<UnitMovement>();
        _rb = GetComponent<Rigidbody2D>();
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

    private void Start()
    {
        if (ConstructionManager.Instance == null)
        {
            ConstructionManager existingManager = FindFirstObjectByType<ConstructionManager>();
            if (existingManager == null)
            {
                GameObject managerObj = new GameObject("ConstructionManager");
                managerObj.AddComponent<ConstructionManager>();
                Debug.LogWarning($"[Unit_Construct] Auto-created ConstructionManager GameObject for {name}");
            }
        }

        if (ConstructionManager.Instance != null && movement != null)
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
            AdjustSpriteDirectionToBuilding((_targetStorage as Component).transform);
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
                Debug.Log($"[Construct:{name}] Cannot deposit resources - request invalid. Returning resources and aborting.");
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
            _unloadingCoroutine = null;
            SetTask_Idle();
            yield break;
        }
        
        if (!CanDepositResource())
        {
            _unloadingCoroutine = null;
            SetTask_Idle();
            yield break;
        }

        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
        {
            Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
            Vector3 piecePos = BuildingManager.Instance.grid.GetCellCenterWorld(targetPieceCell);
            AdjustSpriteDirectionToPosition(piecePos);
        }

        yield return new WaitForSeconds(unloadingTime);
        
        if (!CanDepositResource())
        {
            _unloadingCoroutine = null;
            SetTask_Idle();
            yield break;
        }

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
            Debug.LogWarning($"[Construct:{name}] Cannot set fetch task: movement component not initialized");
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
                Debug.Log($"[Construct:{name}] State -> FetchingResource: {_currentRequest.amount} {_currentRequest.type} from '{(_targetStorage as Object)?.name}'");
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

    private enum ConstructState
    {
        Idle,
        FetchingResource,
        DeliveringResource
    }
}
