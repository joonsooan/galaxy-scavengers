using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ConstructionSite : MonoBehaviour
{
    [Header("Construction Data")]
    public ComboCardData comboCardData;
    public Vector3Int cellPosition;
    
    // Per-piece resource tracking
    private readonly Dictionary<Vector3Int, Dictionary<ResourceType, int>> _pieceRequiredResources = new Dictionary<Vector3Int, Dictionary<ResourceType, int>>();
    private readonly Dictionary<Vector3Int, Dictionary<ResourceType, int>> _pieceDeliveredResources = new Dictionary<Vector3Int, Dictionary<ResourceType, int>>();
    
    // Legacy total resources (for backward compatibility with delivery system)
    private readonly Dictionary<ResourceType, int> _requiredResources = new Dictionary<ResourceType, int>();
    private readonly Dictionary<ResourceType, int> _deliveredResources = new Dictionary<ResourceType, int>();
    private readonly List<ConstructionRequest> _pendingRequests = new List<ConstructionRequest>();
    
    // Per-piece construction tracking
    private readonly Dictionary<Vector3Int, bool> _constructedPieces = new Dictionary<Vector3Int, bool>();
    private readonly Dictionary<Vector3Int, Unit_Construct> _pieceAssignedDrone = new Dictionary<Vector3Int, Unit_Construct>(); // Track which drone is working on which piece
    private readonly Dictionary<Unit_Construct, Vector3Int> _droneInteractionCells = new Dictionary<Unit_Construct, Vector3Int>();
    
    public bool IsComplete { get; private set; }
    
    public Vector3 GetPosition()
    {
        return BuildingManager.Instance.grid.GetCellCenterWorld(cellPosition);
    }
    
    // Initialize piece tracking when costs are calculated
    public void InitializePieceTracking()
    {
        if (comboCardData == null || comboCardData.recipe == null) return;
        
        _constructedPieces.Clear();
        _pieceRequiredResources.Clear();
        _pieceDeliveredResources.Clear();
        
        // Load all CardData to find costs for each gadget type
        CardData[] allCards = Resources.LoadAll<CardData>("Cards");
        Dictionary<GadgetType, CardData> gadgetToCardMap = new Dictionary<GadgetType, CardData>();
        
        foreach (var card in allCards)
        {
            if (card.gadgetType != GadgetType.None && !gadgetToCardMap.ContainsKey(card.gadgetType))
            {
                gadgetToCardMap[card.gadgetType] = card;
            }
        }
        
        // Initialize per-piece resource tracking
        foreach (var piece in comboCardData.recipe)
        {
            Vector3Int pieceCell = cellPosition + piece.relativePosition;
            _constructedPieces[pieceCell] = false;
            
            // Get resources required for this specific piece
            if (gadgetToCardMap.TryGetValue(piece.gadgetType, out CardData pieceCard) && pieceCard.costs != null)
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
    
    // Get the next piece that needs to be constructed (has all its resources and is not being worked on)
    public Vector3Int? GetNextPieceToConstruct()
    {
        if (comboCardData == null || comboCardData.recipe == null) return null;
        
        foreach (var piece in comboCardData.recipe)
        {
            Vector3Int pieceCell = cellPosition + piece.relativePosition;
            
            // Check if piece is already constructed
            if (_constructedPieces.ContainsKey(pieceCell) && _constructedPieces[pieceCell])
            {
                continue;
            }
            
            // Check if another drone is already working on this piece
            if (_pieceAssignedDrone.ContainsKey(pieceCell))
            {
                Unit_Construct assignedDrone = _pieceAssignedDrone[pieceCell];
                // If the assigned drone is null or destroyed, clear the assignment
                if (assignedDrone == null)
                {
                    _pieceAssignedDrone.Remove(pieceCell);
                }
                else
                {
                    // Another drone is working on this piece, skip it
                    continue;
                }
            }
            
            // Check if this piece has all its required resources
            if (HasPieceAllResources(pieceCell))
            {
                return pieceCell;
            }
        }
        return null;
    }
    
    // Atomically get and assign the next piece to construct (prevents race conditions)
    public Vector3Int? GetAndAssignNextPieceToConstruct(Unit_Construct drone)
    {
        if (comboCardData == null || comboCardData.recipe == null || drone == null) return null;
        
        foreach (var piece in comboCardData.recipe)
        {
            Vector3Int pieceCell = cellPosition + piece.relativePosition;
            
            // Check if piece is already constructed
            if (_constructedPieces.ContainsKey(pieceCell) && _constructedPieces[pieceCell])
            {
                continue;
            }
            
            // Check if another drone is already working on this piece
            if (_pieceAssignedDrone.ContainsKey(pieceCell))
            {
                Unit_Construct assignedDrone = _pieceAssignedDrone[pieceCell];
                // If the assigned drone is null or destroyed, clear the assignment
                if (assignedDrone == null)
                {
                    _pieceAssignedDrone.Remove(pieceCell);
                }
                else
                {
                    // Another drone is working on this piece, skip it
                    continue;
                }
            }
            
            // Check if this piece has all its required resources
            if (HasPieceAllResources(pieceCell))
            {
                // Atomically assign the drone to this piece
                if (AssignDroneToPiece(pieceCell, drone))
                {
                    return pieceCell;
                }
                // If assignment failed (race condition), continue to next piece
            }
        }
        return null;
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
    
    // Mark a piece as constructed
    public void MarkPieceConstructed(Vector3Int pieceCell, Unit_Construct drone)
    {
        if (_constructedPieces.ContainsKey(pieceCell))
        {
            _constructedPieces[pieceCell] = true;
            
            // Release the drone assignment
            ReleaseDroneFromPiece(pieceCell, drone);
            
            // Check if all pieces are constructed
            bool allConstructed = true;
            foreach (var constructed in _constructedPieces.Values)
            {
                if (!constructed)
                {
                    allConstructed = false;
                    break;
                }
            }
            
            if (allConstructed)
            {
                IsComplete = true;
                Debug.Log($"[ConstructionSite:{name}] All pieces constructed!");
            }
        }
    }
    
    // Assign interaction cell for a drone at a specific piece position
    public Vector3 AssignInteractionCell(Unit_Construct drone, Vector3Int pieceCell)
    {
        if (drone == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null)
        {
            return BuildingManager.Instance.grid.GetCellCenterWorld(pieceCell);
        }
        
        // Get available interaction cells around this piece
        List<Vector3Int> availableCells = GetAvailableInteractionCells(pieceCell);
        
        if (availableCells.Count == 0)
        {
            // No available cells, use the piece cell itself
            return BuildingManager.Instance.grid.GetCellCenterWorld(pieceCell);
        }
        
        // Check if drone already has an assigned cell for this piece
        if (_droneInteractionCells.TryGetValue(drone, out Vector3Int existingCell))
        {
            if (availableCells.Contains(existingCell))
            {
                return BuildingManager.Instance.grid.GetCellCenterWorld(existingCell);
            }
        }
        
        // Find best available cell (unassigned, then closest to drone)
        Vector3Int bestCell = availableCells[0];
        float minDistance = float.MaxValue;
        Vector3 dronePos = drone.transform.position;
        
        HashSet<Vector3Int> assignedCells = new HashSet<Vector3Int>(_droneInteractionCells.Values);
        
        foreach (Vector3Int cell in availableCells)
        {
            bool isUnassigned = !assignedCells.Contains(cell);
            float distance = Vector3.Distance(dronePos, BuildingManager.Instance.grid.GetCellCenterWorld(cell));
            
            if (isUnassigned && assignedCells.Contains(bestCell))
            {
                bestCell = cell;
                minDistance = distance;
            }
            else if (isUnassigned == !assignedCells.Contains(bestCell))
            {
                if (distance < minDistance)
                {
                    bestCell = cell;
                    minDistance = distance;
                }
            }
        }
        
        // Assign the cell to this drone
        _droneInteractionCells[drone] = bestCell;
        
        return BuildingManager.Instance.grid.GetCellCenterWorld(bestCell);
    }
    
    private List<Vector3Int> GetAvailableInteractionCells(Vector3Int pieceCell)
    {
        HashSet<Vector3Int> interactionCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
        
        // Get all occupied cells in the recipe
        if (comboCardData != null && comboCardData.recipe != null)
        {
            foreach (var piece in comboCardData.recipe)
            {
                Vector3Int cell = cellPosition + piece.relativePosition;
                occupiedCells.Add(cell);
            }
        }
        
        Vector3Int[] cardinalOffsets = {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
        };
        
        // Find walkable cells adjacent to this piece
        foreach (Vector3Int offset in cardinalOffsets)
        {
            Vector3Int neighbor = pieceCell + offset;
            
            if (!occupiedCells.Contains(neighbor) && IsCellWalkable(neighbor))
            {
                interactionCells.Add(neighbor);
            }
        }
        
        return interactionCells.ToList();
    }
    
    private bool IsCellWalkable(Vector3Int cell)
    {
        if (BuildingManager.Instance == null) return false;
        
        return BuildingManager.Instance.CanPlaceBuilding(cell) || 
               BuildingManager.Instance.IsTemporaryTile(cell);
    }
    
    public void ReleaseDrone(Unit_Construct drone)
    {
        if (_droneInteractionCells.ContainsKey(drone))
        {
            _droneInteractionCells.Remove(drone);
        }
    }
    
    // Assign delivery interaction cell (for resource delivery)
    // Uses the specific piece cell that needs the resource
    public Vector3 AssignDeliveryInteractionCell(Unit_Construct drone, Vector3Int pieceCell)
    {
        if (drone == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null)
        {
            return BuildingManager.Instance.grid.GetCellCenterWorld(pieceCell);
        }
        
        // Get available interaction cells around the piece cell that needs resources
        List<Vector3Int> availableCells = GetAvailableInteractionCells(pieceCell);
        
        if (availableCells.Count == 0)
        {
            // No available cells, use the piece cell itself
            return BuildingManager.Instance.grid.GetCellCenterWorld(pieceCell);
        }
        
        // Check if drone already has an assigned cell
        if (_droneInteractionCells.TryGetValue(drone, out Vector3Int existingCell))
        {
            if (availableCells.Contains(existingCell))
            {
                return BuildingManager.Instance.grid.GetCellCenterWorld(existingCell);
            }
        }
        
        // Find best available cell (unassigned, then closest to drone)
        Vector3Int bestCell = availableCells[0];
        float minDistance = float.MaxValue;
        Vector3 dronePos = drone.transform.position;
        
        HashSet<Vector3Int> assignedCells = new HashSet<Vector3Int>(_droneInteractionCells.Values);
        
        foreach (Vector3Int cell in availableCells)
        {
            bool isUnassigned = !assignedCells.Contains(cell);
            float distance = Vector3.Distance(dronePos, BuildingManager.Instance.grid.GetCellCenterWorld(cell));
            
            if (isUnassigned && assignedCells.Contains(bestCell))
            {
                bestCell = cell;
                minDistance = distance;
            }
            else if (isUnassigned == !assignedCells.Contains(bestCell))
            {
                if (distance < minDistance)
                {
                    bestCell = cell;
                    minDistance = distance;
                }
            }
        }
        
        // Assign the cell to this drone
        _droneInteractionCells[drone] = bestCell;
        
        return BuildingManager.Instance.grid.GetCellCenterWorld(bestCell);
    }
    
    private void Awake()
    {
        // Calculate costs will be done in Start() or when comboCardData is set
    }
    
    private void Start()
    {
        // Calculate costs from combo recipe pieces
        // Using Start() ensures comboCardData is set before we calculate costs
        if (comboCardData != null && comboCardData.recipe != null)
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
        
        if (comboCardData == null)
        {
            Debug.LogError($"[ConstructionSite:{name}] Cannot calculate costs: comboCardData is null");
            return;
        }
        
        if (comboCardData.recipe == null || comboCardData.recipe.Count == 0)
        {
            Debug.LogError($"[ConstructionSite:{name}] Cannot calculate costs: recipe is null or empty");
            return;
        }
        
        // Load all CardData to find costs for each gadget type
        CardData[] allCards = Resources.LoadAll<CardData>("Cards");
        Dictionary<GadgetType, CardData> gadgetToCardMap = new Dictionary<GadgetType, CardData>();
        
        foreach (var card in allCards)
        {
            if (card.gadgetType != GadgetType.None && !gadgetToCardMap.ContainsKey(card.gadgetType))
            {
                gadgetToCardMap[card.gadgetType] = card;
            }
        }
        
        // Sum up costs from all pieces in the recipe
        foreach (var piece in comboCardData.recipe)
        {
            if (gadgetToCardMap.TryGetValue(piece.gadgetType, out CardData pieceCard) && pieceCard.costs != null)
            {
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
            else
            {
                Debug.LogWarning($"[ConstructionSite:{name}] Could not find CardData for gadget type {piece.gadgetType}");
            }
        }
        
        // Debug: Log calculated costs
        if (_requiredResources.Count > 0)
        {
            string costString = string.Join(", ", _requiredResources.Select(kvp => $"{kvp.Value} {kvp.Key}"));
            Debug.Log($"[ConstructionSite:{name}] Calculated costs: {costString} (Total resources needed: {_requiredResources.Count} types)");
        }
        else
        {
            Debug.LogError($"[ConstructionSite:{name}] Calculated costs but _requiredResources is empty!");
        }
        
        // Initialize piece tracking
        InitializePieceTracking();
    }
    
    private void OnDestroy()
    {
        // Unregister from ConstructionManager
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
        
        // Find a piece that needs resources, and return a request for one of its needed resources
        // Prioritize pieces that are missing resources
        foreach (var pieceCell in _pieceRequiredResources.Keys)
        {
            // Skip pieces that are already constructed
            if (_constructedPieces.ContainsKey(pieceCell) && _constructedPieces[pieceCell])
            {
                continue;
            }
            
            var pieceRequired = _pieceRequiredResources[pieceCell];
            var pieceDelivered = _pieceDeliveredResources.ContainsKey(pieceCell) 
                ? _pieceDeliveredResources[pieceCell] 
                : new Dictionary<ResourceType, int>();
            
            // Find a resource this piece needs
            foreach (var required in pieceRequired)
            {
                int delivered = pieceDelivered.ContainsKey(required.Key) ? pieceDelivered[required.Key] : 0;
                int stillNeeded = required.Value - delivered;
                
                // Check if there's already a pending request for this resource for this piece
                int onTheWay = 0;
                foreach (var request in _pendingRequests)
                {
                    if (request.type == required.Key && 
                        request.targetPieceCell == pieceCell)
                    {
                        // Count all pending requests for this resource/piece combination (assigned or not)
                        onTheWay += request.amount;
                    }
                }
                
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
        
        // Find a piece that needs resources, and return a request for one of its needed resources
        // Prioritize pieces that are missing resources
        foreach (var pieceCell in _pieceRequiredResources.Keys)
        {
            // Skip pieces that are already constructed
            if (_constructedPieces.ContainsKey(pieceCell) && _constructedPieces[pieceCell])
            {
                continue;
            }
            
            var pieceRequired = _pieceRequiredResources[pieceCell];
            var pieceDelivered = _pieceDeliveredResources.ContainsKey(pieceCell) 
                ? _pieceDeliveredResources[pieceCell] 
                : new Dictionary<ResourceType, int>();
            
            // Find a resource this piece needs
            foreach (var required in pieceRequired)
            {
                int delivered = pieceDelivered.ContainsKey(required.Key) ? pieceDelivered[required.Key] : 0;
                int stillNeeded = required.Value - delivered;
                
                // Check if there's already a pending request for this resource for this piece
                int onTheWay = 0;
                foreach (var request in _pendingRequests)
                {
                    if (request.type == required.Key && 
                        request.targetPieceCell == pieceCell)
                    {
                        // Count all pending requests for this resource/piece combination (assigned or not)
                        onTheWay += request.amount;
                    }
                }
                
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
                        assignedDrone = drone  // Assign immediately to prevent duplicate requests
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
    
    public bool TryDepositResource(ResourceType type, int amount, Unit_Construct drone)
    {
        ConstructionRequest request = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone && r.type == type);
        
        if (request == null)
        {
            Debug.Log($"[ConstructionSite:{name}] Deposit FAILED: no matching request for {type}");
            return false;
        }
        
        // Distribute resources to pieces that need them
        int remainingAmount = amount;
        
        // First, update total delivered resources (for backward compatibility)
        int totalNeeded = _requiredResources.ContainsKey(type) ? _requiredResources[type] : 0;
        int totalDelivered = _deliveredResources.ContainsKey(type) ? _deliveredResources[type] : 0;
        int totalCanAccept = Mathf.Min(remainingAmount, totalNeeded - totalDelivered);
        
        if (totalCanAccept > 0)
        {
            if (!_deliveredResources.ContainsKey(type))
            {
                _deliveredResources[type] = 0;
            }
            _deliveredResources[type] += totalCanAccept;
            
            // Distribute to pieces that need this resource
            foreach (var pieceCell in _pieceRequiredResources.Keys)
            {
                if (remainingAmount <= 0) break;
                if (_constructedPieces.ContainsKey(pieceCell) && _constructedPieces[pieceCell]) continue;
                
                var pieceRequired = _pieceRequiredResources[pieceCell];
                if (pieceRequired.ContainsKey(type))
                {
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
                        
                        Debug.Log($"[ConstructionSite:{name}] Piece at {pieceCell}: {type} +{pieceCanAccept} (total: {pieceDelivered[type]}/{pieceNeeded})");
                    }
                }
            }
            
            _pendingRequests.Remove(request);
            Debug.Log($"[ConstructionSite:{name}] Deposit: {type} +{totalCanAccept} (total: {_deliveredResources[type]}/{totalNeeded})");
            return true;
        }
        
        _pendingRequests.Remove(request);
        return false;
    }
    
    public void CancelRequest(ConstructionRequest request)
    {
        if (_pendingRequests.Contains(request))
        {
            _pendingRequests.Remove(request);
        }
    }
    
    public class ConstructionRequest
    {
        public ResourceType type;
        public int amount;
        public ConstructionSite site;
        public Unit_Construct assignedDrone;
        public Vector3Int? targetPieceCell; // The piece cell that needs this resource
    }
}

