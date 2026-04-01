using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using FMODUnity;

public class AreaBuildingDestroyer : MonoBehaviour
{
    public static event Action OnDemolishComplete;
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Tilemap selectionTilemap;
    [SerializeField] private TileBase selectionTile;
    [Header("Audio")]
    [SerializeField] private EventReference dragSound;
    [SerializeField] private float dragSoundCooldown = 0.1f;

    private Grid _grid;
    private float _lastDragSoundTime;
    private BuildingManager _buildingManager;
    private bool _isDragging;
    private bool _hasMoved;
    private bool _justFinishedAreaDrag;
    private Vector3Int _startCell;
    private Vector3Int _endCell;
    private Vector3 _startWorldPos;
    private readonly HashSet<Vector3Int> _selectedCells = new ();
    private readonly HashSet<Vector3Int> _previouslySelectedCells = new ();

    public bool HasMoved => _hasMoved;
    public bool JustFinishedAreaDrag => _justFinishedAreaDrag;
    
    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }
    
    private void Start()
    {
        _buildingManager = BuildingManager.Instance;
        if (_buildingManager != null)
        {
            _grid = _buildingManager.grid;
        }
    }
    
    private bool _wasJustFinishedAreaDrag;
    
    private void Update()
    {
        _justFinishedAreaDrag = _wasJustFinishedAreaDrag;
        _wasJustFinishedAreaDrag = false;
        
        HandleRightClickDrag();
    }
    
    private void HandleRightClickDrag()
    {
        if (IsLoadingScreenActive())
        {
            if (_isDragging)
            {
                CancelDrag();
            }
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsDragging())
        {
            if (_isDragging)
            {
                CancelDrag();
            }
            return;
        }
        
        if (Input.GetMouseButtonDown(1))
        {
            StartDrag();
        }
        
        if (_isDragging && Input.GetMouseButton(1))
        {
            UpdateDrag();
        }
        
        if (_isDragging && Input.GetMouseButtonUp(1))
        {
            EndDrag();
        }
    }
    
    private void StartDrag()
    {
        if (_grid == null || mainCamera == null) return;
        
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0;
        _startWorldPos = worldPos;
        _startCell = _grid.WorldToCell(worldPos);
        _endCell = _startCell;
        
        _isDragging = true;
        _hasMoved = false;
        _justFinishedAreaDrag = false;
        _wasJustFinishedAreaDrag = false;
        _selectedCells.Clear();
        _previouslySelectedCells.Clear();
    }
    
    private void UpdateDrag()
    {
        if (_grid == null || mainCamera == null) return;
        
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0;
        Vector3Int newEndCell = _grid.WorldToCell(worldPos);
        
        float moveDistance = Vector3.Distance(_startWorldPos, worldPos);
        if (moveDistance > 0.3f)
        {
            _hasMoved = true;
        }
        
        if (newEndCell != _endCell)
        {
            _endCell = newEndCell;
            if (_hasMoved)
            {
                UpdateSelectionVisual();
                PlayDragSound();
            }
        }
    }
    
    private void EndDrag()
    {
        if (!_isDragging) return;
        
        if (_hasMoved)
        {
            _wasJustFinishedAreaDrag = true;
            HashSet<Vector3Int> cellsCopy = new HashSet<Vector3Int>(_selectedCells);
            List<DemolishTarget> targets = CollectDemolishTargets(cellsCopy, out List<ConstructionSite> sitesToDestroyImmediately);
            DestroyEmptyConstructionSitesImmediately(sitesToDestroyImmediately);
            DemolishConfirmUIManager demolishUI = FindFirstObjectByType<DemolishConfirmUIManager>();
            if (targets.Count > 0 && demolishUI != null)
            {
                demolishUI.Show(cellsCopy, targets);
            }
            else if (targets.Count > 0)
            {
                ExecuteDemolish(cellsCopy);
            }
        }
        
        ClearSelectionVisual();
        
        _isDragging = false;
        _hasMoved = false;
        _selectedCells.Clear();
        _previouslySelectedCells.Clear();
    }
    
    private void CancelDrag()
    {
        ClearSelectionVisual();
        _isDragging = false;
        _justFinishedAreaDrag = false;
        _wasJustFinishedAreaDrag = false;
        _selectedCells.Clear();
        _previouslySelectedCells.Clear();
    }
    
    private void UpdateSelectionVisual()
    {
        if (selectionTilemap == null) return;
        
        int minX = Mathf.Min(_startCell.x, _endCell.x);
        int maxX = Mathf.Max(_startCell.x, _endCell.x);
        int minY = Mathf.Min(_startCell.y, _endCell.y);
        int maxY = Mathf.Max(_startCell.y, _endCell.y);
        
        HashSet<Vector3Int> newSelectedCells = new HashSet<Vector3Int>();
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                newSelectedCells.Add(cell);
            }
        }
        
        foreach (Vector3Int cell in _previouslySelectedCells)
        {
            if (!newSelectedCells.Contains(cell))
            {
                selectionTilemap.SetTile(cell, null);
            }
        }
        
        _selectedCells.Clear();
        _selectedCells.UnionWith(newSelectedCells);
        
        if (selectionTile != null)
        {
            foreach (Vector3Int cell in _selectedCells)
            {
                selectionTilemap.SetTile(cell, selectionTile);
            }
        }
        
        _previouslySelectedCells.Clear();
        _previouslySelectedCells.UnionWith(_selectedCells);
        
        if (selectionTilemap != null)
        {
            selectionTilemap.RefreshAllTiles();
        }
    }
    
    private void ClearSelectionVisual()
    {
        if (selectionTilemap == null) return;
        
        foreach (Vector3Int cell in _selectedCells)
        {
            selectionTilemap.SetTile(cell, null);
        }
        
        foreach (Vector3Int cell in _previouslySelectedCells)
        {
            selectionTilemap.SetTile(cell, null);
        }
        
        if (selectionTilemap != null)
        {
            selectionTilemap.RefreshAllTiles();
        }
        
        _selectedCells.Clear();
        _previouslySelectedCells.Clear();
    }
    
    private bool HasBuildingAt(Vector3Int cell)
    {
        if (_buildingManager.GetBuildingAt(cell, out _))
        {
            return true;
        }
        
        if (_buildingManager.IsBuildingTile(cell))
        {
            return true;
        }
        
        return false;
    }

    public void ExecuteDemolish(HashSet<Vector3Int> cells)
    {
        DestroyBuildingsInArea(cells);
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null) {
            GameManager.Instance.uiManager.HideProcessorAndDroneHubPanels();
        }
        OnDemolishComplete?.Invoke();
    }

    private List<DemolishTarget> CollectDemolishTargets(HashSet<Vector3Int> selectedCells, out List<ConstructionSite> sitesToDestroyImmediately)
    {
        sitesToDestroyImmediately = new List<ConstructionSite>();
        List<DemolishTarget> targets = new List<DemolishTarget>();
        HashSet<Vector3Int> seenAnchors = new HashSet<Vector3Int>();
        HashSet<ConstructionSite> seenSites = new HashSet<ConstructionSite>();
        HashSet<Vector3Int> seenPieceCells = new HashSet<Vector3Int>();

        HashSet<Vector3Int> constructionSiteCells = CollectConstructionSiteCells(selectedCells);

        foreach (Vector3Int cell in selectedCells)
        {
            if (_buildingManager.IsMainStructureCell(cell))
                continue;

            if (constructionSiteCells.Contains(cell))
                continue;

            if (_buildingManager.GetBuildingAt(cell, out List<Vector3Int> occupiedCells))
            {
                Vector3Int anchor = GetAnchorForCell(cell);
                if (!seenAnchors.Add(anchor))
                    continue;

                BuildingPiece piece = _buildingManager.GetPieceAt(cell);
                BuildingData data = null;
                if (piece != null)
                {
                    BuildingDataHolder holder = piece.GetComponent<BuildingDataHolder>();
                    if (holder != null && holder.buildingData != null)
                        data = holder.buildingData;
                }
                if (data == null)
                    data = _buildingManager.GetBuildingDataAt(anchor);

                bool isSingleCellStructure = occupiedCells != null && occupiedCells.Count == 1;

                if (data != null && !isSingleCellStructure)
                {
                    string name = !string.IsNullOrEmpty(data.displayName) ? data.displayName : data.name;
                    targets.Add(new DemolishTarget
                    {
                        displayName = name,
                        anchorCell = anchor,
                        buildingData = data,
                        isConstructionSite = false
                    });
                }
                else if (piece != null && !seenPieceCells.Contains(cell))
                {
                    seenPieceCells.Add(cell);
                    BuildingPieceData pieceData = GetBuildingPieceData(piece.buildingPieceType);
                    if (pieceData != null)
                    {
                        string name = !string.IsNullOrEmpty(pieceData.displayName) ? pieceData.displayName : pieceData.name;
                        targets.Add(new DemolishTarget
                        {
                            displayName = name,
                            anchorCell = cell,
                            buildingData = null,
                            buildingPieceType = piece.buildingPieceType,
                            isConstructionSite = false
                        });
                    }
                }
            }
            else if (HasBuildingAt(cell))
            {
                BuildingPiece piece = _buildingManager.GetPieceAt(cell);
                if (piece != null && !seenPieceCells.Contains(cell))
                {
                    seenPieceCells.Add(cell);
                    BuildingPieceData pieceData = GetBuildingPieceData(piece.buildingPieceType);
                    if (pieceData != null)
                    {
                        string name = !string.IsNullOrEmpty(pieceData.displayName) ? pieceData.displayName : pieceData.name;
                        targets.Add(new DemolishTarget
                        {
                            displayName = name,
                            anchorCell = cell,
                            buildingData = null,
                            buildingPieceType = piece.buildingPieceType,
                            isConstructionSite = false
                        });
                    }
                }
            }
        }

        IReadOnlyList<ConstructionSite> allSites = ConstructionManager.Instance != null
            ? ConstructionManager.Instance.ConstructionSites
            : System.Array.Empty<ConstructionSite>();
        foreach (ConstructionSite site in allSites)
        {
            if (site == null || seenSites.Contains(site)) continue;

            bool overlap = false;
            if (selectedCells.Contains(site.cellPosition))
            {
                overlap = true;
            }
            else if (site.buildingData != null && site.buildingData.recipe != null)
            {
                foreach (var piece in site.buildingData.recipe)
                {
                    Vector3Int pieceCell = site.cellPosition + piece.relativePosition;
                    if (selectedCells.Contains(pieceCell))
                    {
                        overlap = true;
                        break;
                    }
                }
            }

            if (overlap)
            {
                seenSites.Add(site);
                if (!site.HasAnyConstructedPieces())
                {
                    sitesToDestroyImmediately.Add(site);
                }
                else
                {
                    string name = "Construction";
                    if (site.buildingData != null)
                        name = !string.IsNullOrEmpty(site.buildingData.displayName) ? site.buildingData.displayName : site.buildingData.name;
                    Dictionary<ResourceType, int> refund = site.GetConstructedPiecesCost();
                    targets.Add(new DemolishTarget
                    {
                        displayName = name,
                        anchorCell = site.cellPosition,
                        buildingData = site.buildingData,
                        isConstructionSite = true,
                        constructionSite = site,
                        preCalculatedRefund = refund
                    });
                }
            }
        }

        CollectUnitTargets(selectedCells, targets);

        return targets;
    }

    private void CollectUnitTargets(HashSet<Vector3Int> selectedCells, List<DemolishTarget> targets)
    {
        if (_grid == null || UnitManager.Instance == null)
        {
            return;
        }

        void AddUnit(UnitBase unit)
        {
            if (unit == null || unit is Unit_Player)
            {
                return;
            }
            if (unit is Unit_Construct construct && construct.IsInvulnerable)
            {
                return;
            }
            Vector3Int unitCell = _grid.WorldToCell(unit.transform.position);
            if (!selectedCells.Contains(unitCell))
            {
                return;
            }
            string displayName = unit.unitData != null && !string.IsNullOrEmpty(unit.unitData.unitName) ? unit.unitData.unitName : (unit.unitData != null ? unit.unitData.name : unit.GetType().Name);
            Dictionary<ResourceType, int> cost = GetUnitProductionCost(unit.unitData);

            targets.Add(new DemolishTarget
            {
                displayName = displayName,
                anchorCell = default,
                buildingData = null,
                buildingPieceType = BuildingPieceType.None,
                isConstructionSite = false,
                constructionSite = null,
                preCalculatedRefund = cost,
                isUnit = true
            });
        }

        foreach (UnitBase unit in UnitManager.Instance.AllyUnits)
        {
            AddUnit(unit);
        }
        foreach (UnitBase unit in UnitManager.Instance.EnemyUnits)
        {
            AddUnit(unit);
        }
    }

    private static Dictionary<ResourceType, int> GetUnitProductionCost(UnitData unitData)
    {
        Dictionary<ResourceType, int> result = new Dictionary<ResourceType, int>();
        if (unitData == null || unitData.productionCosts == null)
        {
            return result;
        }
        foreach (ResourceCost cost in unitData.productionCosts)
        {
            if (result.ContainsKey(cost.resourceType))
            {
                result[cost.resourceType] += cost.amount;
            }
            else
            {
                result[cost.resourceType] = cost.amount;
            }
        }
        return result;
    }

    private HashSet<Vector3Int> CollectConstructionSiteCells(HashSet<Vector3Int> selectedCells)
    {
        HashSet<Vector3Int> result = new HashSet<Vector3Int>();
        IReadOnlyList<ConstructionSite> allSites = ConstructionManager.Instance != null
            ? ConstructionManager.Instance.ConstructionSites
            : System.Array.Empty<ConstructionSite>();
        foreach (ConstructionSite site in allSites)
        {
            if (site == null) continue;

            bool overlap = false;
            if (selectedCells.Contains(site.cellPosition))
                overlap = true;
            else if (site.buildingData != null && site.buildingData.recipe != null)
            {
                foreach (var piece in site.buildingData.recipe)
                {
                    Vector3Int pieceCell = site.cellPosition + piece.relativePosition;
                    if (selectedCells.Contains(pieceCell))
                    {
                        overlap = true;
                        break;
                    }
                }
            }

            if (overlap && site.HasAnyConstructedPieces())
            {
                result.Add(site.cellPosition);
                if (site.buildingData != null && site.buildingData.recipe != null)
                {
                    foreach (var piece in site.buildingData.recipe)
                        result.Add(site.cellPosition + piece.relativePosition);
                }
            }
        }
        return result;
    }

    private Vector3Int GetAnchorForCell(Vector3Int cell)
    {
        if (_buildingManager.GetBuildingAt(cell, out List<Vector3Int> _))
        {
            BuildingPiece piece = _buildingManager.GetPieceAt(cell);
            if (piece != null)
                return piece.cellPosition;
        }
        return default;
    }

    private static BuildingPieceData GetBuildingPieceData(BuildingPieceType type)
    {
        if (type == BuildingPieceType.None) return null;
        BuildingPieceData[] all = Resources.LoadAll<BuildingPieceData>("Building Pieces");
        foreach (var data in all)
        {
            if (data.buildingPieceType == type)
                return data;
        }
        return null;
    }
    
    private void DestroyBuildingsInArea(HashSet<Vector3Int> selectedCells)
    {
        if (_buildingManager == null) return;
        
        HashSet<Vector3Int> buildingsToDestroy = new HashSet<Vector3Int>();
        HashSet<GameObject> storagesToClear = new HashSet<GameObject>();
        
        foreach (Vector3Int cell in selectedCells)
        {
            if (_buildingManager.IsMainStructureCell(cell))
            {
                continue;
            }
            
            if (HasBuildingAt(cell))
            {
                BuildingPiece piece = _buildingManager.GetPieceAt(cell);
                if (piece != null && piece.gameObject != null)
                {
                    IStorage storage = piece.GetComponent<IStorage>();
                    if (storage != null && storage is MonoBehaviour storageMono)
                    {
                        storagesToClear.Add(storageMono.gameObject);
                    }
                }
                
                if (_buildingManager.GetBuildingAt(cell, out _))
                {
                    buildingsToDestroy.Add(cell);
                }
                else
                {
                    buildingsToDestroy.Add(cell);
                }
            }
        }
        
        foreach (GameObject storageObj in storagesToClear)
        {
            if (storageObj != null)
            {
                IStorage storage = storageObj.GetComponent<IStorage>();
                if (storage != null)
                {
                    ClearStorageResources(storage);
                }
            }
        }
        
        DestroyConstructionSitesInArea(selectedCells);
        
        foreach (Vector3Int cell in buildingsToDestroy)
        {
            _buildingManager.ClearBuildingDataAt(cell);
        }
        
        DestroyUnitsInArea(selectedCells);
    }

    private void DestroyUnitsInArea(HashSet<Vector3Int> cells)
    {
        if (_grid == null || UnitManager.Instance == null)
        {
            return;
        }

        List<UnitBase> unitsToDestroy = new List<UnitBase>();

        foreach (UnitBase unit in UnitManager.Instance.AllyUnits)
        {
            if (unit == null || unit is Unit_Player)
            {
                continue;
            }
            if (unit is Unit_Construct construct && construct.IsInvulnerable)
            {
                continue;
            }
            Vector3Int unitCell = _grid.WorldToCell(unit.transform.position);
            if (cells.Contains(unitCell))
            {
                unitsToDestroy.Add(unit);
            }
        }

        foreach (UnitBase unit in UnitManager.Instance.EnemyUnits)
        {
            if (unit == null)
            {
                continue;
            }
            Vector3Int unitCell = _grid.WorldToCell(unit.transform.position);
            if (cells.Contains(unitCell))
            {
                unitsToDestroy.Add(unit);
            }
        }

        foreach (UnitBase unit in unitsToDestroy)
        {
            if (unit != null && unit.gameObject != null)
            {
                UnitManager.Instance.RemoveUnit(unit);
                Destroy(unit.gameObject);
            }
        }
    }
    
    private void DestroyConstructionSitesInArea(HashSet<Vector3Int> cells)
    {
        if (_grid == null) return;
        
        List<ConstructionSite> sitesToDestroy = new List<ConstructionSite>();
        IReadOnlyList<ConstructionSite> allSites = ConstructionManager.Instance != null
            ? ConstructionManager.Instance.ConstructionSites
            : System.Array.Empty<ConstructionSite>();
        
        foreach (ConstructionSite site in allSites)
        {
            if (site == null) continue;
            
            Vector3Int siteCell = site.cellPosition;
            if (cells.Contains(siteCell))
            {
                sitesToDestroy.Add(site);
            }
            else
            {
                if (site.buildingData != null && site.buildingData.recipe != null)
                {
                    foreach (var piece in site.buildingData.recipe)
                    {
                        Vector3Int pieceCell = siteCell + piece.relativePosition;
                        if (cells.Contains(pieceCell))
                        {
                            sitesToDestroy.Add(site);
                            break;
                        }
                    }
                }
            }
        }
        
        foreach (ConstructionSite site in sitesToDestroy)
        {
            if (site != null)
            {
                CancelConstructionSite(site);
                Destroy(site.gameObject);
            }
        }
    }
    
    private void DestroyEmptyConstructionSitesImmediately(List<ConstructionSite> sites)
    {
        if (sites == null) return;
        foreach (ConstructionSite site in sites)
        {
            if (site != null)
            {
                CancelConstructionSite(site);
                Destroy(site.gameObject);
            }
        }
    }

    private void CancelConstructionSite(ConstructionSite site)
    {
        IReadOnlyList<Unit_Construct> allConstructUnits = ConstructionManager.Instance != null
            ? ConstructionManager.Instance.ConstructDrones
            : System.Array.Empty<Unit_Construct>();
        foreach (Unit_Construct unit in allConstructUnits)
        {
            if (unit != null)
            {
                unit.NotifySiteDestroyed(site);
                site.ReleaseDrone(unit);
            }
        }
        
        if (site.buildingData != null)
        {
            _buildingManager.ClearConstructionSiteData(site.cellPosition, site.buildingData);
        }
    }
    
    private void ClearStorageResources(IStorage storage)
    {
        Dictionary<ResourceType, int> storedResources = storage.GetStoredResources();
        List<KeyValuePair<ResourceType, int>> resourcesCopy = new List<KeyValuePair<ResourceType, int>>(storedResources);
        
        foreach (KeyValuePair<ResourceType, int> resource in resourcesCopy)
        {
            if (resource.Value > 0)
            {
                storage.TryWithdrawResource(resource.Key, resource.Value, out _);
            }
        }
    }
    
    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null)
        {
            return false;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        if (loadingScreen == null)
        {
            return false;
        }

        return loadingScreen.gameObject.activeSelf;
    }
    
    private void OnDisable()
    {
        if (_isDragging)
        {
            CancelDrag();
        }
    }

    private void PlayDragSound()
    {
        if (dragSound.IsNull)
        {
            return;
        }

        float currentTime = Time.time;
        if (currentTime - _lastDragSoundTime < dragSoundCooldown)
        {
            return;
        }

        _lastDragSoundTime = currentTime;
        RuntimeManager.PlayOneShot(dragSound);
    }
}

