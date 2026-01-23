using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDragger : MonoBehaviour
{
    public bool IsDragging => _isDragging;

    [Header("References")]
    [SerializeField] private Grid grid;

    private GameObject _ghostBuildingInstance;
    private SpriteRenderer _ghostBuildingRenderer;
    private BuildingData _activeBuildingData;
    private Vector3Int _lastPlacedCell;
    private bool _isDragging;
    
    private List<Vector3Int> _placedCellsInDrag;
    private PointerEventData _pointerEventData;
    private List<ResourceCost> _cachedComboCosts;
    
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

            CreateGhostBuilding();
        }
    }
    
    private List<ResourceCost> CalculateComboCosts(BuildingData comboData)
    {
        List<ResourceCost> totalCosts = new List<ResourceCost>();
        Dictionary<ResourceType, int> costDict = new Dictionary<ResourceType, int>();
        
        BuildingPieceData[] allCards = Resources.LoadAll<BuildingPieceData>("Building Pieces");
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
        if (_ghostBuildingInstance != null)
        {
            Destroy(_ghostBuildingInstance);
        }
        
        _isDragging = false;
        _activeBuildingData = null;
        _cachedComboCosts = null;
        _ghostBuildingRenderer = null;
    }
    
    private void CreateGhostBuilding()
    {
        if (_activeBuildingData == null || _activeBuildingData.buildingPrefab == null) return;
        
        _ghostBuildingInstance = Instantiate(_activeBuildingData.buildingPrefab, Vector3.zero, Quaternion.identity);
        _ghostBuildingRenderer = _ghostBuildingInstance.GetComponent<SpriteRenderer>();

        if (_ghostBuildingRenderer != null)
        {
            Color ghostColor = _ghostBuildingRenderer.color;
            ghostColor.a = 0.5f;
            _ghostBuildingRenderer.color = ghostColor;
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

    private void HandleDragVisuals()
    {
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        Vector3Int cellPosition = grid.WorldToCell(mouseWorldPos);
        Vector3 cellCenterWorld = grid.GetCellCenterWorld(cellPosition);

        if (_ghostBuildingInstance != null)
        {
            _ghostBuildingInstance.transform.position = cellCenterWorld;
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
        
        foreach (var piece in _activeBuildingData.recipe)
        {
            Vector3Int cellPos = anchorCell + piece.relativePosition;
            if (!BuildingManager.Instance.CanPlaceBuilding(cellPos))
            {
                return false;
            }
        }
        
        return true;
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
        
        return ResourceManager.Instance.HasEnoughResources(_cachedComboCosts.ToArray());
    }

    private void UpdateGhostColor(bool canPlace)
    {
        if (_ghostBuildingRenderer == null) return;

        Color color = canPlace ? Color.green : Color.red;
        _ghostBuildingRenderer.color = new Color(color.r, color.g, color.b, 0.5f);
    }
    
    private void HandleMousePlacement()
    {
        if (IsLoadingScreenActive())
        {
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

            Vector3Int cellPosition = grid.WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));

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

    private void AttemptPlacement(Vector3Int cellPos)
    {
        if (_activeBuildingData != null)
        {
            BuildingManager.Instance.CreateConstructionSite(_activeBuildingData, cellPos);
            _placedCellsInDrag.Add(cellPos);
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

        _pointerEventData ??= new PointerEventData(EventSystem.current);
        _pointerEventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(_pointerEventData, results);

        foreach (RaycastResult result in results)
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