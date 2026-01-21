using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AreaBuildingDestroyer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Tilemap selectionTilemap;
    [SerializeField] private TileBase selectionTile;

    private Grid _grid;
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
            }
        }
    }
    
    private void EndDrag()
    {
        if (!_isDragging) return;
        
        if (_hasMoved)
        {
            _wasJustFinishedAreaDrag = true;
            DestroyBuildingsInArea();
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
    
    private void DestroyBuildingsInArea()
    {
        if (_buildingManager == null) return;
        
        HashSet<Vector3Int> buildingsToDestroy = new HashSet<Vector3Int>();
        HashSet<GameObject> storagesToClear = new HashSet<GameObject>();
        
        foreach (Vector3Int cell in _selectedCells)
        {
            if (_buildingManager.IsMainStructureCell(cell))
            {
                continue;
            }
            
            // Storage 자원 버림
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
        
        DestroyConstructionSitesInArea();
        
        foreach (Vector3Int cell in buildingsToDestroy)
        {
            _buildingManager.ClearBuildingDataAt(cell);
        }
        
        DestroyBeaconsInArea();
    }
    
    private void DestroyConstructionSitesInArea()
    {
        if (_grid == null) return;
        
        List<ConstructionSite> sitesToDestroy = new List<ConstructionSite>();
        ConstructionSite[] allSites = FindObjectsByType<ConstructionSite>(FindObjectsSortMode.None);
        
        foreach (ConstructionSite site in allSites)
        {
            if (site == null) continue;
            
            Vector3Int siteCell = site.cellPosition;
            if (_selectedCells.Contains(siteCell))
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
                        if (_selectedCells.Contains(pieceCell))
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
    
    private void CancelConstructionSite(ConstructionSite site)
    {
        Unit_Construct[] allConstructUnits = FindObjectsByType<Unit_Construct>(FindObjectsSortMode.None);
        foreach (Unit_Construct unit in allConstructUnits)
        {
            if (unit != null)
            {
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
    
    private void DestroyBeaconsInArea()
    {
        List<Beacon> beaconsToDestroy = new List<Beacon>();
        Beacon[] allBeacons = FindObjectsByType<Beacon>(FindObjectsSortMode.None);
        
        foreach (Beacon beacon in allBeacons)
        {
            if (beacon == null) continue;
            
            Vector3Int beaconCell = _grid.WorldToCell(beacon.transform.position);
            
            if (_selectedCells.Contains(beaconCell))
            {
                beaconsToDestroy.Add(beacon);
            }
        }
        
        foreach (Beacon beacon in beaconsToDestroy)
        {
            if (beacon != null)
            {
                beacon.DestroyBeacon();
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
}

