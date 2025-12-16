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
    private readonly Dictionary<Unit_Construct, Vector3Int> _droneInteractionCells = new ();
    private readonly HashSet<ConstructionRequest> _requestsBeingDelivered = new ();
    
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
        
        BuildingPieceData[] buildingPieceDataList = Resources.LoadAll<BuildingPieceData>("Building Pieces");
        Dictionary<BuildingPieceType, BuildingPieceData> buildingPieceAndDataMap = new Dictionary<BuildingPieceType, BuildingPieceData>();
        
        foreach (var data in buildingPieceDataList)
        {
            if (data.buildingPieceType != BuildingPieceType.None)
            {
                buildingPieceAndDataMap.TryAdd(data.buildingPieceType, data);
            }
        }
        
        foreach (var piece in buildingData.recipe)
        {
            Vector3Int pieceCell = cellPosition + piece.relativePosition;
            _constructedPieces[pieceCell] = false;
            
            if (buildingPieceAndDataMap.TryGetValue(piece.buildingPieceType, out BuildingPieceData pieceData) && pieceData.costs != null)
            {
                Dictionary<ResourceType, int> pieceRequired = new Dictionary<ResourceType, int>(); // 요청 중인 남은 자원
                Dictionary<ResourceType, int> pieceDelivered = new Dictionary<ResourceType, int>(); // 지금까지 배달된 자원
                
                foreach (var cost in pieceData.costs)
                {
                    pieceRequired[cost.resourceType] = cost.amount;
                    pieceDelivered[cost.resourceType] = 0;
                }
                
                _pieceRequiredResources[pieceCell] = pieceRequired;
                _pieceDeliveredResources[pieceCell] = pieceDelivered;
            }
        }
    }

    private void ReleaseDroneFromPiece(Vector3Int pieceCell, Unit_Construct drone)
    {
        if (_pieceAssignedDrone.ContainsKey(pieceCell) && _pieceAssignedDrone[pieceCell] == drone)
        {
            _pieceAssignedDrone.Remove(pieceCell);
        }
    }

    private bool PieceHasAllResources(Vector3Int pieceCell)
    {
        if (!_pieceRequiredResources.TryGetValue(pieceCell, out var required))
        {
            return false;
        }

        var delivered = _pieceDeliveredResources.TryGetValue(pieceCell, out var resource) 
            ? resource 
            : new Dictionary<ResourceType, int>();
        
        foreach (var req in required)
        {
            int deliveredAmount = delivered.TryGetValue(req.Key, out var value) ? value : 0;
            if (deliveredAmount < req.Value)
            {
                return false;
            }
        }
        
        return true;
    }

    private bool IsPieceConstructed(Vector3Int pieceCell)
    {
        return _constructedPieces.ContainsKey(pieceCell) && _constructedPieces[pieceCell];
    }

    private void MarkPieceAsConstructed(Vector3Int pieceCell, Unit_Construct drone)
    {
        if (_constructedPieces.ContainsKey(pieceCell))
        {
            _constructedPieces[pieceCell] = true;
            _pendingRequests.RemoveAll(r => r.targetPieceCell == pieceCell); // 남아있는 요청 모두 제거
            
            if (drone != null)
            {
                ReleaseDroneFromPiece(pieceCell, drone);
            }
            
            if (AreAllPiecesConstructed())
            {
                IsComplete = true;
            }
        }
    }
    
    // 드론에게 자원 배달을 위한 상호작용 셀 할당
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
        
        // 상호작용 가능한 셀이 없는 경우 셀 자체를 할당
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
    
    // 가장 가까운 상호작용 셀 찾는 함수
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
            new (1, 0, 0), new (-1, 0, 0),
            new (0, 1, 0), new (0, -1, 0)
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
    }
    
    public void CalculateComboCosts()
    {
        _requiredResources.Clear();
        _deliveredResources.Clear();
        
        if (buildingData == null)
        {
            return;
        }
        
        if (buildingData.recipe == null || buildingData.recipe.Count == 0)
        {
            return;
        }
        
        BuildingPieceData[] buildingPieceDataList = Resources.LoadAll<BuildingPieceData>("Building Pieces");
        Dictionary<BuildingPieceType, BuildingPieceData> buildingPieceAndDataMap = new Dictionary<BuildingPieceType, BuildingPieceData>();
        
        foreach (var data in buildingPieceDataList)
        {
            if (data.buildingPieceType != BuildingPieceType.None)
            {
                buildingPieceAndDataMap.TryAdd(data.buildingPieceType, data);
            }
        }
        
        foreach (var piece in buildingData.recipe)
        {
            if (!buildingPieceAndDataMap.TryGetValue(piece.buildingPieceType, out BuildingPieceData pieceData) || pieceData.costs == null)
            {
                continue;
            }

            foreach (var cost in pieceData.costs)
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
    
    public bool AreAllPiecesConstructed()
    {
        if (_constructedPieces.Count == 0) return false;
        
        foreach (var constructed in _constructedPieces.Values)
        {
            if (!constructed) return false;
        }
        return true;
    }
    
    public ConstructionRequest GetAndAssignNextResourceRequest(Unit_Construct drone, int droneCapacity)
    {
        if (_requiredResources.Count == 0 || drone == null)
        {
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
            var pieceDelivered = _pieceDeliveredResources.TryGetValue(pieceCell, out var resource) 
                ? resource 
                : new Dictionary<ResourceType, int>();
            
            foreach (var required in pieceRequired)
            {
                int deliveredAmount = pieceDelivered.TryGetValue(required.Key, out var value) ? value : 0;
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
                    return newRequest;
                }
            }
        }
        
        return null;
    }
    
    public bool CanDepositResource(ResourceType type, Unit_Construct drone)
    {
        if (drone == null) return false;
        
        ConstructionRequest request = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone && r.type == type);
        
        if (request == null)
        {
            // 해당하는 요청 없음
            bool isDuplicate = _requestsBeingDelivered.Any(r => r.assignedDrone == drone && r.type == type);
            if (isDuplicate)
            {
                return false; // 이미 다른 드론에 의해 자원 배달이 완료됨
            }
            // 아예 요청이 존재하지 않음
            return false;
        }
        
        // 자원이 현재 배달 중인 상태임
        if (_requestsBeingDelivered.Contains(request))
        {
            return false;
        }
        
        // 이 자원이 필요한지 확인
        int totalNeeded = _requiredResources.TryGetValue(type, out var requiredResource) ? requiredResource : 0;
        int totalDelivered = _deliveredResources.TryGetValue(type, out var resource) ? resource : 0;
        
        if (totalNeeded <= totalDelivered)
        {
            return false; // 필요한 자원의 양보다 배달되는 양이 더 많음
        }
        
        // 자원 요청이 존재하고, 아직 배달되지 않았으며, 자원이 필요함
        return true;
    }
    
    public bool TryDepositResource(ResourceType type, int amount, Unit_Construct drone)
    {
        ConstructionRequest request = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone && r.type == type);
        
        if (request == null)
        {
            // 현재 이미 진행 중인 요청인지 확인
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
        
        // 이미 배달 중인 요청인지 확인
        if (!_requestsBeingDelivered.Add(request))
        {
            return false;
        }
        
        // '보류 중'에서 제거하기 전에 '배달 됨'으로 요청을 표시
        _pendingRequests.Remove(request);
        
        try
        {
            int remainingAmount = amount;
            int totalNeeded = _requiredResources.TryGetValue(type, out var resource) ? resource : 0;
            int totalDelivered = _deliveredResources.TryGetValue(type, out var deliveredResource) ? deliveredResource : 0;
            int totalCanAccept = Mathf.Min(remainingAmount, totalNeeded - totalDelivered);
            
            if (totalCanAccept <= 0)
            {
                Debug.Log($"[ConstructionSite:{name}] Deposit rejected: piece already has sufficient resources of type {type}");
                return false;
            }

            _deliveredResources.TryAdd(type, 0);
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
                int pieceDeliveredAmount = pieceDelivered.TryGetValue(type, out var value) ? value : 0;
                int pieceCanAccept = Mathf.Min(remainingAmount, pieceNeeded - pieceDeliveredAmount);
                
                if (pieceCanAccept > 0)
                {
                    pieceDelivered.TryAdd(type, 0);
                    pieceDelivered[type] += pieceCanAccept;
                    remainingAmount -= pieceCanAccept;
                    piecesThatReceivedResources.Add(pieceCell);
                }
            }
            
            foreach (var pieceCell in piecesThatReceivedResources)
            {
                if (PieceHasAllResources(pieceCell) && !IsPieceConstructed(pieceCell))
                {
                    StartPieceConstruction(pieceCell);
                }
            }
            
            return true;
        }
        finally
        {
            _requestsBeingDelivered.Remove(request);
        }
    }
    
    private void StartPieceConstruction(Vector3Int pieceCell)
    {
        if (IsPieceConstructed(pieceCell))
        {
            return;
        }
        
        if (!PieceHasAllResources(pieceCell))
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
            MarkPieceAsConstructed(pieceCell, null);
            
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
    
    // IDLE 상태인 유닛들의 요청을 제거
    private void CleanupStaleRequests()
    {
        _pendingRequests.RemoveAll(r => {
            if (r.assignedDrone == null) return true;
            return r.assignedDrone.IsIdle();
        });
        
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
        public ResourceType type;
        public int amount;
        public ConstructionSite site;
        public Unit_Construct assignedDrone;
        public Vector3Int? targetPieceCell;
    }
}

