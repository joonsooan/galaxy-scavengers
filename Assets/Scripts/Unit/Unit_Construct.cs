using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit_Construct : UnitBase
{
    [Header("Construct Drone Settings")]
    [SerializeField] public int carryCapacity = 10;
    [SerializeField] private float constructionSpeed = 1f;
    [SerializeField] private float loadingTime = 1f;
    [SerializeField] private float unloadingTime = 1f;
    [SerializeField] private float constructionTime = 2f;
    [SerializeField] private UnitMovement movement;
    [SerializeField] private Sprite constructIcon;
    
    private int _carriedAmount;
    private ResourceType _carriedResourceType;
    private ConstructionSite.ConstructionRequest _currentRequest;
    private ConstructState _currentState = ConstructState.Idle;
    
    private IStorage _targetStorage;
    private ConstructionSite _currentConstructionSite;
    private Vector3Int _currentPieceCell;
    private float _constructionProgress = 0f;
    
    private float _nextRepathTime;
    private const float RepathInterval = 0.5f;
    
    private Coroutine _loadingCoroutine;
    private Coroutine _unloadingCoroutine;
    private Coroutine _constructionCoroutine;
    
    private Rigidbody2D _rb;
    
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
        
        // Ensure ConstructionManager exists before registering
        if (ConstructionManager.Instance == null)
        {
            // Try to find it in the scene
            ConstructionManager existingManager = FindFirstObjectByType<ConstructionManager>();
            if (existingManager == null)
            {
                // Auto-create ConstructionManager if it doesn't exist
                GameObject managerObj = new GameObject("ConstructionManager");
                managerObj.AddComponent<ConstructionManager>();
                Debug.LogWarning($"[Unit_Construct] Auto-created ConstructionManager GameObject for {name}");
            }
        }
        
        // Register this drone with ConstructionManager
        if (ConstructionManager.Instance != null)
        {
            ConstructionManager.Instance.RegisterConstructDrone(this);
        }
        // else
        // {
        //     Debug.LogError($"[Unit_Construct] Failed to register {name}: ConstructionManager.Instance is null");
        // }
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
        // Request task from ConstructionManager
        if (ConstructionManager.Instance != null)
        {
            ConstructionManager.Instance.RequestTask(this);
        }
    }
    
    private void UpdateFetching()
    {
        if (_targetStorage == null || _currentRequest == null)
        {
            SetTask_Idle();
            return;
        }
        
        if (!movement.IsMoving)
        {
            if (_loadingCoroutine == null)
            {
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
        
        if (_targetStorage != null && _currentRequest != null)
        {
            if (_targetStorage.TryWithdrawResource(_currentRequest.type, _currentRequest.amount, out int withdrawnAmount))
            {
                if (withdrawnAmount > 0)
                {
                    _carriedResourceType = _currentRequest.type;
                    _carriedAmount = withdrawnAmount;
                    // Get interaction cell for delivery (around the piece cell that needs this resource)
                    Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
                    Vector3 interactionPos = _currentRequest.site.AssignDeliveryInteractionCell(this, targetPieceCell);
                    movement.ResumeMovement();
                    movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                    _currentState = ConstructState.DeliveringResource;
                    Debug.Log($"[Construct:{name}] Loaded {withdrawnAmount} {_carriedResourceType} from storage, moving to delivery interaction cell at piece {targetPieceCell}");
                }
                else
                {
                    SetTask_Idle();
                }
            }
            else
            {
                SetTask_Idle();
            }
        }
        else
        {
            SetTask_Idle();
        }
        
        _loadingCoroutine = null;
    }
    
    private void UpdateDelivering()
    {
        if (_currentRequest == null || _currentRequest.site == null)
        {
            SetTask_Idle();
            return;
        }
        
        // If unloading coroutine is running, don't do anything
        if (_unloadingCoroutine != null)
        {
            return;
        }
        
        // Check if we've reached the delivery interaction position
        // Use a tighter tolerance and check that we're not aligning
        bool isAtDelivery = movement.HasReachedTarget(movement.waypointTolerance + 0.1f) && 
                           !movement.IsMoving &&
                           _rb.linearVelocity.sqrMagnitude < 0.01f;
        
        if (isAtDelivery)
        {
            _unloadingCoroutine = StartCoroutine(UnloadingResourceCoroutine());
        }
        else if (!movement.IsMoving && _rb.linearVelocity.sqrMagnitude < 0.01f)
        {
            // Repath to delivery interaction cell if we're not moving and not aligning
            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + RepathInterval;
                Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
                Vector3 interactionPos = _currentRequest.site.AssignDeliveryInteractionCell(this, targetPieceCell);
                bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance);
                if (!hasPath)
                {
                    SetTask_Idle();
                }
            }
        }
    }
    
    private IEnumerator UnloadingResourceCoroutine()
    {
        // Force stop all movement including alignment to prevent jittering
        movement.ForceStopAllMovement();
        
        // Adjust sprite direction to the piece cell that needs this resource (only once)
        if (_currentRequest.site != null && BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
        {
            Vector3Int targetPieceCell = _currentRequest.targetPieceCell ?? _currentRequest.site.cellPosition;
            Vector3 piecePos = BuildingManager.Instance.grid.GetCellCenterWorld(targetPieceCell);
            AdjustSpriteDirectionToPosition(piecePos);
        }
        
        // Wait for unloading time - unit should remain completely still
        yield return new WaitForSeconds(unloadingTime);
        
        if (_currentRequest != null && _currentRequest.site != null)
        {
            ConstructionSite site = _currentRequest.site;
            bool deposited = site.TryDepositResource(_carriedResourceType, _carriedAmount, this);
            Debug.Log($"[Construct:{name}] Delivering: deposit {_carriedAmount} {_carriedResourceType} -> {(deposited ? "OK" : "FAILED")}");
            
            _carriedAmount = 0;
            
            // Release interaction cell assignment for delivery
            site.ReleaseDrone(this);
            
            // Notify construction manager before clearing request
            // Pass this drone so it can be immediately assigned to construct if site has all resources
            if (ConstructionManager.Instance != null && site != null)
            {
                ConstructionManager.Instance.OnSiteResourceDelivered(site, this);
            }
            
            _currentRequest = null;
            _currentState = ConstructState.Idle;
        }
        else
        {
            _currentState = ConstructState.Idle;
        }
        
        _unloadingCoroutine = null;
    }
    
    private void UpdateConstructing()
    {
        if (_currentConstructionSite == null)
        {
            SetTask_Idle();
            return;
        }
        
        // If construction coroutine is running, don't do anything
        if (_constructionCoroutine != null)
        {
            return;
        }
        
        // Check if we've reached the interaction cell and are fully aligned
        // Use a tighter check to ensure we're not still aligning
        bool isAtSite = movement.HasReachedTarget(movement.waypointTolerance + 0.1f) && 
                       !movement.IsMoving &&
                       _rb.linearVelocity.sqrMagnitude < 0.01f;
        
        if (isAtSite)
        {
            _constructionCoroutine = StartCoroutine(ConstructionCoroutine());
        }
        else if (!movement.IsMoving && _rb.linearVelocity.sqrMagnitude < 0.01f)
        {
            // Repath to interaction position if we're not moving and not aligning
            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + RepathInterval;
                // Get interaction position for this specific piece
                Vector3 interactionPos = _currentConstructionSite.AssignInteractionCell(this, _currentPieceCell);
                // Disable alignment to prevent jittering during construction
                bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance, true);
                if (!hasPath)
                {
                    SetTask_Idle();
                }
            }
        }
    }
    
    private IEnumerator ConstructionCoroutine()
    {
        // Force stop all movement including alignment to prevent jittering
        movement.ForceStopAllMovement();
        
        // Adjust sprite direction to the piece being constructed (only once)
        if (_currentConstructionSite != null && BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
        {
            Vector3 pieceWorldPos = BuildingManager.Instance.grid.GetCellCenterWorld(_currentPieceCell);
            AdjustSpriteDirectionToPosition(pieceWorldPos);
        }
        
        // Wait for construction time - unit should remain completely still
        yield return new WaitForSeconds(constructionTime);
        
        if (_currentConstructionSite != null && _currentConstructionSite.comboCardData != null)
        {
            // Place this specific building piece
            BuildingManager.Instance.PlaceBuildingPieceAtCell(_currentConstructionSite.comboCardData, _currentPieceCell, _currentConstructionSite.cellPosition);
            
            // Mark piece as constructed and release drone assignment
            _currentConstructionSite.MarkPieceConstructed(_currentPieceCell, this);
            
            Debug.Log($"[Construct:{name}] Constructed piece at {_currentPieceCell} for '{_currentConstructionSite.comboCardData.displayName}'");
            
            // Notify construction manager
            if (ConstructionManager.Instance != null)
            {
                ConstructionManager.Instance.OnPieceConstructed(_currentConstructionSite);
            }
            
            // Release drone from interaction cell
            _currentConstructionSite.ReleaseDrone(this);
            
            // If all pieces are done, destroy the site
            if (_currentConstructionSite.AreAllPiecesConstructed())
            {
                Destroy(_currentConstructionSite.gameObject);
                _currentConstructionSite = null;
            }
        }
        
        _currentConstructionSite = null;
        _currentPieceCell = default;
        _currentState = ConstructState.Idle;
        _constructionCoroutine = null;
    }
    
    private void AdjustSpriteDirectionToPosition(Vector3 targetPosition)
    {
        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null) return;
        if (!TryGetComponent<UnitSpriteController>(out var spriteController)) return;
        
        Vector3Int unitCell = BuildingManager.Instance.grid.WorldToCell(transform.position);
        Vector3Int targetCell = BuildingManager.Instance.grid.WorldToCell(targetPosition);
        
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
    
    private void AdjustSpriteDirectionToBuilding(Transform buildingTransform)
    {
        if (buildingTransform == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null) return;
        if (!TryGetComponent<UnitSpriteController>(out var spriteController)) return;
        
        Vector3Int unitCell = BuildingManager.Instance.grid.WorldToCell(transform.position);
        Vector3Int buildingCell = BuildingManager.Instance.grid.WorldToCell(buildingTransform.position);
        
        Vector3Int relativePosition = buildingCell - unitCell;
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
    
    public void SetTask_FetchResource(ConstructionSite.ConstructionRequest request, ConstructionSite site)
    {
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
        
        SetTask_Idle();
        Debug.Log($"[Construct:{name}] Fetch failed: no storage or path blocked");
    }
    
    public void SetTask_ConstructPiece(ConstructionSite site, Vector3Int pieceCell)
    {
        if (site == null)
        {
            SetTask_Idle();
            return;
        }
        
        // Get interaction position for this piece
        Vector3 interactionPos = site.AssignInteractionCell(this, pieceCell);
        // Disable alignment to prevent jittering when transitioning from delivery to construction
        bool hasPath = movement.SetNewTargetDirect(interactionPos, movement.waypointTolerance, true);
        
        if (!hasPath)
        {
            SetTask_Idle();
            Debug.Log($"[Construct:{name}] Construct piece FAILED: cannot path to piece at {pieceCell}");
            return;
        }
        
        _currentState = ConstructState.Constructing;
        _currentConstructionSite = site;
        _currentPieceCell = pieceCell;
        _constructionProgress = 0f;
        Debug.Log($"[Construct:{name}] State -> Constructing: piece at {pieceCell}");
    }
    
    public void SetTask_Idle()
    {
        ReleaseFromConstruction();
        _currentState = ConstructState.Idle;
        Debug.Log($"[Construct:{name}] State -> Idle");
    }
    
    private void ReleaseFromConstruction()
    {
        if (_currentRequest != null && _currentRequest.site != null)
        {
            _currentRequest.site.CancelRequest(_currentRequest);
            _currentRequest = null;
        }
        
        if (_currentConstructionSite != null)
        {
            _currentConstructionSite.ReleaseDrone(this);
            
            // Release from piece assignment if we were working on a piece
            if (_currentPieceCell != default)
            {
                _currentConstructionSite.ReleaseDroneFromPiece(_currentPieceCell, this);
            }
        }
        
        if (_loadingCoroutine != null)
        {
            StopCoroutine(_loadingCoroutine);
            _loadingCoroutine = null;
        }
        if (_unloadingCoroutine != null)
        {
            StopCoroutine(_unloadingCoroutine);
            _unloadingCoroutine = null;
        }
        if (_constructionCoroutine != null)
        {
            StopCoroutine(_constructionCoroutine);
            _constructionCoroutine = null;
        }
        
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

