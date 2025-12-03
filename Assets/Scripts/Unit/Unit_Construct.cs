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
    [SerializeField] private float constructionTime = 2f;
    [SerializeField] public UnitMovement movement;
    [SerializeField] private Sprite constructIcon;

    private int _carriedAmount;
    private ResourceType _carriedResourceType;
    private Coroutine _constructionCoroutine;

    private ConstructionSite _currentConstructionSite;
    private Vector3Int _currentPieceCell;
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
        if (ConstructionManager.Instance == null) {
            ConstructionManager existingManager = FindFirstObjectByType<ConstructionManager>();
            if (existingManager == null) {
                GameObject managerObj = new GameObject("ConstructionManager");
                managerObj.AddComponent<ConstructionManager>();
                Debug.LogWarning($"[Unit_Construct] Auto-created ConstructionManager GameObject for {name}");
            }
        }

        if (ConstructionManager.Instance != null && movement != null) {
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

        case ConstructState.Constructing:
            UpdateConstructing();
            break;
        }
    }

    public bool IsIdle()
    {
        return _currentState == ConstructState.Idle;
    }

    private void UpdateIdle()
    {
        if (TryRetryCommittedPiece())
        {
            return;
        }
        
        if (Time.time < _nextTaskRequestTime) {
            return;
        }
        
        if (ConstructionManager.Instance != null) {
            ConstructionManager.Instance.RequestTask(this);
        }
    }
    
    private bool TryRetryCommittedPiece()
    {
        if (_currentConstructionSite == null || _currentPieceCell == default)
        {
            return false;
        }
        
        if (!_currentConstructionSite.IsPieceCommittedTo(_currentPieceCell, this))
        {
            return false;
        }
        
        if (!_currentConstructionSite.HasPieceAllResources(_currentPieceCell) || _currentConstructionSite.IsPieceConstructed(_currentPieceCell))
        {
            return false;
        }
        
        if (!_currentConstructionSite.IsPieceAssignedToMe(_currentPieceCell, this))
        {
            if (!_currentConstructionSite.AssignDroneToPiece(_currentPieceCell, this))
            {
                return true;
            }
        }
        
        Vector3 interactionPos = _currentConstructionSite.AssignInteractionCell(this, _currentPieceCell);
        bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance, true);
        
        if (hasPath) {
            _currentState = ConstructState.Constructing;
            Debug.Log($"[Construct:{name}] Retrying construction of committed piece at {_currentPieceCell}");
        }
        
        return true;
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

        if (_targetStorage != null) {
            AdjustSpriteDirectionToBuilding((_targetStorage as Component).transform);
        }

        yield return new WaitForSeconds(loadingTime);

        if (_targetStorage != null && _currentRequest != null) {
            if (_targetStorage.TryWithdrawResource(_currentRequest.type, _currentRequest.amount, out int withdrawnAmount)) {
                if (withdrawnAmount > 0) {
                    _carriedResourceType = _currentRequest.type;
                    _carriedAmount = withdrawnAmount;
                    Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
                    Vector3 interactionPos = _currentRequest.site.AssignDeliveryInteractionCell(this, targetPieceCell);
                    movement.ResumeMovement();
                    movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                    _currentState = ConstructState.DeliveringResource;
                    Debug.Log($"[Construct:{name}] Loaded {withdrawnAmount} {_carriedResourceType} from storage, moving to delivery interaction cell at piece {targetPieceCell}");
                }
                else {
                    SetTask_Idle();
                }
            }
            else {
                SetTask_Idle();
            }
        }
        else {
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

        bool isAtDelivery = movement.HasReachedTarget(movement.waypointTolerance + 0.1f) &&
            !movement.IsMoving &&
            _rb.linearVelocity.sqrMagnitude < 0.01f;

        if (isAtDelivery) {
            _unloadingCoroutine = StartCoroutine(UnloadingResourceCoroutine());
        }
        else if (!movement.IsMoving && _rb.linearVelocity.sqrMagnitude < 0.01f) {
            if (Time.time >= _nextRepathTime) {
                _nextRepathTime = Time.time + RepathInterval;
                Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
                Vector3 interactionPos = _currentRequest.site.AssignDeliveryInteractionCell(this, targetPieceCell);
                bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                if (!hasPath) {
                    SetTask_Idle();
                }
            }
        }
    }

    private IEnumerator UnloadingResourceCoroutine()
    {
        movement.ForceStopAllMovement();

        if (_currentRequest.site != null && BuildingManager.Instance != null && BuildingManager.Instance.grid != null) {
            Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
            Vector3 piecePos = BuildingManager.Instance.grid.GetCellCenterWorld(targetPieceCell);
            AdjustSpriteDirectionToPosition(piecePos);
        }

        yield return new WaitForSeconds(unloadingTime);

        if (_currentRequest != null && _currentRequest.site != null) {
            ConstructionSite site = _currentRequest.site;
            bool deposited = site.TryDepositResource(_carriedResourceType, _carriedAmount, this);
            Debug.Log($"[Construct:{name}] Delivering: deposit {_carriedAmount} {_carriedResourceType} -> {(deposited ? "OK" : "FAILED")}");

            _carriedAmount = 0;

            site.ReleaseDrone(this);

            Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? site.cellPosition;

            _currentRequest = null;

            if (site.HasPieceAllResources(targetPieceCell)) {
                if (TryStartConstructingPiece(site, targetPieceCell)) {
                    Debug.Log($"[Construct:{name}] After delivering resources, immediately starting construction of piece at {targetPieceCell}");
                }
            }
            else {
                _currentState = ConstructState.Idle;
                if (ConstructionManager.Instance != null && site != null) {
                    ConstructionManager.Instance.OnSiteResourceDelivered(site);
                }
            }
        }
        else {
            _currentState = ConstructState.Idle;
        }

        _unloadingCoroutine = null;
    }

    private void UpdateConstructing()
    {
        if (_currentConstructionSite == null) {
            SetTask_Idle();
            return;
        }

        if (_constructionCoroutine != null) {
            return;
        }

        if (_currentPieceCell != default) {
            if (_currentConstructionSite.IsPieceConstructed(_currentPieceCell)) {
                Debug.Log($"[Construct:{name}] Piece at {_currentPieceCell} already constructed by another unit, releasing commitment");
                ReleaseCurrentPiece();
                SetTask_Idle();
                return;
            }

            if (!_currentConstructionSite.IsPieceAssignedToMe(_currentPieceCell, this) && !_currentConstructionSite.IsPieceCommittedTo(_currentPieceCell, this)) {
                Debug.Log($"[Construct:{name}] Piece at {_currentPieceCell} assigned to another unit and not committed to us, releasing");
                ReleaseCurrentPiece();
                SetTask_Idle();
                return;
            }
        }

        bool isAtSite = movement.HasReachedTarget(movement.waypointTolerance + 0.1f) &&
            !movement.IsMoving &&
            _rb.linearVelocity.sqrMagnitude < 0.01f;

        if (isAtSite) {
            _constructionCoroutine = StartCoroutine(ConstructionCoroutine());
        }
        else if (!movement.IsMoving && _rb.linearVelocity.sqrMagnitude < 0.01f) {
            if (Time.time >= _nextRepathTime) {
                _nextRepathTime = Time.time + RepathInterval;
                Vector3 interactionPos = _currentConstructionSite.AssignInteractionCell(this, _currentPieceCell);
                bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance, true);
                if (!hasPath) {
                    SetTask_Idle();
                }
            }
        }
    }

    private IEnumerator ConstructionCoroutine()
    {
        movement.ForceStopAllMovement();

        if (_currentConstructionSite != null && BuildingManager.Instance != null && BuildingManager.Instance.grid != null) {
            Vector3 pieceWorldPos = BuildingManager.Instance.grid.GetCellCenterWorld(_currentPieceCell);
            AdjustSpriteDirectionToPosition(pieceWorldPos);
        }

        yield return new WaitForSeconds(constructionTime);

        ConstructionSite site = _currentConstructionSite;
        Vector3Int pieceCell = _currentPieceCell;

        if (site == null || site.comboCardData == null || BuildingManager.Instance == null) {
            if (site != null) {
                site.ReleaseDrone(this);
                if (pieceCell != default) {
                    site.ReleaseDroneFromPiece(pieceCell, this);
                }
            }
            _currentConstructionSite = null;
            _currentPieceCell = default;
            _currentState = ConstructState.Idle;
            _constructionCoroutine = null;
            yield break;
        }

        BuildingManager.Instance.PlaceBuildingPieceAtCell(site.comboCardData, pieceCell, site.cellPosition);

        BuildingPiece placedPiece = BuildingManager.Instance.GetPieceAt(pieceCell);
        if (placedPiece != null) {
            HandleConstructionSuccess(site, pieceCell);
        }
        else {
            HandleConstructionFailure(site, pieceCell);
        }

        _currentState = ConstructState.Idle;
        _constructionCoroutine = null;
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

    private Vector2 CalculateDirection(Vector3Int from, Vector3Int to)
    {
        Vector3Int relativePosition = to - from;

        if (relativePosition.x > 0) return Vector2.right;
        if (relativePosition.x < 0) return Vector2.left;
        if (relativePosition.y > 0) return Vector2.up;
        if (relativePosition.y < 0) return Vector2.down;

        return Vector2.zero;
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

        if (_targetStorage != null) {
            bool hasPath = movement.SetNewTarget(_targetStorage.GetPosition());
            if (hasPath) {
                _currentState = ConstructState.FetchingResource;
                Debug.Log($"[Construct:{name}] State -> FetchingResource: {_currentRequest.amount} {_currentRequest.type} from '{(_targetStorage as Object)?.name}'");
                return;
            }
        }

        if (_currentRequest != null && _currentRequest.site != null) {
            _currentRequest.site.CancelRequest(_currentRequest);
        }
        _nextTaskRequestTime = Time.time + TaskRequestCooldown;
        SetTask_Idle();
        Debug.Log($"[Construct:{name}] Fetch failed: no storage or path blocked - request cancelled");
    }

    public void SetTask_ConstructPiece(ConstructionSite site, Vector3Int pieceCell)
    {
        if (site == null) {
            SetTask_Idle();
            return;
        }

        if (movement == null) {
            Debug.LogWarning($"[Construct:{name}] Cannot set construct task: movement component not initialized");
            return;
        }

        Vector3 interactionPos = site.AssignInteractionCell(this, pieceCell);
        bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance, true);

        if (!hasPath) {
            SetTask_Idle();
            Debug.Log($"[Construct:{name}] Construct piece FAILED: cannot path to piece at {pieceCell}");
            return;
        }

        _currentState = ConstructState.Constructing;
        _currentConstructionSite = site;
        _currentPieceCell = pieceCell;

        Debug.Log($"[Construct:{name}] State -> Constructing: piece at {pieceCell}");
    }

    private void SetTask_Idle()
    {
        ReleaseFromConstruction();
        _currentState = ConstructState.Idle;
        Debug.Log($"[Construct:{name}] State -> Idle");
    }
    
    public void SetTaskRequestCooldown(float duration)
    {
        _nextTaskRequestTime = Time.time + duration;
    }

    private void ReleaseCurrentPiece()
    {
        if (_currentConstructionSite == null || _currentPieceCell == default) return;
        
        if (!_currentConstructionSite.IsPieceConstructed(_currentPieceCell)) {
            _currentConstructionSite.ReleaseCommitmentFromPiece(_currentPieceCell, this);
        }
        _currentConstructionSite.ReleaseDroneFromPiece(_currentPieceCell, this);
        _currentConstructionSite.ReleaseDrone(this);
        _currentConstructionSite = null;
        _currentPieceCell = default;
    }
    
    private void ReleaseFromConstruction()
    {
        if (_currentRequest != null && _currentRequest.site != null) {
            _currentRequest.site.CancelRequest(_currentRequest);
            _currentRequest = null;
        }

        if (_currentConstructionSite != null) {
            if (_currentPieceCell != default) {
                ReleaseCurrentPiece();
            } else {
                _currentConstructionSite.ReleaseDrone(this);
                _currentConstructionSite = null;
            }
        }

        StopAllCoroutines();
        _loadingCoroutine = null;
        _unloadingCoroutine = null;
        _constructionCoroutine = null;
    }

    private bool TryStartConstructingPiece(ConstructionSite site, Vector3Int pieceCell)
    {
        site.CommitDroneToPiece(pieceCell, this);
        _currentConstructionSite = site;
        _currentPieceCell = pieceCell;
        
        if (!site.AssignDroneToPiece(pieceCell, this)) {
            _currentState = ConstructState.Idle;
            Debug.Log($"[Construct:{name}] Committed to piece at {pieceCell}, will retry construction in idle state");
            return false;
        }
        
        Vector3 interactionPos = site.AssignInteractionCell(this, pieceCell);
        bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance, true);
        
        if (hasPath) {
            _currentState = ConstructState.Constructing;
            return true;
        }
        
        _currentState = ConstructState.Idle;
        Debug.Log($"[Construct:{name}] Committed to piece at {pieceCell}, pathfinding failed - will retry in idle state");
        return false;
    }
    
    private void HandleConstructionSuccess(ConstructionSite site, Vector3Int pieceCell)
    {
        site.MarkPieceConstructed(pieceCell, this);
        site.ReleaseCommitmentFromPiece(pieceCell, this);
        Debug.Log($"[Construct:{name}] Constructed piece at {pieceCell} for '{site.comboCardData.displayName}'");

        if (ConstructionManager.Instance != null) {
            ConstructionManager.Instance.OnPieceConstructed(site);
        }

        site.ReleaseDrone(this);

        if (site.AreAllPiecesConstructed()) {
            Destroy(site.gameObject);
            _currentConstructionSite = null;
        }
        else {
            site.ReleaseDroneFromPiece(pieceCell, this);
            _currentConstructionSite = null;
            _currentPieceCell = default;
        }
    }
    
    private void HandleConstructionFailure(ConstructionSite site, Vector3Int pieceCell)
    {
        Debug.LogError($"[Construct:{name}] Failed to construct piece at {pieceCell} - piece was not placed! Site: {site.name}, Combo: {site.comboCardData?.displayName}");
        site.ReleaseCommitmentFromPiece(pieceCell, this);
        site.ReleaseDroneFromPiece(pieceCell, this);
        site.ReleaseDrone(this);
        _currentConstructionSite = null;
        _currentPieceCell = default;
    }

    private enum ConstructState
    {
        Idle,
        FetchingResource,
        DeliveringResource,
        Constructing
    }
}
