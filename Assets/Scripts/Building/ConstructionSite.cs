using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ConstructionSite : MonoBehaviour
{
    [Header("Construction Data")]
    public BuildingData buildingData;
    public Vector3Int cellPosition;
    
    private readonly Dictionary<Vector3Int, Dictionary<ResourceType, int>> _pieceRequiredResources = new ();
    private readonly Dictionary<Vector3Int, Dictionary<ResourceType, int>> _pieceDeliveredResources = new ();
    
    private readonly Dictionary<ResourceType, int> _requiredResources = new ();
    private readonly Dictionary<ResourceType, int> _deliveredResources = new ();
    private readonly List<ConstructionRequest> _pendingRequests = new ();
    
    private readonly Dictionary<Vector3Int, bool> _constructedPieces = new ();
    private readonly Dictionary<Vector3Int, Unit_Construct> _pieceAssignedDrone = new ();
    private readonly Dictionary<Vector3Int, Unit_Construct> _pieceCommittedDrone = new ();
    private readonly Dictionary<Unit_Construct, Vector3Int> _droneInteractionCells = new ();
    private readonly HashSet<ConstructionRequest> _requestsBeingDelivered = new ();
    
    private static int nextRequestId = 1;
    
    public bool IsComplete { get; private set; }
    
    public Vector3 GetPosition()
    {
        return BuildingManager.Instance?.grid?.GetCellCenterWorld(cellPosition) ?? Vector3.zero;
    }
    
    private void InitializePieceTracking()
    {
        if (buildingData == null || buildingData.recipe == null) return;
        
        _constructedPieces.Clear();
        _pieceRequiredResources.Clear();
        _pieceDeliveredResources.Clear();
        
        BuildingPieceData[] allCards = Resources.LoadAll<BuildingPieceData>("Building Pieces");
        Dictionary<BuildingPieceType, BuildingPieceData> buildingPieceAndCardMap = new Dictionary<BuildingPieceType, BuildingPieceData>();
        
        foreach (var card in allCards)
        {
            if (card.buildingPieceType != BuildingPieceType.None)
            {
                buildingPieceAndCardMap.TryAdd(card.buildingPieceType, card);
            }
        }
        
        foreach (var piece in buildingData.recipe)
        {
            Vector3Int pieceCell = cellPosition + piece.relativePosition;
            _constructedPieces[pieceCell] = false;
            
            // Get resources required for this specific piece
            if (buildingPieceAndCardMap.TryGetValue(piece.buildingPieceType, out BuildingPieceData pieceCard) && pieceCard.costs != null)
            {
                Dictionary<ResourceType, int> pieceRequired = new Dictionary<ResourceType, int>();
                Dictionary<ResourceType, int> pieceDelivered = new Dictionary<ResourceType, int>();
                
                foreach (var cost in pieceCard.costs)
                {
                    pieceRequired[cost.resourceType] = cost.amount;
                    pieceDelivered[cost.resourceType] = 0;
                }
                
                _pieceRequiredResources[pieceCell] = pieceRequired;
                _pieceDeliveredResources[pieceCell] = pieceDelivered;
            }
        }
    }
    
    // Atomically get and assign the next piece to construct (prevents race conditions)
    public Vector3Int? GetAndAssignNextPieceToConstruct(Unit_Construct drone)
    {
        if (buildingData == null || buildingData.recipe == null || drone == null) return null;
        
        foreach (var piece in buildingData.recipe)
        {
            Vector3Int pieceCell = cellPosition + piece.relativePosition;
            
            if (!CanAssignPieceToDrone(pieceCell, drone))
            {
                continue;
            }
            
            if (HasPieceAllResources(pieceCell) && AssignDroneToPiece(pieceCell, drone))
            {
                return pieceCell;
            }
        }
        return null;
    }
    
    private bool CanAssignPieceToDrone(Vector3Int pieceCell, Unit_Construct drone)
    {
        if (IsPieceConstructed(pieceCell))
        {
            return false;
        }
        
        if (_pieceAssignedDrone.ContainsKey(pieceCell))
        {
            Unit_Construct assignedDrone = _pieceAssignedDrone[pieceCell];
            if (assignedDrone == null)
            {
                _pieceAssignedDrone.Remove(pieceCell);
            }
            else if (drone == null || assignedDrone != drone)
            {
                return false;
            }
        }
        
        if (IsPieceCommitted(pieceCell) && (drone == null || !IsPieceCommittedTo(pieceCell, drone)))
        {
            return false;
        }
        
        return true;
    }
    
    // Assign a drone to work on a specific piece
    public bool AssignDroneToPiece(Vector3Int pieceCell, Unit_Construct drone)
    {
        // Check if piece is already assigned to another drone
        if (_pieceAssignedDrone.ContainsKey(pieceCell))
        {
            Unit_Construct assignedDrone = _pieceAssignedDrone[pieceCell];
            if (assignedDrone != null && assignedDrone != drone)
            {
                // Another drone is already working on this piece
                return false;
            }
        }
        
        _pieceAssignedDrone[pieceCell] = drone;
        return true;
    }
    
    // Release a drone from a piece (when construction is complete or cancelled)
    public void ReleaseDroneFromPiece(Vector3Int pieceCell, Unit_Construct drone)
    {
        if (_pieceAssignedDrone.ContainsKey(pieceCell) && _pieceAssignedDrone[pieceCell] == drone)
        {
            _pieceAssignedDrone.Remove(pieceCell);
        }
    }
    
    // Commit a drone to constructing a piece (after delivering resources)
    public void CommitDroneToPiece(Vector3Int pieceCell, Unit_Construct drone)
    {
        _pieceCommittedDrone[pieceCell] = drone;
    }
    
    // Check if a piece is committed to a specific drone
    public bool IsPieceCommittedTo(Vector3Int pieceCell, Unit_Construct drone)
    {
        return _pieceCommittedDrone.ContainsKey(pieceCell) && _pieceCommittedDrone[pieceCell] == drone;
    }
    
    // Check if a piece is committed to any drone
    public bool IsPieceCommitted(Vector3Int pieceCell)
    {
        return _pieceCommittedDrone.ContainsKey(pieceCell) && _pieceCommittedDrone[pieceCell] != null;
    }
    
    // Release a drone's commitment to a piece (after construction is complete)
    public void ReleaseCommitmentFromPiece(Vector3Int pieceCell, Unit_Construct drone)
    {
        if (_pieceCommittedDrone.ContainsKey(pieceCell) && _pieceCommittedDrone[pieceCell] == drone)
        {
            _pieceCommittedDrone.Remove(pieceCell);
        }
    }
    
    // Check if a specific piece has all its required resources
    public bool HasPieceAllResources(Vector3Int pieceCell)
    {
        if (!_pieceRequiredResources.ContainsKey(pieceCell))
        {
            return false;
        }
        
        var required = _pieceRequiredResources[pieceCell];
        var delivered = _pieceDeliveredResources.ContainsKey(pieceCell) 
            ? _pieceDeliveredResources[pieceCell] 
            : new Dictionary<ResourceType, int>();
        
        foreach (var req in required)
        {
            int deliveredAmount = delivered.ContainsKey(req.Key) ? delivered[req.Key] : 0;
            if (deliveredAmount < req.Value)
            {
                return false;
            }
        }
        
        return true;
    }
    
    // Check if a piece is already constructed
    public bool IsPieceConstructed(Vector3Int pieceCell)
    {
        return _constructedPieces.ContainsKey(pieceCell) && _constructedPieces[pieceCell];
    }
    
    // Check if a piece is assigned to a specific drone
    public bool IsPieceAssignedToMe(Vector3Int pieceCell, Unit_Construct drone)
    {
        if (!_pieceAssignedDrone.ContainsKey(pieceCell))
        {
            return false;
        }
        return _pieceAssignedDrone[pieceCell] == drone;
    }
    
    // Mark a piece as constructed
    public void MarkPieceConstructed(Vector3Int pieceCell, Unit_Construct drone)
    {
        if (_constructedPieces.ContainsKey(pieceCell))
        {
            _constructedPieces[pieceCell] = true;
            _pendingRequests.RemoveAll(r => r.targetPieceCell == pieceCell);
            
            if (drone != null)
            {
                ReleaseDroneFromPiece(pieceCell, drone);
            }
            
            if (AreAllPiecesConstructed())
            {
                IsComplete = true;
                Debug.Log($"[ConstructionSite:{name}] All pieces constructed!");
            }
        }
    }
    
    // Assign interaction cell for a drone at a specific piece position
    public Vector3 AssignInteractionCell(Unit_Construct drone, Vector3Int pieceCell)
    {
        return AssignInteractionCellInternal(drone, pieceCell);
    }
    
    // Assign delivery interaction cell (for resource delivery)
    public Vector3 AssignDeliveryInteractionCell(Unit_Construct drone, Vector3Int pieceCell)
    {
        return AssignInteractionCellInternal(drone, pieceCell);
    }
    
    private Vector3 AssignInteractionCellInternal(Unit_Construct drone, Vector3Int pieceCell)
    {
        if (drone == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null)
        {
            return BuildingManager.Instance.grid.GetCellCenterWorld(pieceCell);
        }
        
        List<Vector3Int> availableCells = GetAvailableInteractionCells(pieceCell);
        
        if (availableCells.Count == 0)
        {
            return BuildingManager.Instance.grid.GetCellCenterWorld(pieceCell);
        }
        
        if (_droneInteractionCells.TryGetValue(drone, out Vector3Int existingCell) && availableCells.Contains(existingCell))
        {
            return BuildingManager.Instance.grid.GetCellCenterWorld(existingCell);
        }
        
        Vector3Int bestCell = FindBestInteractionCell(drone, availableCells);
        _droneInteractionCells[drone] = bestCell;
        return BuildingManager.Instance.grid.GetCellCenterWorld(bestCell);
    }
    
    private Vector3Int FindBestInteractionCell(Unit_Construct drone, List<Vector3Int> availableCells)
    {
        Vector3Int bestCell = availableCells[0];
        float minDistance = float.MaxValue;
        Vector3 dronePos = drone.transform.position;
        HashSet<Vector3Int> assignedCells = new HashSet<Vector3Int>(_droneInteractionCells.Values);
        
        foreach (Vector3Int cell in availableCells)
        {
            bool isUnassigned = !assignedCells.Contains(cell);
            bool bestIsAssigned = assignedCells.Contains(bestCell);
            float distance = Vector3.Distance(dronePos, BuildingManager.Instance.grid.GetCellCenterWorld(cell));
            
            if (isUnassigned && bestIsAssigned)
            {
                bestCell = cell;
                minDistance = distance;
            }
            else if (isUnassigned == !bestIsAssigned && distance < minDistance)
            {
                bestCell = cell;
                minDistance = distance;
            }
        }
        
        return bestCell;
    }
    
    private List<Vector3Int> GetAvailableInteractionCells(Vector3Int pieceCell)
    {
        HashSet<Vector3Int> interactionCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> occupiedCells = GetOccupiedCells();
        
        Vector3Int[] cardinalOffsets = {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
        };
        
        foreach (Vector3Int offset in cardinalOffsets)
        {
            Vector3Int neighbor = pieceCell + offset;
            if (!occupiedCells.Contains(neighbor) && IsCellWalkable(neighbor))
            {
                interactionCells.Add(neighbor);
            }
        }
        
        if (interactionCells.Count == 0)
        {
            foreach (Vector3Int offset in cardinalOffsets)
            {
                Vector3Int neighbor = pieceCell + offset;
                if (occupiedCells.Contains(neighbor) && BuildingManager.Instance != null && BuildingManager.Instance.IsTemporaryTile(neighbor))
                {
                    interactionCells.Add(neighbor);
                }
            }
        }
        
        return interactionCells.ToList();
    }
    
    private HashSet<Vector3Int> GetOccupiedCells()
    {
        HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
        
        if (buildingData != null && buildingData.recipe != null)
        {
            foreach (var piece in buildingData.recipe)
            {
                occupiedCells.Add(cellPosition + piece.relativePosition);
            }
        }
        
        return occupiedCells;
    }
    
    private bool IsCellWalkable(Vector3Int cell)
    {
        if (BuildingManager.Instance == null) return false;
        return BuildingManager.Instance.CanPlaceBuilding(cell) || BuildingManager.Instance.IsTemporaryTile(cell);
    }
    
    public void ReleaseDrone(Unit_Construct drone)
    {
        _droneInteractionCells.Remove(drone);
    }
    
    private void Start()
    {
        if (buildingData != null && buildingData.recipe != null)
        {
            CalculateComboCosts();
        }
        else
        {
            Debug.LogWarning($"[ConstructionSite] comboCardData is null or has no recipe at Start()");
        }
    }
    
    public void CalculateComboCosts()
    {
        _requiredResources.Clear();
        _deliveredResources.Clear();
        
        if (buildingData == null)
        {
            Debug.LogError($"[ConstructionSite:{name}] Cannot calculate costs: comboCardData is null");
            return;
        }
        
        if (buildingData.recipe == null || buildingData.recipe.Count == 0)
        {
            Debug.LogError($"[ConstructionSite:{name}] Cannot calculate costs: recipe is null or empty");
            return;
        }
        
        // Load all CardData to find costs for each gadget type
        BuildingPieceData[] allCards = Resources.LoadAll<BuildingPieceData>("Building Pieces");
        Dictionary<BuildingPieceType, BuildingPieceData> gadgetToCardMap = new Dictionary<BuildingPieceType, BuildingPieceData>();
        
        foreach (var card in allCards)
        {
            if (card.buildingPieceType != BuildingPieceType.None && !gadgetToCardMap.ContainsKey(card.buildingPieceType))
            {
                gadgetToCardMap[card.buildingPieceType] = card;
            }
        }
        
        foreach (var piece in buildingData.recipe)
        {
            if (!gadgetToCardMap.TryGetValue(piece.buildingPieceType, out BuildingPieceData pieceCard) || pieceCard.costs == null)
            {
                Debug.LogWarning($"[ConstructionSite:{name}] Could not find CardData for gadget type {piece.buildingPieceType}");
                continue;
            }

            foreach (var cost in pieceCard.costs)
            {
                if (_requiredResources.ContainsKey(cost.resourceType))
                {
                    _requiredResources[cost.resourceType] += cost.amount;
                }
                else
                {
                    _requiredResources[cost.resourceType] = cost.amount;
                }
                _deliveredResources[cost.resourceType] = 0;
            }
        }
        
        if (_requiredResources.Count > 0)
        {
            string costString = string.Join(", ", _requiredResources.Select(kvp => $"{kvp.Value} {kvp.Key}"));
            Debug.Log($"[ConstructionSite:{name}] Calculated costs: {costString} (Total resources needed: {_requiredResources.Count} types)");
        }
        else
        {
            Debug.LogError($"[ConstructionSite:{name}] Calculated costs but _requiredResources is empty!");
        }
        
        InitializePieceTracking();
    }
    
    private void OnDestroy()
    {
        _requestsBeingDelivered.Clear();
        
        if (ConstructionManager.Instance != null)
        {
            ConstructionManager.Instance.UnregisterConstructionSite(this);
        }
    }
    
    public bool HasAllResources()
    {
        foreach (var required in _requiredResources)
        {
            if (!_deliveredResources.ContainsKey(required.Key) || 
                _deliveredResources[required.Key] < required.Value)
            {
                return false;
            }
        }
        return true;
    }
    
    // Check if all pieces are constructed
    public bool AreAllPiecesConstructed()
    {
        if (_constructedPieces.Count == 0) return false;
        
        foreach (var constructed in _constructedPieces.Values)
        {
            if (!constructed) return false;
        }
        return true;
    }
    
    public ConstructionRequest GetNextResourceRequest(int droneCapacity)
    {
        if (_requiredResources.Count == 0)
        {
            Debug.LogWarning($"[ConstructionSite:{name}] GetNextResourceRequest called but _requiredResources is empty!");
            return null;
        }
        
        foreach (var pieceCell in _pieceRequiredResources.Keys)
        {
            if (_constructedPieces.ContainsKey(pieceCell) && _constructedPieces[pieceCell])
            {
                continue;
            }
            
            var pieceRequired = _pieceRequiredResources[pieceCell];
            var pieceDelivered = _pieceDeliveredResources.ContainsKey(pieceCell) 
                ? _pieceDeliveredResources[pieceCell] 
                : new Dictionary<ResourceType, int>();
            
            foreach (var required in pieceRequired)
            {
                int deliveredAmount = pieceDelivered.ContainsKey(required.Key) ? pieceDelivered[required.Key] : 0;
                int stillNeeded = required.Value - deliveredAmount;
                int onTheWay = GetOnTheWayAmount(required.Key, pieceCell);
                int totalNeeded = stillNeeded - onTheWay;
                
                if (totalNeeded > 0)
                {
                    int amountToRequest = Mathf.Min(totalNeeded, droneCapacity);
                    ConstructionRequest newRequest = new ConstructionRequest
                    {
                        type = required.Key,
                        amount = amountToRequest,
                        site = this,
                        targetPieceCell = pieceCell
                    };
                    _pendingRequests.Add(newRequest);
                    Debug.Log($"[ConstructionSite:{name}] Created resource request: {amountToRequest} {required.Key} for piece at {pieceCell} (still needed: {stillNeeded}, on the way: {onTheWay})");
                    return newRequest;
                }
            }
        }
        
        Debug.Log($"[ConstructionSite:{name}] GetNextResourceRequest: No resources needed. Required: {_requiredResources.Count} types");
        return null;
    }
    
    // Atomically get and assign a resource request to a drone (prevents duplicate requests)
    public ConstructionRequest GetAndAssignNextResourceRequest(Unit_Construct drone, int droneCapacity)
    {
        if (_requiredResources.Count == 0 || drone == null)
        {
            Debug.LogWarning($"[ConstructionSite:{name}] GetAndAssignNextResourceRequest called but _requiredResources is empty or drone is null!");
            return null;
        }
        
        CleanupStaleRequests();
        
        foreach (var pieceCell in _pieceRequiredResources.Keys)
        {
            if (_constructedPieces.ContainsKey(pieceCell) && _constructedPieces[pieceCell])
            {
                continue;
            }
            
            var pieceRequired = _pieceRequiredResources[pieceCell];
            var pieceDelivered = _pieceDeliveredResources.ContainsKey(pieceCell) 
                ? _pieceDeliveredResources[pieceCell] 
                : new Dictionary<ResourceType, int>();
            
            foreach (var required in pieceRequired)
            {
                int deliveredAmount = pieceDelivered.ContainsKey(required.Key) ? pieceDelivered[required.Key] : 0;
                int stillNeeded = required.Value - deliveredAmount;
                int onTheWay = GetOnTheWayAmount(required.Key, pieceCell);
                int totalNeeded = stillNeeded - onTheWay;
                
                if (totalNeeded > 0)
                {
                    int amountToRequest = Mathf.Min(totalNeeded, droneCapacity);
                    ConstructionRequest newRequest = new ConstructionRequest
                    {
                        type = required.Key,
                        amount = amountToRequest,
                        site = this,
                        targetPieceCell = pieceCell,
                        assignedDrone = drone
                    };
                    _pendingRequests.Add(newRequest);
                    Debug.Log($"[ConstructionSite:{name}] Created and assigned resource request: {amountToRequest} {required.Key} for piece at {pieceCell} to drone {drone.name} (still needed: {stillNeeded}, on the way: {onTheWay})");
                    return newRequest;
                }
            }
        }
        
        Debug.Log($"[ConstructionSite:{name}] GetAndAssignNextResourceRequest: No resources needed. Required: {_requiredResources.Count} types");
        return null;
    }
    
    public bool CanDepositResource(ResourceType type, Unit_Construct drone)
    {
        if (drone == null) return false;
        
        // Find the request for this specific drone and resource type
        ConstructionRequest request = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone && r.type == type);
        
        if (request == null)
        {
            // No pending request found - check if it's already being delivered (duplicate)
            bool isDuplicate = _requestsBeingDelivered.Any(r => r.assignedDrone == drone && r.type == type);
            if (isDuplicate)
            {
                return false; // Already being delivered
            }
            // Request doesn't exist - can't deposit
            return false;
        }
        
        // Check if this exact request is already being delivered
        if (_requestsBeingDelivered.Contains(request))
        {
            return false;
        }
        
        // Check if we still need this resource type
        int totalNeeded = _requiredResources.ContainsKey(type) ? _requiredResources[type] : 0;
        int totalDelivered = _deliveredResources.ContainsKey(type) ? _deliveredResources[type] : 0;
        
        if (totalNeeded <= totalDelivered)
        {
            return false; // Already have enough resources
        }
        
        // Request exists, not being delivered, and we still need resources
        return true;
    }
    
    public bool TryDepositResource(ResourceType type, int amount, Unit_Construct drone)
    {
        if (drone == null)
        {
            Debug.LogWarning($"[ConstructionSite:{name}] TryDepositResource called with null drone");
            return false;
        }

        // Find the request for this specific drone and resource type
        ConstructionRequest request = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone && r.type == type);
        
        if (request == null)
        {
            // Check if this request is already being delivered (duplicate attempt)
            bool isDuplicate = _requestsBeingDelivered.Any(r => r.assignedDrone == drone && r.type == type);
            if (isDuplicate)
            {
                Debug.Log($"[ConstructionSite:{name}] Deposit REJECTED: duplicate delivery attempt from drone {drone.name} for {type} (request already being processed)");
            }
            else
            {
                Debug.Log($"[ConstructionSite:{name}] Deposit FAILED: no matching pending request for {type} from drone {drone.name}");
            }
            return false;
        }
        
        // Check if this exact request is already being delivered
        if (_requestsBeingDelivered.Contains(request))
        {
            Debug.Log($"[ConstructionSite:{name}] Deposit REJECTED: request {request.requestId} from drone {drone.name} is already being processed");
            return false;
        }
        
        // Mark request as being delivered BEFORE removing from pending (atomic operation)
        _requestsBeingDelivered.Add(request);
        _pendingRequests.Remove(request);
        
        try
        {
            int remainingAmount = amount;
            int totalNeeded = _requiredResources.ContainsKey(type) ? _requiredResources[type] : 0;
            int totalDelivered = _deliveredResources.ContainsKey(type) ? _deliveredResources[type] : 0;
            int totalCanAccept = Mathf.Min(remainingAmount, totalNeeded - totalDelivered);
            
            if (totalCanAccept <= 0)
            {
                Debug.Log($"[ConstructionSite:{name}] Deposit rejected: piece already has sufficient resources of type {type}");
                return false;
            }

            if (!_deliveredResources.ContainsKey(type))
            {
                _deliveredResources[type] = 0;
            }
            _deliveredResources[type] += totalCanAccept;
            
            HashSet<Vector3Int> piecesThatReceivedResources = new HashSet<Vector3Int>();
            
            foreach (var pieceCell in _pieceRequiredResources.Keys)
            {
                if (remainingAmount <= 0) break;
                if (_constructedPieces.ContainsKey(pieceCell) && _constructedPieces[pieceCell]) continue;
                
                var pieceRequired = _pieceRequiredResources[pieceCell];
                if (!pieceRequired.ContainsKey(type)) continue;

                var pieceDelivered = _pieceDeliveredResources[pieceCell];
                int pieceNeeded = pieceRequired[type];
                int pieceDeliveredAmount = pieceDelivered.ContainsKey(type) ? pieceDelivered[type] : 0;
                int pieceCanAccept = Mathf.Min(remainingAmount, pieceNeeded - pieceDeliveredAmount);
                
                if (pieceCanAccept > 0)
                {
                    if (!pieceDelivered.ContainsKey(type))
                    {
                        pieceDelivered[type] = 0;
                    }
                    pieceDelivered[type] += pieceCanAccept;
                    remainingAmount -= pieceCanAccept;
                    piecesThatReceivedResources.Add(pieceCell);
                }
            }
            
            foreach (var pieceCell in piecesThatReceivedResources)
            {
                if (HasPieceAllResources(pieceCell) && !IsPieceConstructed(pieceCell))
                {
                    StartPieceConstruction(pieceCell);
                }
            }
            
            return true;
        }
        finally
        {
            // Always remove from in-progress tracking, even if there was an error
            _requestsBeingDelivered.Remove(request);
        }
    }
    
    private void StartPieceConstruction(Vector3Int pieceCell)
    {
        if (IsPieceConstructed(pieceCell))
        {
            return;
        }
        
        if (!HasPieceAllResources(pieceCell))
        {
            Debug.LogWarning($"[ConstructionSite:{name}] Cannot construct piece at {pieceCell} - resources not fully delivered.");
            return;
        }
        
        if (buildingData == null || BuildingManager.Instance == null)
        {
            Debug.LogError($"[ConstructionSite:{name}] Cannot construct piece: missing comboCardData or BuildingManager");
            return;
        }
        
        BuildingManager.Instance.PlaceBuildingPieceAtCell(buildingData, pieceCell, cellPosition);
        
        BuildingPiece placedPiece = BuildingManager.Instance.GetPieceAt(pieceCell);
        if (placedPiece != null)
        {
            MarkPieceConstructed(pieceCell, null);
            Debug.Log($"[ConstructionSite:{name}] Immediately constructed piece at {pieceCell} for '{buildingData.displayName}'");
            
            if (ConstructionManager.Instance != null)
            {
                ConstructionManager.Instance.OnPieceConstructed(this);
            }
        }
        else
        {
            Debug.LogError($"[ConstructionSite:{name}] Failed to construct piece at {pieceCell} - piece was not placed!");
        }
    }
    
    public void CancelRequest(ConstructionRequest request)
    {
        if (request == null) return;
        
        _pendingRequests.Remove(request);
        _requestsBeingDelivered.Remove(request);
    }
    
    // Clean up stale requests assigned to units that are idle
    public void CleanupStaleRequests()
    {
        _pendingRequests.RemoveAll(r => {
            if (r.assignedDrone == null) return true;
            return r.assignedDrone.IsIdle();
        });
        
        // Also clean up in-progress requests if the drone is idle (shouldn't happen, but safety check)
        _requestsBeingDelivered.RemoveWhere(r => {
            if (r.assignedDrone == null) return true;
            return r.assignedDrone.IsIdle();
        });
    }
    
    private int GetOnTheWayAmount(ResourceType resourceType, Vector3Int pieceCell)
    {
        int onTheWay = 0;
        foreach (var request in _pendingRequests)
        {
            if (request.type == resourceType && request.targetPieceCell == pieceCell)
            {
                if (request.assignedDrone == null || !request.assignedDrone.IsIdle())
                {
                    onTheWay += request.amount;
                }
            }
        }
        return onTheWay;
    }
    
    public class ConstructionRequest
    {
        public int requestId; // Unique identifier for this request
        public ResourceType type;
        public int amount;
        public ConstructionSite site;
        public Unit_Construct assignedDrone;
        public Vector3Int? targetPieceCell; // The piece cell that needs this resource
        
        public ConstructionRequest()
        {
            requestId = nextRequestId++;
        }
    }
}

