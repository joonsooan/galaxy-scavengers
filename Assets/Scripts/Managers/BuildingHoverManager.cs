using UnityEngine;

public class BuildingHoverManager : MonoBehaviour
{
    private Camera _mainCamera;
    private BuildingPiece _currentHoveredBuilding;

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsDragging())
        {
            ClearHover();
            return;
        }

        if (UIUtils.IsPointerOverUI())
        {
            ClearHover();
            return;
        }

        CheckBuildingHover();
    }

    private void CheckBuildingHover()
    {
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero);

        BuildingPiece hoveredBuilding = null;

        if (hit.collider != null)
        {
            hoveredBuilding = hit.collider.GetComponent<BuildingPiece>();
        }

        if (hoveredBuilding != _currentHoveredBuilding)
        {
            ClearHover();

            if (hoveredBuilding != null)
            {
                ShowBuildingInfo(hoveredBuilding);
                _currentHoveredBuilding = hoveredBuilding;
            }
        }
    }

    private void ShowBuildingInfo(BuildingPiece buildingPiece)
    {
        if (BuildingInfoPanel.Instance == null) return;
        if (BuildingManager.Instance == null) return;

        Vector3Int cellPosition = buildingPiece.cellPosition;
        if (cellPosition == Vector3Int.zero && BuildingManager.Instance.grid != null)
        {
            cellPosition = BuildingManager.Instance.grid.WorldToCell(buildingPiece.transform.position);
        }

        BuildingData buildingData = BuildingManager.Instance.GetBuildingDataAt(cellPosition);
        
        if (buildingData != null)
        {
            BuildingInfoPanel.Instance.PreviewInfo(buildingData);
        }
    }

    private void ClearHover()
    {
        if (_currentHoveredBuilding != null)
        {
            if (BuildingInfoPanel.Instance != null)
            {
                BuildingInfoPanel.Instance.CancelPreview();
            }
            _currentHoveredBuilding = null;
        }
    }

    private void OnDisable()
    {
        ClearHover();
    }
}

