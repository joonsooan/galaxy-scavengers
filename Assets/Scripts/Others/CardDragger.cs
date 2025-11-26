using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDragger : MonoBehaviour
{
    public bool IsDragging => _isDragging;

    [Header("References")]
    [SerializeField] private Grid grid;

    private GameObject _ghostBuildingInstance;
    private SpriteRenderer _ghostBuildingRenderer;
    private ComboCardData _activeComboCardData;
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
        if (_isDragging) return;

        // Only support ComboCardData
        ComboCardData comboCardData = data as ComboCardData;
        if (comboCardData != null && comboCardData.comboPrefab != null)
        {
            _activeComboCardData = comboCardData;
            _isDragging = true;
            _lastPlacedCell = Vector3Int.one * int.MaxValue;
            _placedCellsInDrag = new List<Vector3Int>();
            
            // Calculate and cache costs from recipe
            _cachedComboCosts = CalculateComboCosts(comboCardData);

            CreateGhostBuilding();
        }
    }
    
    private List<ResourceCost> CalculateComboCosts(ComboCardData comboData)
    {
        List<ResourceCost> totalCosts = new List<ResourceCost>();
        Dictionary<ResourceType, int> costDict = new Dictionary<ResourceType, int>();
        
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
        if (comboData.recipe != null)
        {
            foreach (var piece in comboData.recipe)
            {
                if (gadgetToCardMap.TryGetValue(piece.gadgetType, out CardData pieceCard) && pieceCard.costs != null)
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
        
        // Convert dictionary to list
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

        GameManager.Instance.uiManager?.UnpinAndHideCardPanel();

        _isDragging = false;
        _activeComboCardData = null;
        _cachedComboCosts = null;
        _ghostBuildingRenderer = null;
    }
    
    private void CreateGhostBuilding()
    {
        if (_activeComboCardData == null || _activeComboCardData.comboPrefab == null) return;
        
        _ghostBuildingInstance = Instantiate(_activeComboCardData.comboPrefab, Vector3.zero, Quaternion.identity);
        _ghostBuildingRenderer = _ghostBuildingInstance.GetComponent<SpriteRenderer>();

        if (_ghostBuildingRenderer != null)
        {
            Color ghostColor = _ghostBuildingRenderer.color;
            ghostColor.a = 0.5f;
            _ghostBuildingRenderer.color = ghostColor;
        }
        
        // Disable VisionProvider components on ghost building to prevent fog of war updates during drag
        VisionProvider[] visionProviders = _ghostBuildingInstance.GetComponentsInChildren<VisionProvider>(true);
        foreach (var visionProvider in visionProviders)
        {
            if (visionProvider != null)
            {
                visionProvider.SetActive(false);
                // Unregister immediately in case it already registered during OnEnable
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

        // Position ghost building at the grid cell center (snap to grid)
        if (_ghostBuildingInstance != null)
        {
            _ghostBuildingInstance.transform.position = cellCenterWorld;
        }

        // Check if the entire combo pattern can be placed
        bool canPlacePattern = CanPlaceComboPattern(cellPosition);
        
        // Check if resources are available
        bool hasResources = HasEnoughResourcesForCombo();
        
        // Can place if pattern is valid AND resources are available
        bool canPlace = canPlacePattern && hasResources;

        UpdateGhostColor(canPlace);
    }
    
    private bool CanPlaceComboPattern(Vector3Int anchorCell)
    {
        if (_activeComboCardData == null || _activeComboCardData.recipe == null)
        {
            return false;
        }
        
        // Check if room is unlocked for the anchor position
        if (!IsRoomUnlockedForPlacement(anchorCell))
        {
            return false;
        }
        
        // Check if all cells in the recipe pattern can be placed
        foreach (var piece in _activeComboCardData.recipe)
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
            return false;
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
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverDragEndZone())
            {
                EndDrag();
                return;
            }

            Vector3Int cellPosition = grid.WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));

            if (cellPosition != _lastPlacedCell && !_placedCellsInDrag.Contains(cellPosition))
            {
                // Check if the entire combo pattern can be placed and resources are available
                if (CanPlaceComboPattern(cellPosition) && HasEnoughResourcesForCombo())
                {
                    AttemptPlacement(cellPosition);
                    _lastPlacedCell = cellPosition;
                }
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            GameManager.Instance.EndDrag();
        }
    }

    private void AttemptPlacement(Vector3Int cellPos)
    {
        if (_activeComboCardData != null)
        {
            // Create construction site for combo building
            BuildingManager.Instance.CreateComboConstructionSite(_activeComboCardData, cellPos);
            _placedCellsInDrag.Add(cellPos);
        }
    }

    private bool IsRoomUnlockedForPlacement(Vector3Int cellPos)
    {
        if (GameManager.Instance.mapGenerator == null) return false;

        Vector2Int roomCoordinates = GameManager.Instance.mapGenerator.GetRoomCoordinates(grid.GetCellCenterWorld(cellPos));
        return GameManager.Instance.mapGenerator.IsRoomUnlocked(roomCoordinates.x, roomCoordinates.y);
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
}