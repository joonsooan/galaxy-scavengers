using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

public class CardDragger : MonoBehaviour
{
    private const string DraggingSortingLayerName = "Dragging";
    public bool IsDragging => _isDragging;

    public bool IsDraggingBuildingCard => _isDragging && _activeBuildingData != null;

    public bool IsDraggingPowerGridPreviewBuilding =>
        IsDraggingBuildingCard &&
        IsPowerGridPreviewBuildingType(_activeBuildingData.buildingType);

    public static bool IsPowerGridPreviewBuildingType(BuildingType buildingType)
    {
        return buildingType == BuildingType.Generator ||
               buildingType == BuildingType.Battery ||
               buildingType == BuildingType.PowerReceiver;
    }

    [Header("References")]
    [SerializeField] private Grid grid;

    [Header("Drag Placement")]
    [SerializeField] private Tilemap platformPreviewTilemap;
    [SerializeField] private TileBase platformPreviewTile;
    [SerializeField] private Tilemap wallPreviewTilemap;
    [SerializeField] private TileBase wallPreviewTile;

    private GameObject _ghostBuildingInstance;
    private SpriteRenderer _ghostBuildingRenderer;
    private BuildingData _activeBuildingData;
    private Vector3Int _lastPlacedCell;
    private bool _isDragging;

    private List<Vector3Int> _placedCellsInDrag;
    private PointerEventData _pointerEventData;
    private List<ResourceCost> _cachedComboCosts;
    private ResourceCost[] _cachedComboCostArray;

    private bool _isDragPlacing;
    private Vector3Int _dragStartCell;
    private Vector3Int _dragCurrentCell;
    private List<Vector3Int> _bresenhamScratch;

    private Camera _mainCamera;
    private readonly List<RaycastResult> _raycastResultsCache = new List<RaycastResult>();

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (!_isDragging) return;

        HandleDragVisuals();
        HandleMousePlacement();
    }

    public void StartDrag(DisplayableData data)
    {
        if (_isDragging)
        {
            EndDrag();
        }

        BuildingData buildingData = data as BuildingData;
        if (buildingData != null && buildingData.buildingPrefab != null)
        {
            _activeBuildingData = buildingData;
            _isDragging = true;
            _lastPlacedCell = Vector3Int.one * int.MaxValue;
            _placedCellsInDrag = new List<Vector3Int>();

            _cachedComboCosts = CalculateComboCosts(buildingData);
            _cachedComboCostArray = _cachedComboCosts.Count == 0 ? null : _cachedComboCosts.ToArray();

            CreateGhostBuilding();

            if (PowerCoveragePreviewOverlay.Instance != null)
            {
                PowerCoveragePreviewOverlay.Instance.Show();
            }
        }
    }

    private List<ResourceCost> CalculateComboCosts(BuildingData comboData)
    {
        List<ResourceCost> totalCosts = new List<ResourceCost>();
        Dictionary<ResourceType, int> costDict = new Dictionary<ResourceType, int>();

        BuildingPieceData[] allCards = Resources.LoadAll<BuildingPieceData>("Building Piece Data");
        Dictionary<BuildingPieceType, BuildingPieceData> gadgetToCardMap = new Dictionary<BuildingPieceType, BuildingPieceData>();

        foreach (var card in allCards)
        {
            if (card.buildingPieceType != BuildingPieceType.None && !gadgetToCardMap.ContainsKey(card.buildingPieceType))
            {
                gadgetToCardMap[card.buildingPieceType] = card;
            }
        }

        if (comboData.recipe != null)
        {
            foreach (var piece in comboData.recipe)
            {
                if (gadgetToCardMap.TryGetValue(piece.buildingPieceType, out BuildingPieceData pieceCard) && pieceCard.costs != null)
                {
                    foreach (var cost in pieceCard.costs)
                    {
                        if (costDict.ContainsKey(cost.resourceType))
                        {
                            costDict[cost.resourceType] += cost.amount;
                        }
                        else
                        {
                            costDict[cost.resourceType] = cost.amount;
                        }
                    }
                }
            }
        }

        foreach (var kvp in costDict)
        {
            totalCosts.Add(new ResourceCost { resourceType = kvp.Key, amount = kvp.Value });
        }

        return totalCosts;
    }

    public void EndDrag()
    {
        bool wasDraggingBuildingCard = IsDraggingBuildingCard;

        ClearPlatformPreview();
        ClearWallPreview();
        _isDragPlacing = false;

        if (_ghostBuildingInstance != null)
        {
            Destroy(_ghostBuildingInstance);
        }

        _isDragging = false;
        _activeBuildingData = null;
        _cachedComboCosts = null;
        _cachedComboCostArray = null;
        _ghostBuildingRenderer = null;

        if (wasDraggingBuildingCard && PowerCoveragePreviewOverlay.Instance != null)
        {
            PowerCoveragePreviewOverlay.Instance.Clear();
        }
    }

    private void CreateGhostBuilding()
    {
        if (_activeBuildingData == null || _activeBuildingData.buildingPrefab == null) return;

        _ghostBuildingInstance = Instantiate(_activeBuildingData.buildingPrefab, Vector3.zero, Quaternion.identity);
        SetGhostSortingLayer();
        _ghostBuildingRenderer = _ghostBuildingInstance.GetComponent<SpriteRenderer>();

        const float ghostAlpha = 0.9f;
        SpriteRenderer[] ghostRenderers = _ghostBuildingInstance.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < ghostRenderers.Length; i++)
        {
            SpriteRenderer r = ghostRenderers[i];
            if (r != null)
            {
                Color c = r.color;
                c.a = ghostAlpha;
                r.color = c;
            }
        }

        Collider2D[] colliders = _ghostBuildingInstance.GetComponentsInChildren<Collider2D>(true);
        foreach (var collider in colliders)
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        VisionProvider[] visionProviders = _ghostBuildingInstance.GetComponentsInChildren<VisionProvider>(true);
        foreach (var visionProvider in visionProviders)
        {
            if (visionProvider != null)
            {
                visionProvider.SetActive(false);
                if (FogOfWarManager.Instance != null)
                {
                    FogOfWarManager.Instance.UnregisterVisionProvider(visionProvider);
                }
            }
        }
    }

    private void SetGhostSortingLayer()
    {
        if (_ghostBuildingInstance == null)
        {
            return;
        }

        bool hasDraggingLayer = false;
        SortingLayer[] sortingLayers = SortingLayer.layers;
        for (int i = 0; i < sortingLayers.Length; i++)
        {
            if (sortingLayers[i].name == DraggingSortingLayerName)
            {
                hasDraggingLayer = true;
                break;
            }
        }

        if (!hasDraggingLayer)
        {
            return;
        }

        SpriteRenderer[] renderers = _ghostBuildingInstance.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null)
            {
                renderer.sortingLayerName = DraggingSortingLayerName;
            }
        }
    }

    private void HandleDragVisuals()
    {
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        Vector3Int cellPosition = grid.WorldToCell(mouseWorldPos);
        Vector3 cellCenterWorld = grid.GetCellCenterWorld(cellPosition);

        bool isPreviewDragging = _isDragPlacing && _activeBuildingData != null &&
                                 (_activeBuildingData.buildingType == BuildingType.Platform ||
                                  _activeBuildingData.buildingType == BuildingType.Wall);

        if (_ghostBuildingInstance != null)
        {
            _ghostBuildingInstance.transform.position = cellCenterWorld;
            _ghostBuildingInstance.SetActive(!isPreviewDragging);
        }

        if (isPreviewDragging)
        {
            if (_activeBuildingData.buildingType == BuildingType.Platform)
            {
                _dragCurrentCell = cellPosition;
                UpdatePlatformPreview(_dragStartCell, _dragCurrentCell);
            }
            else
            {
                UpdateLinePreview(_dragStartCell, cellPosition);
            }
            return;
        }

        if (IsDraggingPowerGridPreviewBuilding && PowerCoveragePreviewOverlay.Instance != null)
        {
            PowerCoveragePreviewOverlay.Instance.ShowIncludingPlacementPreview(_activeBuildingData, cellPosition, grid);
        }

        bool canPlacePattern = CanPlaceComboPattern(cellPosition);
        bool hasResources = HasEnoughResourcesForCombo();
        bool canPlace = canPlacePattern && hasResources;

        UpdateGhostColor(canPlace);
    }

    private bool CanPlaceComboPattern(Vector3Int anchorCell)
    {
        if (_activeBuildingData == null || _activeBuildingData.recipe == null)
        {
            return false;
        }

        if (!IsPositionInBoundsForPlacement(anchorCell))
        {
            return false;
        }

        return BuildingManager.Instance.CanPlaceBuildingAtAnchor(anchorCell, _activeBuildingData);
    }

    private bool HasEnoughResourcesForCombo()
    {
        if (_cachedComboCosts == null || _cachedComboCosts.Count == 0)
        {
            return true;
        }

        if (ResourceManager.Instance == null)
        {
            return false;
        }

        return ResourceManager.Instance.HasEnoughResources(_cachedComboCostArray);
    }

    private void UpdateGhostColor(bool canPlace)
    {
        if (_ghostBuildingRenderer == null) return;

        float a = _ghostBuildingRenderer.color.a;
        Color tint = canPlace ? Color.green : Color.red;
        _ghostBuildingRenderer.color = new Color(tint.r, tint.g, tint.b, a);
    }

    private void HandleMousePlacement()
    {
        if (IsLoadingScreenActive())
        {
            return;
        }

        if (_activeBuildingData != null && _activeBuildingData.buildingType == BuildingType.Wall)
        {
            HandleWallPlacement();
            return;
        }

        if (_activeBuildingData != null && _activeBuildingData.buildingType == BuildingType.Platform)
        {
            HandlePlatformPlacement();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (UIUtils.IsPointerOverUI())
            {
                return;
            }

            if (IsPointerOverDragEndZone())
            {
                EndDrag();
                return;
            }

            Vector3Int cellPosition = grid.WorldToCell(_mainCamera.ScreenToWorldPoint(Input.mousePosition));

            if (cellPosition != _lastPlacedCell && !_placedCellsInDrag.Contains(cellPosition))
            {
                if (CanPlaceComboPattern(cellPosition) && HasEnoughResourcesForCombo())
                {
                    AttemptPlacement(cellPosition);
                    _lastPlacedCell = cellPosition;
                }
            }
        }
        else if (Input.GetMouseButtonUp(1))
        {
            if (UIUtils.IsPointerOverUI())
            {
                return;
            }

            GameManager.Instance.EndDrag();
        }
    }

    private void HandleWallPlacement()
    {
        if (Input.GetMouseButtonUp(1))
        {
            if (UIUtils.IsPointerOverUI())
            {
                return;
            }

            ClearWallPreview();
            _isDragPlacing = false;
            GameManager.Instance.EndDrag();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (UIUtils.IsPointerOverUI())
            {
                return;
            }

            if (IsPointerOverDragEndZone())
            {
                EndDrag();
                return;
            }

            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;
            _dragStartCell = grid.WorldToCell(mouseWorldPos);
            _isDragPlacing = true;
            return;
        }

        if (Input.GetMouseButtonUp(0) && _isDragPlacing)
        {
            if (UIUtils.IsPointerOverUI())
            {
                ClearWallPreview();
                _isDragPlacing = false;
                return;
            }

            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;
            Vector3Int releaseCell = grid.WorldToCell(mouseWorldPos);

            ClearWallPreview();
            _isDragPlacing = false;

            PlaceWallLine(_dragStartCell, releaseCell);
        }
    }

    private void HandlePlatformPlacement()
    {
        if (Input.GetMouseButtonUp(1))
        {
            if (UIUtils.IsPointerOverUI())
            {
                return;
            }

            ClearPlatformPreview();
            _isDragPlacing = false;
            GameManager.Instance.EndDrag();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (UIUtils.IsPointerOverUI())
            {
                return;
            }

            if (IsPointerOverDragEndZone())
            {
                EndDrag();
                return;
            }

            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;
            _dragStartCell = grid.WorldToCell(mouseWorldPos);
            _dragCurrentCell = _dragStartCell;
            _isDragPlacing = true;
            return;
        }

        if (Input.GetMouseButtonUp(0) && _isDragPlacing)
        {
            if (UIUtils.IsPointerOverUI())
            {
                ClearPlatformPreview();
                _isDragPlacing = false;
                return;
            }

            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;
            Vector3Int releaseCell = grid.WorldToCell(mouseWorldPos);

            ClearPlatformPreview();
            _isDragPlacing = false;

            PlacePlatformRectangle(_dragStartCell, releaseCell);
        }
    }

    private void UpdateLinePreview(Vector3Int startCell, Vector3Int endCell)
    {
        if (wallPreviewTilemap == null || wallPreviewTile == null)
        {
            return;
        }

        wallPreviewTilemap.ClearAllTiles();

        if (_bresenhamScratch == null)
        {
            _bresenhamScratch = new List<Vector3Int>();
        }

        GetBresenhamLine(startCell, endCell, _bresenhamScratch);

        for (int i = 0; i < _bresenhamScratch.Count; i++)
        {
            if (CanPlaceComboPattern(_bresenhamScratch[i]))
            {
                wallPreviewTilemap.SetTile(_bresenhamScratch[i], wallPreviewTile);
            }
        }
    }

    private void PlaceWallLine(Vector3Int startCell, Vector3Int endCell)
    {
        if (_bresenhamScratch == null)
        {
            _bresenhamScratch = new List<Vector3Int>();
        }

        GetBresenhamLine(startCell, endCell, _bresenhamScratch);

        for (int i = 0; i < _bresenhamScratch.Count; i++)
        {
            Vector3Int cell = _bresenhamScratch[i];
            if (_placedCellsInDrag.Contains(cell))
            {
                continue;
            }

            if (CanPlaceComboPattern(cell) && HasEnoughResourcesForCombo())
            {
                AttemptPlacement(cell);
            }
        }
    }

    private void UpdatePlatformPreview(Vector3Int startCell, Vector3Int endCell)
    {
        if (platformPreviewTilemap == null || platformPreviewTile == null)
        {
            return;
        }

        platformPreviewTilemap.ClearAllTiles();

        int minX = Mathf.Min(startCell.x, endCell.x);
        int maxX = Mathf.Max(startCell.x, endCell.x);
        int minY = Mathf.Min(startCell.y, endCell.y);
        int maxY = Mathf.Max(startCell.y, endCell.y);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                platformPreviewTilemap.SetTile(new Vector3Int(x, y, 0), platformPreviewTile);
            }
        }
    }

    private void ClearPlatformPreview()
    {
        if (platformPreviewTilemap == null)
        {
            return;
        }

        platformPreviewTilemap.ClearAllTiles();
    }

    private void ClearWallPreview()
    {
        if (wallPreviewTilemap == null)
        {
            return;
        }

        wallPreviewTilemap.ClearAllTiles();
    }

    private void PlacePlatformRectangle(Vector3Int startCell, Vector3Int endCell)
    {
        int minX = Mathf.Min(startCell.x, endCell.x);
        int maxX = Mathf.Max(startCell.x, endCell.x);
        int minY = Mathf.Min(startCell.y, endCell.y);
        int maxY = Mathf.Max(startCell.y, endCell.y);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (_placedCellsInDrag.Contains(cell))
                {
                    continue;
                }

                if (CanPlaceComboPattern(cell) && HasEnoughResourcesForCombo())
                {
                    AttemptPlacement(cell);
                }
            }
        }
    }

    private void GetBresenhamLine(Vector3Int from, Vector3Int to, List<Vector3Int> results)
    {
        results.Clear();

        int x0 = from.x;
        int y0 = from.y;
        int x1 = to.x;
        int y1 = to.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            results.Add(new Vector3Int(x0, y0, 0));

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void AttemptPlacement(Vector3Int cellPos)
    {
        if (_activeBuildingData != null)
        {
            BuildingManager.Instance.CreateConstructionSite(_activeBuildingData, cellPos);
            _placedCellsInDrag.Add(cellPos);
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnBuildingPlaced(_activeBuildingData.buildingType);
        }
    }

    private bool IsPositionInBoundsForPlacement(Vector3Int cellPos)
    {
        if (GameManager.Instance.mapGenerator == null) return false;

        Vector3 worldPos = grid.GetCellCenterWorld(cellPos);
        return GameManager.Instance.mapGenerator.IsPositionInBounds(worldPos);
    }

    private bool IsPointerOverDragEndZone()
    {
        if (EventSystem.current == null) return false;

        if (_pointerEventData == null) _pointerEventData = new PointerEventData(EventSystem.current);
        _pointerEventData.position = Input.mousePosition;

        _raycastResultsCache.Clear();
        EventSystem.current.RaycastAll(_pointerEventData, _raycastResultsCache);

        foreach (RaycastResult result in _raycastResultsCache)
        {
            if (result.gameObject.GetComponent<UIDragEndZone>() != null)
            {
                return true;
            }
        }
        return false;
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
}
