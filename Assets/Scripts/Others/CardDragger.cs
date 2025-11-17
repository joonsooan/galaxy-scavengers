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
    private CardData _activeCardData;
    private Vector3Int _lastPlacedCell;
    private bool _isDragging;
    private List<Vector3Int> _placedCellsInDrag;
    private PointerEventData _pointerEventData;
    
    private void Update()
    {
        if (!_isDragging) return;

        HandleDragVisuals();
        HandleMousePlacement();
    }
    
    public void StartDrag(DisplayableData data)
    {
        if (_isDragging) return;

        CardData cardData = data as CardData;
        if (cardData == null || cardData.buildingPrefab == null) return;

        _activeCardData = cardData;
        _isDragging = true;
        _lastPlacedCell = Vector3Int.one * int.MaxValue;
        _placedCellsInDrag = new List<Vector3Int>();

        CreateGhostBuilding();
    }

    public void EndDrag()
    {
        if (_ghostBuildingInstance != null)
        {
            Destroy(_ghostBuildingInstance);
        }

        GameManager.Instance.uiManager?.UnpinAndHideCardPanel();

        _isDragging = false;
        _activeCardData = null;
        _ghostBuildingRenderer = null;
    }
    
    private void CreateGhostBuilding()
    {
        _ghostBuildingInstance = Instantiate(_activeCardData.buildingPrefab, Vector3.zero, Quaternion.identity);
        _ghostBuildingRenderer = _ghostBuildingInstance.GetComponent<SpriteRenderer>();

        if (_ghostBuildingRenderer != null)
        {
            Color ghostColor = _ghostBuildingRenderer.color;
            ghostColor.a = 0.5f;
            _ghostBuildingRenderer.color = ghostColor;
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

        bool canPlace = BuildingManager.Instance.CanPlaceBuilding(cellPosition) &&
                        IsRoomUnlockedForPlacement(cellPosition) &&
                        ResourceManager.Instance.HasEnoughResources(_activeCardData.costs);

        UpdateGhostColor(canPlace);
    }

    private void UpdateGhostColor(bool canPlace)
    {
        if (_ghostBuildingRenderer == null) return;

        Color color = canPlace ? Color.green : Color.red;
        _ghostBuildingRenderer.color = new Color(color.r, color.g, color.b, 0.5f);
    }
    
    private void HandleMousePlacement()
    {
        if (Input.GetMouseButton(0))
        {
            if (_placedCellsInDrag.Count > 0 && IsPointerOverDragEndZone())
            {
                EndDrag();
                return;
            }

            Vector3Int cellPosition = grid.WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));

            if (cellPosition != _lastPlacedCell && !_placedCellsInDrag.Contains(cellPosition))
            {
                if (BuildingManager.Instance.CanPlaceBuilding(cellPosition) && IsRoomUnlockedForPlacement(cellPosition))
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
        if (ResourceManager.Instance.HasEnoughResources(_activeCardData.costs))
        {
            BuildingManager.Instance.PlaceBuilding(_activeCardData, cellPos);
            ResourceManager.Instance.SpendResources(_activeCardData.costs);
            _placedCellsInDrag.Add(cellPos);
        }
        else
        {
            Debug.Log("Not enough resources.");
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